using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using WareHouseFileArchiver.Models.Domains;

namespace WareHouseFileArchiver.Interfaces
{
    public interface ITokenRepository
    {
        // string CreateJWTToken(ApplicationUser user, IList<string> roles);

        (string Token, DateTime Expiry) CreateJWTToken(ApplicationUser user, IList<string> roles);

        string GenerateRefreshToken();
        ClaimsPrincipal GetPrincipalFromExpiredToken(string token);
    }
}