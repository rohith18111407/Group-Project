using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WareHouseFileArchiver.Interfaces;
using WareHouseFileArchiver.Models.Domains;
using WareHouseFileArchiver.Models.DTOs;

namespace WareHouseFileArchiver.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly UserManager<ApplicationUser> userManager;

        public UserRepository(UserManager<ApplicationUser> userManager)
        {
            this.userManager = userManager;
        }
        public async Task<UserDto?> CreateAsync(CreateUserDto dto)
        {
            if (dto.Roles.Any(r => r != "Admin" && r != "Staff"))
                return null;

            var user = new ApplicationUser
            {
                UserName = dto.Username,
                Email = dto.Username
            };

            var result = await userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
                return null;

            await userManager.AddToRolesAsync(user, dto.Roles);

            return new UserDto
            {
                Id = user.Id,
                Username = user.UserName,
                Email = user.Email,
                Roles = dto.Roles
            };
        }

        public async Task<UserDto?> DeleteAsync(string id)
        {
            var user = await userManager.FindByIdAsync(id);
            if (user == null)
                return null;

            var roles = await userManager.GetRolesAsync(user);

            var deletedUserDto = new UserDto
            {
                Id = id,
                Username = user.UserName,
                Email = user.Email,
                Roles = roles
            };

            var result = await userManager.DeleteAsync(user);
            return deletedUserDto;
        }

        public async Task<List<UserDto>> GetAllAsync(int pageNumber, int pageSize,
                                        string? roleFilter,
                                        string? sortBy, string? sortOrder)
        {
            List<ApplicationUser> users;

            if (!string.IsNullOrEmpty(roleFilter))
            {
                // Get users in role (in-memory list)
                users = (await userManager.GetUsersInRoleAsync(roleFilter)).ToList();

                // Apply sorting manually - Enhanced with lastlogin sorting
                users = (sortBy?.ToLower(), sortOrder?.ToLower()) switch
                {
                    ("username", "desc") => users.OrderByDescending(u => u.UserName).ToList(),
                    ("email", "asc") => users.OrderBy(u => u.Email).ToList(),
                    ("email", "desc") => users.OrderByDescending(u => u.Email).ToList(),
                    ("lastlogin", "asc") => users.OrderBy(u => u.LastLoginAt ?? DateTime.MinValue).ToList(),
                    ("lastlogin", "desc") => users.OrderByDescending(u => u.LastLoginAt ?? DateTime.MinValue).ToList(),
                    ("username", _) => users.OrderBy(u => u.UserName).ToList(),
                    _ => users.OrderBy(u => u.UserName).ToList()
                };

                // Apply pagination manually
                users = users.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
            }
            else
            {
                var usersQuery = userManager.Users.AsQueryable();

                // Sorting with IQueryable for EF support - Enhanced with lastlogin sorting
                usersQuery = (sortBy?.ToLower(), sortOrder?.ToLower()) switch
                {
                    ("username", "desc") => usersQuery.OrderByDescending(u => u.UserName),
                    ("email", "asc") => usersQuery.OrderBy(u => u.Email),
                    ("email", "desc") => usersQuery.OrderByDescending(u => u.Email),
                    ("lastlogin", "asc") => usersQuery.OrderBy(u => u.LastLoginAt ?? DateTime.MinValue),
                    ("lastlogin", "desc") => usersQuery.OrderByDescending(u => u.LastLoginAt ?? DateTime.MinValue),
                    ("username", _) => usersQuery.OrderBy(u => u.UserName),
                    _ => usersQuery.OrderBy(u => u.UserName)
                };

                users = await usersQuery
                            .Skip((pageNumber - 1) * pageSize)
                            .Take(pageSize)
                            .ToListAsync();
            }

            // Convert to DTOs - Enhanced with login information
            var userDtos = new List<UserDto>();
            foreach (var user in users)
            {
                var roles = await userManager.GetRolesAsync(user);
                userDtos.Add(new UserDto
                {
                    Id = user.Id,
                    Username = user.UserName,
                    Email = user.Email,
                    Roles = roles,
                    LastLoginAt = user.LastLoginAt,
                    LastLoginFormatted = LoginUtils.FormatLastLogin(user.LastLoginAt),
                    LoginStatus = LoginUtils.GetLoginStatus(user.LastLoginAt),
                    DaysSinceLastLogin = LoginUtils.GetDaysSinceLastLogin(user.LastLoginAt)
                });
            }

            return userDtos;
        }

        public async Task<UserDto?> GetByIdAsync(string id)
        {
            var user = await userManager.FindByIdAsync(id);
            if (user == null)
                return null;

            var roles = await userManager.GetRolesAsync(user);

            return new UserDto
            {
                Id = user.Id,
                Username = user.UserName,
                Email = user.Email,
                Roles = roles,
                LastLoginAt = user.LastLoginAt,
                LastLoginFormatted = LoginUtils.FormatLastLogin(user.LastLoginAt),
                LoginStatus = LoginUtils.GetLoginStatus(user.LastLoginAt),
                DaysSinceLastLogin = LoginUtils.GetDaysSinceLastLogin(user.LastLoginAt)
            };
        }

        public async Task<UserDto?> UpdateAsync(string id, UpdateUserDto dto)
        {
            var user = await userManager.FindByIdAsync(id);
            if (user == null)
                return null;
            if (dto.Roles.Any(r => r != "Admin" && r != "Staff"))
                return null;

            user.Email = dto.Username;
            user.UserName = dto.Username;
            var updateResult = await userManager.UpdateAsync(user);

            if (!updateResult.Succeeded)
                return null;


            var currentRoles = await userManager.GetRolesAsync(user);
            await userManager.RemoveFromRolesAsync(user, currentRoles);

            await userManager.AddToRolesAsync(user, dto.Roles);

            return new UserDto
            {
                Id = id,
                Username = dto.Username,
                Email = dto.Username,
                Roles = dto.Roles
            };
        }

        public static class LoginUtils
        //Utility service for formatting and getting login information
        {
            public static string FormatLastLogin(DateTime? lastLoginAt)
            {
                if (lastLoginAt == null) return "Never logged in";
                
                // If the datetime appears to be UTC (based on your backend showing +05:30), 
                // convert it to IST for display
                DateTime displayTime;
                
                if (lastLoginAt.Value.Kind == DateTimeKind.Utc || 
                    // Check if this looks like UTC time (5.5 hours behind expected IST)
                    lastLoginAt.Value.Hour < 12 && DateTime.UtcNow.Hour > 12)
                {
                    // Convert UTC to IST (+5:30)
                    var istZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
                    displayTime = TimeZoneInfo.ConvertTimeFromUtc(lastLoginAt.Value, istZone);
                }
                else
                {
                    // Already in IST or local time
                    displayTime = lastLoginAt.Value;
                }
                
                return displayTime.ToString("yyyy-MM-dd HH:mm:ss") + " IST";
            }

            public static string GetLoginStatus(DateTime? lastLoginAt)
            {
                if (lastLoginAt == null) return "Never logged in";

                var daysSince = (DateTime.UtcNow - lastLoginAt.Value).Days;
                

                return daysSince switch
                {
                    0 => "Active today",
                    1 => "Active yesterday",
                    <= 7 => "Active this week",
                    <= 30 => "Active this month",
                    _ => "Inactive"
                };
            }
            
            public static int? GetDaysSinceLastLogin(DateTime? lastLoginAt)
            {
                if (lastLoginAt == null) return null;
                return (DateTime.UtcNow - lastLoginAt.Value).Days;
            }
            
        }

        
    }
}