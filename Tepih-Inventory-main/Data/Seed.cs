using Microsoft.AspNetCore.Identity;

namespace Inventar.Data
{
    public class Seed
    {
        public static async Task SeedUsersAndRolesAsync(IApplicationBuilder applicationBuilder)
        {
            using (var serviceScope = applicationBuilder.ApplicationServices.CreateScope())
            {
                //Roles
                var roleManager = serviceScope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

                //if (!await roleManager.RoleExistsAsync(UserRoles.Admin))
                //    await roleManager.CreateAsync(new IdentityRole(UserRoles.Admin));
                //if (!await roleManager.RoleExistsAsync(UserRoles.User))
                //    await roleManager.CreateAsync(new IdentityRole(UserRoles.User));
                if (!await roleManager.RoleExistsAsync(UserRoles.SuperAdmin))
                    await roleManager.CreateAsync(new IdentityRole(UserRoles.SuperAdmin));
                if (!await roleManager.RoleExistsAsync(UserRoles.Employee))
                    await roleManager.CreateAsync(new IdentityRole(UserRoles.Employee));
            }
        }
    }
}
