using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WareHouseFileArchiver.Interfaces;
using WareHouseFileArchiver.Models.Domains;
using WareHouseFileArchiver.Models.DTOs;

namespace WareHouseFileArchiver.Controllers
{
    [ApiController]
    [Route("api/v1/items")]
    public class ItemsController : ControllerBase
    {
        private readonly IItemRepository itemRepository;

        public ItemsController(IItemRepository itemRepository)
        {
            this.itemRepository = itemRepository;
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] CreateItemRequestDto createDto)
        {
            var createdBy = User.FindFirstValue(ClaimTypes.Email) ?? "system";
            var now = DateTime.UtcNow;
            var createdItems = new List<ItemDto>();

            foreach (var categoryStr in createDto.Categories)
            {
                if (string.IsNullOrWhiteSpace(categoryStr))
                    return BadRequest(new
                    {
                        success = false,
                        message = $"Category cannot be empty.",
                        data = (object?)null,
                        errors = new { Categories = new[] { $"Category cannot be empty." } }
                    });

                var exists = await itemRepository.ExistsAsync(createDto.Name, categoryStr);
                if (exists)
                    continue;

                var item = new Item
                {
                    Id = Guid.NewGuid(),
                    Name = createDto.Name,
                    Description = createDto.Description,
                    Category = categoryStr.Trim(),
                    CreatedAt = now,
                    CreatedBy = createdBy
                };

                await itemRepository.CreateAsync(item);

                createdItems.Add(new ItemDto
                {
                    Id = item.Id,
                    Name = item.Name,
                    Description = item.Description,
                    Categories = new List<string> { item.Category },
                    CreatedAt = item.CreatedAt,
                    CreatedBy = item.CreatedBy
                });
            }

            if (!createdItems.Any())
                return Conflict(new
                {
                    success = false,
                    message = "All provided item(s) already exist.",
                    data = (object?)null,
                    errors = new { Name = new[] { "Item(s) already exist with the same name and category." } }
                });

            return Created("", new
            {
                success = true,
                message = "Item(s) created successfully",
                data = createdItems,
                errors = (object?)null
            });
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateItemRequestDto updateDto)
        {
            var item = await itemRepository.GetByIdAsync(id);
            if (item == null)
                return NotFound(new
                {
                    success = false,
                    message = "Item not found",
                    data = (object?)null,
                    errors = (object?)null
                });

            if (updateDto.Categories == null || updateDto.Categories.Count != 1)
                return BadRequest(new
                {
                    success = false,
                    message = "Update requires exactly one category.",
                    data = (object?)null,
                    errors = new { Categories = new[] { "Exactly one category is required." } }
                });

            var categoryStr = updateDto.Categories[0];

            if (string.IsNullOrWhiteSpace(categoryStr))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Category cannot be empty.",
                    data = (object?)null,
                    errors = new { Categories = new[] { "Exactly one valid category is required." } }
                });
            }

            if (!item.Name.Equals(updateDto.Name, StringComparison.OrdinalIgnoreCase) ||
                !item.Category.Equals(categoryStr.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                var exists = await itemRepository.ExistsAsync(updateDto.Name, categoryStr);
                if (exists)
                {
                    return Conflict(new
                    {
                        success = false,
                        message = "Another item with the same name and category already exists.",
                        data = (object?)null,
                        errors = new { Name = new[] { "Duplicate item found." } }
                    });
                }
            }

            item.Name = updateDto.Name;
            item.Description = updateDto.Description;
            item.Category = categoryStr.Trim();
            item.UpdatedAt = DateTime.UtcNow;
            item.UpdatedBy = User.Identity?.Name ?? "system";


            await itemRepository.UpdateAsync(item);

            return Ok(new
            {
                success = true,
                message = "Item updated successfully",
                data = new ItemDto
                {
                    Id = item.Id,
                    Name = item.Name,
                    Description = item.Description,
                    Categories = new List<string> { item.Category.ToString() },
                    CreatedAt = item.CreatedAt,
                    CreatedBy = item.CreatedBy,
                    UpdatedAt = item.UpdatedAt,
                    UpdatedBy = item.UpdatedBy
                },
                errors = (object?)null
            });
        }


        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete([FromRoute] Guid id)
        {
            var item = await itemRepository.GetByIdAsync(id);
            if (item == null) 
                return NotFound(new
                {
                    success = false,
                    message = "Item not found",
                    data = (object?)null,
                    errors = (object?)null
                });

            await itemRepository.DeleteAsync(item);

            return Ok(new
            {
                success = true,
                message = "Item deleted successfully",
                data = new ItemDto
                {
                    Id = item.Id,
                    Name = item.Name,
                    Description = item.Description,
                    Categories = new List<string> { item.Category },
                    CreatedAt = item.CreatedAt,
                    CreatedBy = item.CreatedBy,
                    UpdatedAt = item.UpdatedAt,
                    UpdatedBy = item.UpdatedBy
                },
                errors = (object?)null
            });
        }

        [Authorize(Roles = "Admin,Staff")]
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById([FromRoute] Guid id)
        {
            var item = await itemRepository.GetByIdAsync(id);
            if (item == null) 
                return NotFound(new {
                    success = false,
                    message = "Item not found",
                    data = (object?)null,
                    errors = (object?)null });

                return Ok(new
                {
                    success = true,
                    message = "Item fetched successfully",
                    data = new ItemDto
                    {
                        Id = item.Id,
                        Name = item.Name,
                        Description = item.Description,
                        Categories = new List<string> { item.Category },
                        CreatedAt = item.CreatedAt,
                        CreatedBy = item.CreatedBy,
                        UpdatedAt = item.UpdatedAt,
                        UpdatedBy = item.UpdatedBy
                    },
                    errors = (object?)null
                });
        }

        [Authorize(Roles = "Admin,Staff")]
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? sortBy,
            [FromQuery] bool isDescending = false,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var items = await itemRepository.GetAllAsync(sortBy, isDescending, pageNumber, pageSize);
            var totalCount = await itemRepository.GetTotalCountAsync();

            return Ok(new
            {
                success = true,
                message = "Items fetched successfully",
                data = items.Select(i => new ItemDto
                {
                    Id = i.Id,
                    Name = i.Name,
                    Description = i.Description,
                    Categories = new List<string> { i.Category },
                    CreatedAt = i.CreatedAt,
                    CreatedBy = i.CreatedBy,
                    UpdatedAt = i.UpdatedAt,
                    UpdatedBy = i.UpdatedBy
                }).ToList(),
                pagination = new
                {
                    totalRecords = totalCount,
                    page = pageNumber,
                    pageSize = pageSize
                },
                errors = (object?)null
            });
        }
    }
}