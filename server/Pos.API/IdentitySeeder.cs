using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Infrastructure.Identity;
using Pos.Infrastructure.Persistence;

namespace Pos.Api;

/// <summary>
/// Ensures the four fixed roles exist, and — in Development only — seeds one
/// Admin user so there's something to log in with before Step 13's invite flow
/// exists. Remove the dev-admin block (or gate it behind an even stricter check)
/// once Step 13 ships; it's a bootstrap convenience, not a real onboarding path.
/// </summary>
public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration config, bool isDevelopment)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var db = services.GetRequiredService<PosDbContext>();

        foreach (var roleName in Enum.GetNames<RegisterUserRole>())
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
            }
        }

        if (!isDevelopment) return;

        var devAdminEmail = config["DevSeed:AdminEmail"];
        var devAdminPassword = config["DevSeed:AdminPassword"];
        if (string.IsNullOrEmpty(devAdminEmail) || string.IsNullOrEmpty(devAdminPassword)) return;

        if (await userManager.FindByEmailAsync(devAdminEmail) is not null) return; // already seeded

        var appUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = devAdminEmail,
            Email = devAdminEmail,
            EmailConfirmed = true,
        };

        var createResult = await userManager.CreateAsync(appUser, devAdminPassword);
        if (!createResult.Succeeded) return;

        await userManager.AddToRoleAsync(appUser, nameof(RegisterUserRole.Admin));

        db.DomainUsers.Add(new User
        {
            Id = appUser.Id, // shared primary key with the Identity row
            FullName = "Dev Admin",
            Email = devAdminEmail,
            Role = RegisterUserRole.Admin,
            IsActive = true,
        });
        await db.SaveChangesAsync();
    }
}