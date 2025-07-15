using Microsoft.AspNetCore.Identity;

namespace WareHouseFileArchiver.Models.Domains
{
    public class ApplicationUser : IdentityUser
    {
        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiryTime { get; set; }
    }
}