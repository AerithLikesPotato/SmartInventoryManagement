using System.Data.SqlTypes;
using Microsoft.AspNetCore.Identity;
using SmartInventory.Infrastructure.Identity;

namespace SmartInventory.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAdminUserAsync(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        
        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            await roleManager.CreateAsync(new IdentityRole("Admin"));
        }
        if (!await roleManager.RoleExistsAsync("Cashier"))
        {
            await roleManager.CreateAsync(new IdentityRole("Cashier"));
        }

        
        const string adminEmail = "admin@smartinventory.com";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);

        if (adminUser == null)
        {
            var user = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FullName = "System Administrator",
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(user, "Admin123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, "Admin");
            }
        }

        const string cashierEmail = "cashier@SmartInventory.com";
        var cashierUser = await userManager.FindByEmailAsync(cashierEmail);
        if (cashierUser == null)
        {
            var cashier = new ApplicationUser
            {
                UserName = cashierEmail,
                Email = cashierEmail,
                FullName = "Cashier",
                EmailConfirmed = true
            };
            if ((await userManager.CreateAsync(cashier, "Cashier123!")).Succeeded)
            {
                await userManager.AddToRoleAsync(cashier, "Cashier");
            }
        }
    }
}