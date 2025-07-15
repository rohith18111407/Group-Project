using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WareHouseFileArchiver.Interfaces;
using WareHouseFileArchiver.Models.DTOs;

namespace WareHouseFileArchiver.Controllers
{
    [ApiController]
    [Route("api/v1/users")]
    [Authorize(Roles = "Admin")]
    public class UserController : ControllerBase
    {
        private readonly IUserRepository userRepository;

        public UserController(IUserRepository userRepository)
        {
            this.userRepository = userRepository;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int pageNumber = 1,
                                                [FromQuery] int pageSize = 10,
                                                [FromQuery] string? role = null,
                                                [FromQuery] string? sortBy = "username",
                                                [FromQuery] string? sortOrder = "asc")
        {
            var validRoles = new[] { "Admin", "Staff" };
            if (role != null && !validRoles.Contains(role))
            {
                // return BadRequest("Invalid role filter. Only 'Admin' or 'Staff' are allowed.");
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid role filter. Only 'Admin' or 'Staff' are allowed.",
                    data = (object?)null,
                    errors = new { Role = new[] { "Allowed values are 'Admin' or 'Staff'" } }
                });
            }

            var users = await userRepository.GetAllAsync(pageNumber, pageSize, role, sortBy, sortOrder);
            // return Ok(users);

            return Ok(new
            {
                success = true,
                message = "Users fetched successfully",
                data = users,
                pagination = new
                {
                    page = pageNumber,
                    pageSize = pageSize,
                },
                errors = (object?)null
            });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById([FromRoute] string id)
        {
            var user = await userRepository.GetByIdAsync(id);
            if (user == null)
                // return NotFound();
                return NotFound(new
                {
                    success = false,
                    message = "User not found",
                    data = (object?)null,
                    errors = new { Id = new[] { "No user exists with the provided ID" } }
                });
            // return Ok(user);
            return Ok(new
            {
                success = true,
                message = "User fetched successfully",
                data = user,
                errors = (object?)null
            });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateUserDto dto)
        {
            var user = await userRepository.CreateAsync(dto);
            if (user == null)
                // return BadRequest("User creation failed. Only Admin/Staff roles allowed.");
                return BadRequest(new
                {
                    success = false,
                    message = "User creation failed. Only Admin/Staff roles allowed.",
                    data = (object?)null,
                    errors = new { Role = new[] { "Only Admin or Staff roles are allowed." } }
                });
            // return Ok(user);
            return Ok(new
            {
                success = true,
                message = "User created successfully",
                data = user,
                errors = (object?)null
            });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update([FromRoute] string id, [FromBody] UpdateUserDto dto)
        {
            var result = await userRepository.UpdateAsync(id, dto);
            if (result == null)
                // return BadRequest("Update failed or invalid roles.");
                return BadRequest(new
                {
                    success = false,
                    message = "User update failed. Invalid ID or roles.",
                    data = (object?)null,
                    errors = new { User = new[] { "Update failed due to invalid input or constraints." } }
                });
            // return Ok(result);
            return Ok(new
            {
                success = true,
                message = "User updated successfully",
                data = result,
                errors = (object?)null
            });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete([FromRoute] string id)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (currentUserId == id)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "You cannot delete your own account while logged in.",
                    data = (object?)null,
                    errors = new { Id = new[] { "Self-deletion is not allowed." } }
                });
            }

            var result = await userRepository.DeleteAsync(id);
            if (result == null)
                // return NotFound("User not found.");
                return NotFound(new
                {
                    success = false,
                    message = "User not found.",
                    data = (object?)null,
                    errors = new { Id = new[] { "No user found with the given ID." } }
                });
            // return Ok(result);
            return Ok(new
            {
                success = true,
                message = "User deleted successfully",
                data = result,
                errors = (object?)null
            });
        }
    }
}