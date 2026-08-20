using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Infrastructure.Identity;
using Pos.Infrastructure.Persistence;
using Pos.Application.Common.Interfaces;

namespace Pos.Api;

public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration config, bool isDevelopment)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var db = services.GetRequiredService<PosDbContext>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(IdentitySeeder));
        var mfaService = services.GetService<IMfaService>();

        await db.Database.OpenConnectionAsync();

        foreach (var roleName in Enum.GetNames<RegisterUserRole>())
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
            }
        }

        if (!isDevelopment) return;

        await SeedDevUserAsync(
            userManager, db, logger,
            email: config["DevSeed:AdminEmail"],
            password: config["DevSeed:AdminPassword"],
            fullName: "Dev Admin",
            role: RegisterUserRole.Admin,
            assignedRegisterId: null,
            mfaService: mfaService);

        // Register-scoped authorization (Step 9) needs a Cashier tied to a real
        // register to test against — create both if config for it is present.
        var cashierEmail = config["DevSeed:CashierEmail"];
        var cashierPassword = config["DevSeed:CashierPassword"];
        if (!string.IsNullOrEmpty(cashierEmail) && !string.IsNullOrEmpty(cashierPassword))
        {
            var testRegister = await db.Registers.FirstOrDefaultAsync(r => r.Name == "Dev Test Register");
            if (testRegister is null)
            {
                testRegister = new Register { Name = "Dev Test Register", IsActive = true };
                db.Registers.Add(testRegister);
                await db.SaveChangesAsync();
                logger.LogInformation("Seeded Dev Test Register with Id {RegisterId}", testRegister.Id);
            }

            // Seed an open TillSession for the test register — without this, checkout
            // (Step 24) is untestable out of the box since a sale can't complete against
            // a closed till, and there'd be no way to open one via the UI on a totally
            // fresh database until someone manually calls POST /api/till/open. Dev-only
            // (same isDevelopment gate as this whole method), self-healing on every
            // startup (checks for an existing open session first), and requires the
            // Admin dev account to already be seeded above since it needs a real
            // OpenedByUserId to satisfy the FK.
            var hasOpenSession = await db.TillSessions
                .AnyAsync(t => t.RegisterId == testRegister.Id && t.Status == TillSessionStatus.Open);
            if (!hasOpenSession)
            {
                var adminEmail = config["DevSeed:AdminEmail"];
                var adminUserId = string.IsNullOrEmpty(adminEmail)
                    ? (Guid?)null
                    : await db.DomainUsers.Where(u => u.Email == adminEmail).Select(u => (Guid?)u.Id).FirstOrDefaultAsync();

                if (adminUserId is Guid openedByUserId)
                {
                    db.TillSessions.Add(new TillSession
                    {
                        RegisterId = testRegister.Id,
                        OpenedByUserId = openedByUserId,
                        OpenedAt = DateTime.UtcNow,
                        OpeningFloat = 5000m, // a plausible KES opening float for dev testing
                        Status = TillSessionStatus.Open,
                    });
                    await db.SaveChangesAsync();
                    logger.LogInformation("Opened a dev TillSession on Dev Test Register {RegisterId}", testRegister.Id);
                }
                else
                {
                    logger.LogWarning(
                        "Could not seed an open TillSession for Dev Test Register — DevSeed:AdminEmail is missing or that user hasn't been seeded yet. Open the till manually via POST /api/till/open.");
                }
            }

            await SeedDevUserAsync(
                userManager, db, logger,
                email: cashierEmail,
                password: cashierPassword,
                fullName: "Dev Cashier",
                role: RegisterUserRole.Cashier,
                assignedRegisterId: testRegister.Id,
                mfaService: mfaService);
        }
    }

    private static async Task SeedDevUserAsync(
        UserManager<ApplicationUser> userManager,
        PosDbContext db,
        ILogger logger,
        string? email,
        string? password,
        string fullName,
        RegisterUserRole role,
        Guid? assignedRegisterId,
        IMfaService? mfaService)
    {
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password)) return;
        if (await userManager.FindByEmailAsync(email) is not null) return; // already seeded

        var appUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
        };

        var createResult = await userManager.CreateAsync(appUser, password);
        if (!createResult.Succeeded)
        {
            var errors = string.Join("; ", createResult.Errors.Select(e => $"{e.Code}: {e.Description}"));
            logger.LogError("Dev user seeding failed for {Email} — user was NOT created: {Errors}", email, errors);
            return;
        }

        await userManager.AddToRoleAsync(appUser, role.ToString());

        var domainUser = new User
        {
            Id = appUser.Id, // shared primary key with the Identity row
            FullName = fullName,
            Email = email,
            Role = role,
            AssignedRegisterId = assignedRegisterId,
            IsActive = true,
        };

        // Auto-enable MFA for Admin/Manager in dev seeding so their logins require MFA immediately.
        /*if ((role == RegisterUserRole.Admin || role == RegisterUserRole.Manager) && mfaService is not null)
        {
            var rawSecret = mfaService.GenerateSecret();
            domainUser.MfaSecret = mfaService.EncryptSecret(rawSecret);
            domainUser.MfaEnabled = true;
            var uri = mfaService.GenerateOtpAuthUri(rawSecret, email);
            logger.LogInformation("Enabled MFA for seeded {Role} user: {Email}. Scan this OTP URI with your authenticator app: {Uri}", role, email, uri);
            // Note: To actually enroll an authenticator app, expose the otpAuthUri via /api/auth/mfa/setup if needed.
        }*/

        db.DomainUsers.Add(domainUser);
        await db.SaveChangesAsync();

        logger.LogInformation("Seeded dev {Role} user: {Email}", role, email);
    }
}