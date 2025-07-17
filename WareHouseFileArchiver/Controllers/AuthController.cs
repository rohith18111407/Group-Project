using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WareHouseFileArchiver.Interfaces;
using WareHouseFileArchiver.Models.Domains;
using WareHouseFileArchiver.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using WareHouseFileArchiver.Repositories;

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
            try
            {
                var user = await userManager.FindByEmailAsync(loginRequestDto.Username);
                if (user == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Username or Password is incorrect",
                        data = (object?)null,
                        errors = new { Credentials = new[] { "Invalid username or password" } }
                    });
                }

                var checkPasswordResult = await userManager.CheckPasswordAsync(user, loginRequestDto.Password);
                if (!checkPasswordResult)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Username or Password is incorrect", 
                        data = (object?)null,
                        errors = new { Credentials = new[] { "Invalid username or password" } }
                    });
                }

                var roles = await userManager.GetRolesAsync(user);
                if (roles == null || !roles.Any())
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "User has no assigned roles",
                        data = (object?)null,
                        errors = new { Roles = new[] { "No roles assigned to user" } }
                    });
                }

                // Generate tokens
                var (jwtToken, jwtExpiry) = tokenRepository.CreateJWTToken(user, roles.ToList());
                var refreshToken = tokenRepository.GenerateRefreshToken();

                // CRITICAL: Update LastLoginAt and other login info
                var loginTime = DateTime.UtcNow;
                
                Console.WriteLine($"[LOGIN] Updating LastLoginAt for user {user.UserName} to {loginTime}");
                
                // Update user properties
                user.RefreshToken = refreshToken;
                user.RefreshTokenExpiryTime = loginTime.AddMinutes(15);
                user.LastLoginAt = loginTime;

                // Save changes to database
                var updateResult = await userManager.UpdateAsync(user);
                
                if (!updateResult.Succeeded)
                {
                    Console.WriteLine($"[LOGIN ERROR] Failed to update user: {string.Join(", ", updateResult.Errors.Select(e => e.Description))}");
                    
                    // If UserManager update fails, don't fail the entire login
                    // but log the error for debugging
                    foreach (var error in updateResult.Errors)
                    {
                        Console.WriteLine($"[LOGIN ERROR] {error.Code}: {error.Description}");
                    }
                }
                else
                {
                    Console.WriteLine($"[LOGIN SUCCESS] LastLoginAt updated successfully for user {user.UserName}");
                }

                // Verify the update worked by fetching fresh data
                var verifyUser = await userManager.FindByIdAsync(user.Id);
                Console.WriteLine($"[LOGIN VERIFY] Database LastLoginAt: {verifyUser.LastLoginAt}");

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
            catch (Exception ex)
            {
                Console.WriteLine($"[LOGIN EXCEPTION] {ex.Message}");
                Console.WriteLine($"[LOGIN EXCEPTION] Stack: {ex.StackTrace}");
                
                return StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred during login",
                    data = (object?)null,
                    errors = new { Exception = new[] { ex.Message } }
                });
            }
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
            try
            {
                Console.WriteLine("[LASTLOGIN] Fetching fresh user data from database");
                
                // Force a fresh database query with no caching
                var users = await userManager.Users
                    .AsNoTracking() // Disable Entity Framework caching
                    .OrderByDescending(u => u.LastLoginAt ?? DateTime.MinValue)
                    .Select(u => new
                    {
                        u.Id,
                        u.UserName,
                        u.Email,
                        u.LastLoginAt
                    })
                    .ToListAsync();

                Console.WriteLine($"[LASTLOGIN] Retrieved {users.Count} users from database");
                
                // Log the raw data for debugging
                foreach (var user in users.Take(3)) // Log first 3 users
                {
                    Console.WriteLine($"[LASTLOGIN] User: {user.UserName}, LastLoginAt: {user.LastLoginAt}");
                }

                // Enhanced users with formatted login information
                var enhancedUsers = users.Select(u => new
                {
                    u.Id,
                    u.UserName,
                    u.Email,
                    u.LastLoginAt,
                    LastLoginFormatted = UserRepository.LoginUtils.FormatLastLogin(u.LastLoginAt),
                    LoginStatus = UserRepository.LoginUtils.GetLoginStatus(u.LastLoginAt),
                    DaysSinceLastLogin = UserRepository.LoginUtils.GetDaysSinceLastLogin(u.LastLoginAt),
                }).ToList();

                Console.WriteLine("[LASTLOGIN] Enhanced user data prepared");

                return Ok(new
                {
                    success = true,
                    message = "Last login times retrieved",
                    data = enhancedUsers,
                    // Add debug info
                    debug = new
                    {
                        QueryTime = DateTime.UtcNow,
                        TotalUsers = users.Count
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LASTLOGIN ERROR] {ex.Message}");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Failed to retrieve last login information",
                    errors = new { Exception = new[] { ex.Message } }
                });
            }
        }

    }
}