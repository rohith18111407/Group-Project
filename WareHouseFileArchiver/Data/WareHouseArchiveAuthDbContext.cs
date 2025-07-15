using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WareHouseFileArchiver.Models.Domains;

namespace WareHouseFileArchiver.Data
{
   public class WareHouseArchiveAuthDbContext : IdentityDbContext<ApplicationUser>
    {
        public WareHouseArchiveAuthDbContext(DbContextOptions<WareHouseArchiveAuthDbContext> options) : base(options)
        {
            
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            var adminRoleId = "a71a55d6-99d7-4123-b4e0-1218ecb90e3e";
            var staffRoleId = "c309fa92-2123-47be-b397-a1c77adb502c";

            var roles = new List<IdentityRole>
            {
                new IdentityRole
                {
                    Id = adminRoleId,
                    ConcurrencyStamp = adminRoleId,
                    Name = "Admin",
                    NormalizedName = "Admin".ToUpper()
                },
                new IdentityRole
                {
                    Id = staffRoleId,
                    ConcurrencyStamp = staffRoleId,
                    Name = "Staff",
                    NormalizedName = "Staff".ToUpper()
                }
            };

            builder.Entity<IdentityRole>().HasData(roles);
        }   

    }

}