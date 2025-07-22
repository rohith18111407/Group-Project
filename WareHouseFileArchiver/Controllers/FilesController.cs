using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using WareHouseFileArchiver.Interfaces;
using WareHouseFileArchiver.Models.Domains;
using WareHouseFileArchiver.Models.DTOs;
using WareHouseFileArchiver.SignalRHub;

namespace WareHouseFileArchiver.Controllers
{
    [ApiController]
    [Route("api/v1/files")]
    public class FilesController : ControllerBase
    {
        private readonly IArchiveFileRepository archiveFileRepository;
        private readonly IWebHostEnvironment webHostEnvironment;
        private readonly IHttpContextAccessor httpContextAccessor;
        private readonly UserManager<ApplicationUser> userManager;
        private readonly IHubContext<NotificationsHub> hubContext;

        public FilesController(IArchiveFileRepository archiveFileRepository,
                               IWebHostEnvironment webHostEnvironment,
                               IHttpContextAccessor httpContextAccessor,
                               UserManager<ApplicationUser> userManager,
                               IHubContext<NotificationsHub> hubContext)
        {
            this.archiveFileRepository = archiveFileRepository;
            this.webHostEnvironment = webHostEnvironment;
            this.httpContextAccessor = httpContextAccessor;
            this.userManager = userManager;
            this.hubContext = hubContext;
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("upload")]
        public async Task<IActionResult> Upload([FromForm] UploadArchiveFileRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Validation failed",
                    data = (object?)null,
                    errors = ModelState.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray())
                });
            }

            var itemExists = await archiveFileRepository.ItemExistsAsync(request.ItemId);
            if (!itemExists)
            {
                return BadRequest(new
                {
                    success = false,
                    message = $"Item with ID {request.ItemId} does not exist.",
                    data = (object?)null,
                    errors = new { ItemId = new[] { "Invalid ItemId." } }
                });
            }

            var item = await archiveFileRepository.GetItemByIdAsync(request.ItemId);

            // Get user who is uploading the file
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await userManager.FindByIdAsync(userId);
            var uploadedBy = user?.UserName ?? "Unknown";

            // Check for existing file with the same name and item
            var existingFile = await archiveFileRepository.GetLatestVersionAsync(
                Path.GetFileNameWithoutExtension(request.File.FileName),
                request.ItemId
            );

            int newVersion = existingFile != null ? existingFile.VersionNumber + 1 : 1;

            // Mark the existing file as updated
            if (existingFile != null)
            {
                existingFile.UpdatedAt = DateTime.UtcNow;
                existingFile.UpdatedBy = uploadedBy;
                await archiveFileRepository.UpdateAsync(existingFile);
            }

            var fileExtension = Path.GetExtension(request.File.FileName);
            var uniqueFileName = $"{Path.GetFileNameWithoutExtension(request.File.FileName)}_{DateTime.UtcNow.Ticks}{fileExtension}";
            var filePath = Path.Combine(webHostEnvironment.ContentRootPath, "ArchiveFiles", uniqueFileName);

            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            using var stream = new FileStream(filePath, FileMode.Create);
            await request.File.CopyToAsync(stream);

            var archiveFile = new ArchiveFile
            {
                Id = Guid.NewGuid(),
                ItemId = request.ItemId,
                FileName = Path.GetFileNameWithoutExtension(request.File.FileName),
                FileExtension = fileExtension,
                FileSizeInBytes = request.File.Length,
                FilePath = filePath,
                CreatedBy = uploadedBy,
                CreatedAt = DateTime.UtcNow,
                Description = request.Description,
                Category = request.Category,
                VersionNumber = newVersion
            };

            await archiveFileRepository.UploadAsync(archiveFile);

            // Notify clients
            await hubContext.Clients.All.SendAsync("ReceiveNotification", new
            {
                Action = existingFile != null ? "File Version Updated" : "New File Uploaded",
                archiveFile.Id,
                archiveFile.FileName,
                archiveFile.Category,
                archiveFile.FileExtension,
                archiveFile.FileSizeInBytes,
                archiveFile.FilePath,
                archiveFile.VersionNumber,
                archiveFile.ItemId,
                ItemName = item?.Name,
                ItemCategory = item?.Category,
                archiveFile.CreatedAt,
                archiveFile.CreatedBy,
                archiveFile.UpdatedAt,
                archiveFile.UpdatedBy
            });

            return Ok(new
            {
                success = true,
                message = existingFile != null ? "File version updated successfully" : "File uploaded successfully",
                data = new ArchiveFileResponseDto
                {
                    Id = archiveFile.Id,
                    FileName = archiveFile.FileName,
                    Category = archiveFile.Category,
                    FileExtension = archiveFile.FileExtension,
                    FileSizeInBytes = archiveFile.FileSizeInBytes,
                    FilePath = archiveFile.FilePath,
                    VersionNumber = archiveFile.VersionNumber,
                    Description = archiveFile.Description,
                    ItemId = request.ItemId,
                    ItemName = archiveFile.Item?.Name,
                    CreatedAt = archiveFile.CreatedAt,
                    CreatedBy = archiveFile.CreatedBy
                },
                errors = (object?)null
            });
        }

        [Authorize(Roles = "Admin,Staff")]
        [HttpGet("{filename}/v{version}")]
        public async Task<IActionResult> Download(string filename, int version)
        {
            var archiveFile = await archiveFileRepository.GetFileByNameAndVersionAsync(filename, version);

            if (archiveFile == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "File not found",
                    data = (object?)null,
                    errors = new { File = new[] { "No file with that name and version." } }
                });
            }

            // Check if file is archived - only admins can download archived files
            if (archiveFile.IsArchivedDueToInactivity && !User.IsInRole("Admin"))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "File is archived and cannot be downloaded",
                    data = (object?)null,
                    errors = new { File = new[] { "This file has been archived due to admin inactivity." } }
                });
            }

            if (!System.IO.File.Exists(archiveFile.FilePath))
            {
                return NotFound(new
                {
                    success = false,
                    message = "File not found on disk",
                    data = (object?)null,
                    errors = new { File = new[] { "Physical file not found." } }
                });
            }

            // Rest of the download logic remains the same...
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await userManager.FindByIdAsync(userId);
            var downloadedBy = user?.UserName ?? "Unknown";

            var log = new FileDownloadLog
            {
                Id = Guid.NewGuid(),
                ArchiveFileId = archiveFile.Id,
                DownloadedBy = downloadedBy,
                DownloadedAt = DateTime.UtcNow
            };

            await archiveFileRepository.LogDownloadAsync(log);

            await hubContext.Clients.All.SendAsync("ReceiveNotification", new
            {
                Action = archiveFile.IsArchivedDueToInactivity ? "Archived File Downloaded" : "File Downloaded",
                archiveFile.Id,
                archiveFile.FileName,
                archiveFile.Category,
                archiveFile.FileExtension,
                archiveFile.FileSizeInBytes,
                archiveFile.FilePath,
                archiveFile.VersionNumber,
                archiveFile.ItemId,
                ItemName = archiveFile.Item?.Name,
                ItemCategory = archiveFile.Item?.Category,
                archiveFile.CreatedAt,
                archiveFile.CreatedBy,
                archiveFile.UpdatedAt,
                archiveFile.UpdatedBy,
                IsArchived = archiveFile.IsArchivedDueToInactivity
            });

            var stream = new FileStream(archiveFile.FilePath, FileMode.Open, FileAccess.Read);
            return File(stream, "application/octet-stream", $"{archiveFile.FileName}{archiveFile.FileExtension}");
        }

        [Authorize(Roles = "Admin,Staff")]
        [HttpGet("by-item/{itemId}")]
        public async Task<IActionResult> GetByItemId(Guid itemId, [FromQuery] bool includeArchived = false)
        {
            // Only admins can view archived files
            if (includeArchived && !User.IsInRole("Admin"))
                includeArchived = false;

            var files = await archiveFileRepository.GetFilesByItemIdAsync(itemId, includeArchived);
            return Ok(new
            {
                success = true,
                message = "Files fetched successfully",
                data = files,
                errors = (object?)null
            });
        }

        [Authorize(Roles = "Admin,Staff")]
        [HttpGet]
        public async Task<IActionResult> GetAllFiles([FromQuery] bool includeArchived = false)
        {
            // Only admins can view archived files
            if (includeArchived && !User.IsInRole("Admin"))
                includeArchived = false;

            var files = await archiveFileRepository.GetAllFilesAsync(includeArchived);

            var response = files.Select(f => new AllFilesResponseDto
            {
                Id = f.Id,
                FileName = f.FileName,
                FileExtension = f.FileExtension,
                FileSizeInBytes = f.FileSizeInBytes,
                FilePath = f.FilePath,
                VersionNumber = f.VersionNumber,
                Description = f.Description,
                Category = f.Category,
                ItemId = f.ItemId,
                ItemName = f.Item?.Name,
                CreatedAt = f.CreatedAt,
                CreatedBy = f.CreatedBy,
                UpdatedAt = f.UpdatedAt,
                UpdatedBy = f.UpdatedBy
            });

            return Ok(new
            {
                success = true,
                message = "Files fetched successfully",
                data = response,
                errors = (object?)null
            });
        }


        [Authorize(Roles = "Admin")]
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            // Get the file from the DB
            var file = await archiveFileRepository.GetFileByIdAsync(id);

            if (file == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "File not found",
                    data = (object?)null,
                    errors = new { Id = new[] { "Invalid file ID." } }
                });
            }

            // Delete physical file if it exists
            if (System.IO.File.Exists(file.FilePath))
            {
                try
                {
                    System.IO.File.Delete(file.FilePath);
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new
                    {
                        success = false,
                        message = "Error deleting physical file",
                        data = (object?)null,
                        errors = new { File = new[] { ex.Message } }
                    });
                }
            }

            // Remove from DB
            await archiveFileRepository.DeleteAsync(file);

            return Ok(new
            {
                success = true,
                message = "File deleted successfully",
                data = new { file.Id, file.FileName, file.VersionNumber },
                errors = (object?)null
            });
        }


    }
}