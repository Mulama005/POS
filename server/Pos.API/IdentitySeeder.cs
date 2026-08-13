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