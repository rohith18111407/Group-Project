using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WareHouseFileArchiver.Interfaces;
using WareHouseFileArchiver.Models.Domains;
using WareHouseFileArchiver.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace WareHouseFileArchiver.Controllers
{
    [ApiController]
    [Route("api/v1/auth")]
    public class AuthController : ControllerBase
    {
        private readonly ITokenRepository tokenRepository;
        private readonly UserManager<ApplicationUser> userManager;
        private readonly SignInManager<ApplicationUser> signInManager;


        public AuthController(UserManager<ApplicationUser> userManager,
                      ITokenRepository tokenRepository,
                      SignInManager<ApplicationUser> signInManager)
        {
            this.userManager = userManager;
            this.tokenRepository = tokenRepository;
            this.signInManager = signInManager;
        }

        // POST: /api/v1/auth/register
        [HttpPost]
        [Route("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDto registerRequestDto)
        {
            var validRoles = new[] { "Admin", "Staff" };

            // Check for invalid roles
            if (registerRequestDto.Roles != null && registerRequestDto.Roles.Any(r => !validRoles.Contains(r, StringComparer.OrdinalIgnoreCase)))
            {
                // return BadRequest("Invalid role. Only 'Admin' or 'Staff' are allowed.");

                return BadRequest(new
                {
                    success = false,
                    message = "Validation failed",
                    data = (object?)null,
                    errors = new { Roles = new[] { "Invalid role. Only 'Admin' or 'Staff' are allowed." } }
                });
            }

            //  Check if user already exists
            var existingUser = await userManager.FindByEmailAsync(registerRequestDto.Username);
            if (existingUser != null)
            {
                return Conflict(new
                {
                    success = false,
                    message = "User already exists",
                    data = (object?)null,
                    errors = new { Username = new[] { "This email/username is already registered." } }
                });
            }

            var identityUser = new ApplicationUser
            {
                UserName = registerRequestDto.Username,
                Email = registerRequestDto.Username
            };
            var identityResult = await userManager.CreateAsync(identityUser, registerRequestDto.Password);

            if (identityResult.Succeeded)
            {
                // Add roles to this User
                if (registerRequestDto.Roles != null && registerRequestDto.Roles.Any())
                {
                    identityResult = await userManager.AddToRolesAsync(identityUser, registerRequestDto.Roles);
                    if (identityResult.Succeeded)
                    {
                        // return Ok("User was registered! Please login.");
                        return Ok(new
                        {
                            success = true,
                            message = "User was registered! Please login.",
                            data = new { },
                            errors = (object?)null
                        });
                    }
                }
            }
            return BadRequest("Something went wrong");
        }

        // POST: /api/v1/auth/login
        // [HttpPost("login")]
        // public async Task<IActionResult> Login([FromBody] LoginRequestDto loginRequestDto)
        // {
        //     var user = await userManager.FindByEmailAsync(loginRequestDto.Username);
        //     if (user != null)
        //     {
        //         var checkPasswordResult = await userManager.CheckPasswordAsync(user, loginRequestDto.Password);
        //         if (checkPasswordResult)
        //         {
        //             var roles = await userManager.GetRolesAsync(user);
        //             if (roles != null)
        //             {
        //                 var jwtToken = tokenRepository.CreateJWTToken(user, roles.ToList());
        //                 var refreshToken = tokenRepository.GenerateRefreshToken();

        //                 user.RefreshToken = refreshToken;
        //                 user.RefreshTokenExpiryTime = DateTime.UtcNow.AddMinutes(15);
        //                 await userManager.UpdateAsync(user);

        //                 var response = new LoginResponseDto
        //                 {
        //                     JwtToken = jwtToken,
        //                     RefreshToken = refreshToken
        //                 };

        //                 // return Ok(response);
        //                 return Ok(new
        //                 {
        //                     success = true,
        //                     message = "Login successful",
        //                     data = response,
        //                     errors = (object?)null
        //                 });
        //             }
        //         }
        //     }
        //     return BadRequest("Username or Password is incorrect");
        // }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto loginRequestDto)
        {
            var user = await userManager.FindByEmailAsync(loginRequestDto.Username);
            if (user != null)
            {
                var checkPasswordResult = await userManager.CheckPasswordAsync(user, loginRequestDto.Password);
                if (checkPasswordResult)
                {
                    var roles = await userManager.GetRolesAsync(user);
                    if (roles != null)
                    {
                        var (jwtToken, jwtExpiry) = tokenRepository.CreateJWTToken(user, roles.ToList());
                        var refreshToken = tokenRepository.GenerateRefreshToken();

                        user.RefreshToken = refreshToken;
                        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddMinutes(15);
                        user.LastLoginAt = DateTime.UtcNow;
                        await userManager.UpdateAsync(user);

                        var response = new LoginResponseDto
                        {
                            JwtToken = jwtToken,
                            RefreshToken = refreshToken,
                            Role = roles.FirstOrDefault() ?? "Unknown",
                            JwtExpiryTime = jwtExpiry
                        };

                        return Ok(new
                        {
                            success = true,
                            message = "Login successful",
                            data = response,
                            errors = (object?)null
                        });
                    }
                }
            }

            return BadRequest("Username or Password is incorrect");
        }


        // POST: /api/v1/auth/logout
        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await userManager.FindByIdAsync(userId);

            if (user != null)
            {
                user.RefreshToken = null;
                user.RefreshTokenExpiryTime = DateTime.UtcNow;
                await userManager.UpdateAsync(user);
            }

            // return Ok("Refresh Token is successfully cleared, can't re-authenticate again. JWT Access token becomes useless after its short expiration.");
            return Ok(new
            {
                success = true,
                message = "Successfully logged out. Refresh token cleared.",
                data = (object?)null,
                errors = (object?)null
            });
        }

        // POST: /api/v1/auth/refresh
        // [HttpPost("refresh")]
        // public async Task<IActionResult> RefreshToken([FromBody] TokenRequestDto tokenRequest)
        // {
        //     if (tokenRequest is null)
        //         return BadRequest("Invalid client request");

        //     var principal = tokenRepository.GetPrincipalFromExpiredToken(tokenRequest.AccessToken);
        //     if (principal == null)
        //         // return BadRequest("Invalid access token");
        //         return BadRequest(new
        //         {
        //             success = false,
        //             message = "Invalid access token",
        //             data = (object?)null,
        //             errors = new { Token = new[] { "Could not extract principal" } }
        //         });

        //     var email = principal.FindFirstValue(ClaimTypes.Email);
        //     var user = await userManager.FindByEmailAsync(email);

        //     if (user == null ||
        //         user.RefreshToken != tokenRequest.RefreshToken ||
        //         user.RefreshTokenExpiryTime <= DateTime.UtcNow)
        //     {
        //         // return BadRequest("Invalid refresh token");
        //         return BadRequest(new
        //         {
        //             success = false,
        //             message = "Invalid or expired refresh token",
        //             data = (object?)null,
        //             errors = new { Token = new[] { "Invalid or expired refresh token" } }
        //         });
        //     }

        //     var roles = await userManager.GetRolesAsync(user);
        //     var newAccessToken = tokenRepository.CreateJWTToken(user, roles.ToList());
        //     var newRefreshToken = tokenRepository.GenerateRefreshToken();

        //     user.RefreshToken = newRefreshToken;
        //     user.RefreshTokenExpiryTime = DateTime.UtcNow.AddMinutes(4); // Refresh token lifespan
        //     await userManager.UpdateAsync(user);

        //     return Ok(new
        //     {
        //         success = true,
        //         message = "Token refreshed successfully",
        //         data = new LoginResponseDto
        //         {
        //             JwtToken = newAccessToken,
        //             RefreshToken = newRefreshToken
        //         },
        //         errors = (object?)null
        //     });
        // }

        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshToken([FromBody] TokenRequestDto tokenRequest)
        {
            if (tokenRequest is null)
                return BadRequest("Invalid client request");

            var principal = tokenRepository.GetPrincipalFromExpiredToken(tokenRequest.AccessToken);
            if (principal == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid access token",
                    data = (object?)null,
                    errors = new { Token = new[] { "Could not extract principal" } }
                });
            }

            var email = principal.FindFirstValue(ClaimTypes.Email);
            var user = await userManager.FindByEmailAsync(email);

            if (user == null ||
                user.RefreshToken != tokenRequest.RefreshToken ||
                user.RefreshTokenExpiryTime <= DateTime.UtcNow)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid or expired refresh token",
                    data = (object?)null,
                    errors = new { Token = new[] { "Invalid or expired refresh token" } }
                });
            }

            var roles = await userManager.GetRolesAsync(user);
            var (newAccessToken, newExpiry) = tokenRepository.CreateJWTToken(user, roles.ToList());
            var newRefreshToken = tokenRepository.GenerateRefreshToken();

            user.RefreshToken = newRefreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddMinutes(4);
            await userManager.UpdateAsync(user);

            return Ok(new
            {
                success = true,
                message = "Token refreshed successfully",
                data = new LoginResponseDto
                {
                    JwtToken = newAccessToken,
                    RefreshToken = newRefreshToken,
                    Role = roles.FirstOrDefault() ?? "Unknown",
                    JwtExpiryTime = newExpiry
                },
                errors = (object?)null
            });
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            Console.WriteLine("Me endpoint hit!");
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await userManager.FindByIdAsync(userId);
            if (user == null)
            {
                // return NotFound();
                return NotFound(new
                {
                    success = false,
                    message = "User not found",
                    data = (object?)null,
                    errors = new { User = new[] { "User not found" } }
                });
            }
            var roles = await userManager.GetRolesAsync(user);

            return Ok(new
            {
                success = true,
                message = "User fetched successfully",
                data = new
                {
                    user.Id,
                    user.UserName,
                    user.Email,
                    Roles = roles
                },
                errors = (object?)null
            });
        }

        [Authorize(Roles = "Admin,Staff")]
        [HttpGet("LastLogin")]
        public async Task<IActionResult> GetUserLastLogin()
        {
            var users = await userManager.Users
                .Select(u => new
                {
                    u.Id,
                    u.UserName,
                    u.Email,
                    u.LastLoginAt
                })
                .ToListAsync();

            return Ok(new
            {
                success = true,
                message = "Last login times retrieved",
                data = users
            });
        }

    }
}