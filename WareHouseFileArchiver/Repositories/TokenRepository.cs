using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using WareHouseFileArchiver.Interfaces;
using WareHouseFileArchiver.Models.Domains;

namespace WareHouseFileArchiver.Repositories
{
    public class TokenRepository : ITokenRepository
    {
        private readonly IConfiguration configuration;

        public TokenRepository(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        // public string CreateJWTToken(ApplicationUser user, IList<string> roles)
        // {
        //     // Create Claims
        //     var claims = new List<Claim>();
        //     claims.Add(new Claim(ClaimTypes.NameIdentifier, user.Id));
        //     claims.Add(new Claim(ClaimTypes.Email, user.Email));

        //     foreach (var role in roles)
        //     {
        //         claims.Add(new Claim(ClaimTypes.Role, role));
        //     }

        //     var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]));
        //     var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        //     var token = new JwtSecurityToken(
        //         configuration["Jwt:Issuer"],
        //         configuration["Jwt:Audience"],
        //         claims,
        //         expires: DateTime.Now.AddMinutes(2),
        //         signingCredentials: credentials
        //     );

        //     return new JwtSecurityTokenHandler().WriteToken(token);
        // }

        public (string Token, DateTime Expiry) CreateJWTToken(ApplicationUser user, IList<string> roles)
        {
            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, user.Id),
        new Claim(ClaimTypes.Email, user.Email)
    };

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var expires = DateTime.UtcNow.AddMinutes(20); // JWT expiry

            var token = new JwtSecurityToken(
                configuration["Jwt:Issuer"],
                configuration["Jwt:Audience"],
                claims,
                expires: expires,
                signingCredentials: credentials
            );

            return (new JwtSecurityTokenHandler().WriteToken(token), expires);
        }



        public string GenerateRefreshToken()
        {
            var randomNumber = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
        {
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = true,
                ValidateIssuer = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = false,
                ValidIssuer = configuration["Jwt:Issuer"],
                ValidAudience = configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]))
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            try
            {
                var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out var securityToken);
                if (securityToken is not JwtSecurityToken jwtSecurityToken ||
                    !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                {
                    throw new SecurityTokenException("Invalid token");
                }

                return principal;
            }
            catch
            {
                return null;
            }
        }
    }
}