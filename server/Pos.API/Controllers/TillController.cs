using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pos.Api.Authorization;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Infrastructure.Persistence;
using System.Security.Claims;
using Pos.Application.Common.Interfaces;

namespace Pos.Api.Controllers;

/// <summary>
/// Step 25 — till open/close per register per shift, with expected-vs-counted cash
/// reconciliation at close. Any register-capable role can open or close (Cashier
/// restricted to their assigned register via the RegisterScoped policy, same as
/// checkout; Manager/Admin can operate any register).
/// </summary>
[ApiController]
[Route("api/till")]
[Authorize(Roles = RoleGroups.RegisterCapableRoles)]
public sealed class TillController : ControllerBase
{
    private readonly PosDbContext _db;
    private readonly IAuthorizationService _authorizationService;
    private readonly IAuditService _auditService;

    public TillController(IAuditService auditService, PosDbContext db, IAuthorizationService authorizationService)
    {
        _db = db;
        _authorizationService = authorizationService;
        _auditService = auditService;
    }

    /// <summary>The currently open session for a register, or 204 No Content if the till
    /// is closed. Lets the checkout screen know whether to show "open till" or "close
    /// till" and display who opened it / with what float.</summary>
    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent([FromQuery] Guid registerId, CancellationToken cancellationToken)
    {
        var authResult = await _authorizationService.AuthorizeAsync(User, registerId, PolicyNames.RegisterScoped);
        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var session = await _db.TillSessions
            .AsNoTracking()
            .Where(t => t.RegisterId == registerId && t.Status == TillSessionStatus.Open)
            .Select(t => new TillSessionResponse(
                t.Id, t.RegisterId, t.Register.Name, t.OpenedByUserId, t.OpenedByUser.FullName,
                t.OpenedAt, t.OpeningFloat, t.Status.ToString()))
            .FirstOrDefaultAsync(cancellationToken);

        return session is null ? NoContent() : Ok(session);
    }

    [HttpPost("open")]
    public async Task<IActionResult> Open([FromBody] OpenTillRequest request, CancellationToken cancellationToken)
    {
        if (request.OpeningFloat < 0)
        {
            return BadRequest("Opening float cannot be negative.");
        }

        var authResult = await _authorizationService.AuthorizeAsync(User, request.RegisterId, PolicyNames.RegisterScoped);
        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var register = await _db.Registers.FirstOrDefaultAsync(r => r.Id == request.RegisterId, cancellationToken);
        if (register is null || !register.IsActive)
        {
            return BadRequest("Register not found or inactive.");
        }

        var alreadyOpen = await _db.TillSessions
            .AnyAsync(t => t.RegisterId == request.RegisterId && t.Status == TillSessionStatus.Open, cancellationToken);
        if (alreadyOpen)
        {
            return Conflict(new { message = "This register's till is already open." });
        }

        var session = new TillSession
        {
            RegisterId = request.RegisterId,
            OpenedByUserId = userId,
            OpenedAt = DateTime.UtcNow,
            OpeningFloat = request.OpeningFloat,
            Status = TillSessionStatus.Open,
        };

        _db.TillSessions.Add(session);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            if (await IsAlreadyOpenAsync(request.RegisterId, cancellationToken))
            {
                // Lost a race against another open request for the same register — the
                // partial unique index on (RegisterId) WHERE Status = Open is what actually
                // stops this at the DB level; this just turns the resulting exception into a
                // clean 409 instead of a 500.
                return Conflict(new { message = "This register's till is already open." });
            }

            throw;
        }

        var openedByName = await _db.DomainUsers
            .Where(u => u.Id == userId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(cancellationToken) ?? "";
        
        await _auditService.LogAsync(
            userId: userId,
            actionType: "TILL_OPENED",
            entityName: "TillSession",
            entityId: session.Id,
            details: $"Opened till on register {register.Id} with float {session.OpeningFloat}"
        );

        return Ok(new TillSessionResponse(
            session.Id, register.Id, register.Name, userId, openedByName,
            session.OpenedAt, session.OpeningFloat, session.Status.ToString()));
    }

    [HttpPost("close")]
    public async Task<IActionResult> Close([FromBody] CloseTillRequest request, CancellationToken cancellationToken)
    {
        if (request.CountedCashAtClose < 0)
        {
            return BadRequest("Counted cash cannot be negative.");
        }

        var authResult = await _authorizationService.AuthorizeAsync(User, request.RegisterId, PolicyNames.RegisterScoped);
        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var session = await _db.TillSessions
            .FirstOrDefaultAsync(t => t.RegisterId == request.RegisterId && t.Status == TillSessionStatus.Open, cancellationToken);
        if (session is null)
        {
            return BadRequest("This register's till isn't open.");
        }

        // Only Completed sales count toward reconciliation — a Held (in-progress, not
        // yet paid) sale shouldn't be able to exist at this point since checkout keeps
        // those client-side until completion, but Voided/Refunded sales are explicitly
        // excluded on purpose: this session's till drawer already accounted for a
        // refund's cash impact at the moment it happened (Step 29's scope), not here.
        var paymentsBySale = await _db.Payments
            .Where(p => p.Sale.TillSessionId == session.Id && p.Sale.Status == SaleStatus.Completed)
            .ToListAsync(cancellationToken);

        var cashTotal = paymentsBySale.Where(p => p.Method == PaymentMethod.Cash && p.Status == PaymentStatus.Success).Sum(p => p.Amount);
        var mpesaTotal = paymentsBySale.Where(p => p.Method == PaymentMethod.Mpesa && p.Status == PaymentStatus.Success).Sum(p => p.Amount);
        var cardTotal = paymentsBySale.Where(p => p.Method == PaymentMethod.Card && p.Status == PaymentStatus.Success).Sum(p => p.Amount);

        var expectedCash = session.OpeningFloat + cashTotal;
        var variance = request.CountedCashAtClose - expectedCash;

        session.ClosedByUserId = userId;
        session.ClosedAt = DateTime.UtcNow;
        session.ExpectedCashAtClose = expectedCash;
        session.CountedCashAtClose = request.CountedCashAtClose;
        session.VarianceAtClose = variance;
        session.Status = TillSessionStatus.Closed;

        await _db.SaveChangesAsync(cancellationToken);
        
        await _auditService.LogAsync(
            userId: userId,
            actionType: "TILL_CLOSED",
            entityName: "TillSession",
            entityId: session.Id,
            details: $"Closed till on register {session.RegisterId}, expected: {expectedCash}, counted: {request.CountedCashAtClose}, variance: {variance}"
        );

        return Ok(new TillReconciliationResponse(
            session.Id, session.RegisterId, session.OpenedAt, session.ClosedAt.Value,
            session.OpeningFloat, cashTotal, expectedCash, request.CountedCashAtClose, variance,
            mpesaTotal, cardTotal));
    }

    private async Task<bool> IsAlreadyOpenAsync(Guid registerId, CancellationToken cancellationToken)
    {
        return await _db.TillSessions
            .AsNoTracking()
            .AnyAsync(t => t.RegisterId == registerId && t.Status == TillSessionStatus.Open, cancellationToken);
    }
}