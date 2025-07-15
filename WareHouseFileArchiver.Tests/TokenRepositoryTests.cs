using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using WareHouseFileArchiver.Models.Domains;
using WareHouseFileArchiver.Repositories;

namespace WareHouseFileArchiver.Tests
{
    public class TokenRepositoryTests
    {
        private TokenRepository _tokenRepository;
        private IConfiguration _configuration;
        private ApplicationUser _testUser;

        [SetUp]
        public void Setup()
        {
            var inMemorySettings = new Dictionary<string, string>
            {
                { "Jwt:Key", "ThisIsASecretKeyForJWTTokenTest12345" },
                { "Jwt:Issuer", "TestIssuer" },
                { "Jwt:Audience", "TestAudience" }
            };

            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings!)
                .Build();

            _tokenRepository = new TokenRepository(_configuration);

            _testUser = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                Email = "test@example.com",
                UserName = "test@example.com"
            };
        }

        [Test]
        public void GenerateRefreshToken_ShouldReturnBase64String()
        {
            var refreshToken = _tokenRepository.GenerateRefreshToken();

            Assert.That(refreshToken, Is.Not.Null);
            Assert.That(refreshToken, Is.Not.Empty);

            var buffer = Convert.FromBase64String(refreshToken);
            Assert.That(buffer.Length, Is.EqualTo(32));
        }

        [Test]
        public void CreateJWTToken_ShouldReturnValidToken()
        {
            var roles = new List<string> { "Admin", "Staff" };
            var token = _tokenRepository.CreateJWTToken(_testUser, roles);

            Assert.That(token, Is.Not.Null.And.Not.Empty);

            var handler = new JwtSecurityTokenHandler();
            Assert.That(handler.CanReadToken(token), Is.True);

            var jwtToken = handler.ReadJwtToken(token);
            Assert.That(jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value, Is.EqualTo(_testUser.Email));

            var roleClaims = jwtToken.Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .ToList();

            Assert.That(roleClaims, Does.Contain("Admin"));
            Assert.That(roleClaims, Does.Contain("Staff"));
        }


        [Test]
        public void GetPrincipalFromExpiredToken_ShouldReturnClaimsPrincipal()
        {
            var roles = new List<string> { "Admin" };
            var token = _tokenRepository.CreateJWTToken(_testUser, roles);

            var principal = _tokenRepository.GetPrincipalFromExpiredToken(token);

            Assert.That(principal, Is.Not.Null);
            Assert.That(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, Is.EqualTo(_testUser.Id));
            Assert.That(principal.FindFirst(ClaimTypes.Email)?.Value, Is.EqualTo(_testUser.Email));
        }

        [Test]
        public void GetPrincipalFromExpiredToken_ShouldReturnNull_IfInvalidToken()
        {
            var invalidToken = "this.is.an.invalid.token";
            var principal = _tokenRepository.GetPrincipalFromExpiredToken(invalidToken);

            Assert.That(principal, Is.Null);
        }
    }
}
