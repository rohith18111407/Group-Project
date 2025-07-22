using System.IO.Compression;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using WareHouseFileArchiver.Interfaces;
using WareHouseFileArchiver.Models.Domains;
using WareHouseFileArchiver.Models.DTOs;
using WareHouseFileArchiver.Services;
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
        private readonly IFileManagementService fileManagementService;

        public FilesController(IArchiveFileRepository archiveFileRepository,
                               IWebHostEnvironment webHostEnvironment,
                               IHttpContextAccessor httpContextAccessor,
                               UserManager<ApplicationUser> userManager,
                               IHubContext<NotificationsHub> hubContext,
                               IFileManagementService fileManagementService)
        {
            this.archiveFileRepository = archiveFileRepository;
            this.webHostEnvironment = webHostEnvironment;
            this.httpContextAccessor = httpContextAccessor;
            this.userManager = userManager;
            this.hubContext = hubContext;
            this.fileManagementService = fileManagementService;
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

            // Check if this is a scheduled upload
            if (request.ScheduledUploadDate.HasValue && request.ScheduledUploadDate.Value > DateTime.UtcNow)
            {
                return await HandleScheduledUpload(request);
            }

            // Handle immediate upload (existing logic)
            return await HandleImmediateUpload(request);
        }

        private async Task<IActionResult> HandleScheduledUpload(UploadArchiveFileRequestDto request)
        {
            try
            {
                var item = await archiveFileRepository.GetItemByIdAsync(request.ItemId);
                
                // Get the user who uploaded the file
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

                // Store file temporarily for scheduled upload
                var fileExtension = Path.GetExtension(request.File.FileName);
                var tempFileName =  $"{Path.GetFileNameWithoutExtension(request.File.FileName)}_{DateTime.UtcNow.Ticks}{fileExtension}";
                var filePath = Path.Combine(webHostEnvironment.ContentRootPath, "TempFiles", tempFileName);

                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                using var stream = new FileStream(filePath, FileMode.Create);
                await request.File.CopyToAsync(stream);

                // Create scheduled archive file record
                var archiveFile = new ArchiveFile
                {
                    Id = Guid.NewGuid(),
                    ItemId = request.ItemId,
                    FileName = Path.GetFileNameWithoutExtension(request.File.FileName),
                    FileExtension = fileExtension,
                    FileSizeInBytes = request.File.Length,
                    FilePath = filePath, // Temporary path
                    CreatedBy = uploadedBy,
                    CreatedAt = request.ScheduledUploadDate.Value, // Use scheduled time
                    Description = request.Description,
                    Category = request.Category,
                    VersionNumber = newVersion,
                    IsScheduled = true,
                    IsProcessed = false
                };

                await archiveFileRepository.UploadAsync(archiveFile);

                // Notify clients about the scheduled upload
                await hubContext.Clients.All.SendAsync("ReceiveNotification", new
                {
                    Action = existingFile != null ? "File Version Updated" : "New File Scheduled",
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
                    archiveFile.UpdatedBy,
                    archiveFile.IsScheduled,
                    archiveFile.IsProcessed
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
                        CreatedBy = archiveFile.CreatedBy,
                        IsScheduled = archiveFile.IsScheduled,
                        IsProcessed = archiveFile.IsProcessed
                    },
                    errors = (object?)null
                });
            } catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred while scheduling the file upload.",
                    data = (object?)null,
                    errors = (object?)null
                });
            }            
        }

        private async Task<IActionResult> HandleImmediateUpload(UploadArchiveFileRequestDto request)
        {
            try
            {
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
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred while processing the upload",
                    data = (object?)null,
                    errors = new { File = new[] { ex.Message } }
                });
            }
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

            //  Log download without modifying ArchiveFile
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
                Action = "File Downloaded",
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
                archiveFile.UpdatedBy
            });


            var stream = new FileStream(archiveFile.FilePath, FileMode.Open, FileAccess.Read);
            return File(stream, "application/octet-stream", $"{archiveFile.FileName}{archiveFile.FileExtension}");
        }

        [Authorize(Roles = "Admin,Staff")]
        [HttpPost("bulk-download")]
        public async Task<IActionResult> BulkDownload([FromBody] BulkDownloadRequestDto request)
        {
            if (request.FileIds == null || !request.FileIds.Any())
            {
                return BadRequest(new
                {
                    success = false,
                    message = "No files selected for download",
                    data = (object?)null,
                    errors = new { FileIds = new[] { "At least one file must be selected." } }
                });
            }

            try
            {
                var files = new List<ArchiveFile>();
                var missingFiles = new List<Guid>();

                // Fetch all files by IDs
                foreach (var fileId in request.FileIds)
                {
                    var file = await archiveFileRepository.GetFileByIdAsync(fileId);
                    if (file == null || !System.IO.File.Exists(file.FilePath))
                    {
                        missingFiles.Add(fileId);
                    }
                    else
                    {
                        files.Add(file);
                    }
                }

                if (missingFiles.Any())
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Some files could not be found",
                        data = (object?)null,
                        errors = new { MissingFiles = missingFiles.ToArray() }
                    });
                }

                if (!files.Any())
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "No valid files found for download",
                        data = (object?)null,
                        errors = (object?)null
                    });
                }

                // Get user info for logging
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var user = await userManager.FindByIdAsync(userId);
                var downloadedBy = user?.UserName ?? "Unknown";

                // Create a zip file for bulk download
                using var memoryStream = new MemoryStream();
                using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
                {
                    var fileNameCounts = new Dictionary<string, int>();

                    foreach (var file in files)
                    {
                        try
                        {
                            // Handle duplicate file names by adding a counter
                            var fileName = $"{file.FileName}{file.FileExtension}";
                            if (fileNameCounts.ContainsKey(fileName))
                            {
                                fileNameCounts[fileName]++;
                                var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                                var extension = Path.GetExtension(fileName);
                                fileName = $"{nameWithoutExt}_({fileNameCounts[fileName]}){extension}";
                            }
                            else
                            {
                                fileNameCounts[fileName] = 0;
                            }

                            // Create entry in ZIP
                            var zipEntry = archive.CreateEntry(fileName, CompressionLevel.Optimal);

                            using var zipStream = zipEntry.Open();
                            using var fileStream = new FileStream(file.FilePath, FileMode.Open, FileAccess.Read);
                            await fileStream.CopyToAsync(zipStream);

                            // Log individual file download
                            var log = new FileDownloadLog
                            {
                                Id = Guid.NewGuid(),
                                ArchiveFileId = file.Id,
                                DownloadedBy = downloadedBy,
                                DownloadedAt = DateTime.UtcNow
                            };
                            await archiveFileRepository.LogDownloadAsync(log);
                        }
                        catch (Exception ex)
                        {
                            // Log error but continue with other files
                            Console.WriteLine($"Error adding file {file.FileName} to ZIP: {ex.Message}");
                        }
                    }
                }

                // Generate a zip file name
                var zipFileName = string.IsNullOrEmpty(request.ZipFileName)
                    ? $"ArchiveFiles_{DateTime.UtcNow:yyyyMMdd_HHmmss}.zip"
                    : $"{request.ZipFileName}.zip";
                
                // Send notification about bulk download
                await hubContext.Clients.All.SendAsync("ReceiveNotification", new
                {
                    Action = "Bulk Files Downloaded",
                    FileCount = files.Count,
                    ZipFileName = zipFileName,
                    DownloadedBy = downloadedBy,
                    DownloadedAt = DateTime.UtcNow,
                    Files = files.Select(f => new { f.Id, f.FileName, f.FileExtension }).ToArray()
                });

                // Return ZIP file
                memoryStream.Position = 0;
                return File(memoryStream.ToArray(), "application/zip", zipFileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred while processing the bulk download",
                    data = (object?)null,
                    errors = new { File = new[] { ex.Message } }
                });
            }
        }

        [Authorize(Roles = "Admin,Staff")]
        [HttpGet("by-item/{itemId}")]
        public async Task<IActionResult> GetByItemId(Guid itemId)
        {
            var files = await archiveFileRepository.GetFilesByItemIdAsync(itemId);
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
        public async Task<IActionResult> GetAllFiles()
        {
            var files = await archiveFileRepository.GetAllFilesAsync();

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

            // Get current user for auditing
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await userManager.FindByIdAsync(userId);
            var deletedBy = user?.UserName ?? "Unknown";

            try
            {
                // Move file to trash folder
                var trashFilePath = await fileManagementService.MoveFileToTrashAsync(file);
                
                // Mark as deleted in database
                await archiveFileRepository.MoveToTrashAsync(file, deletedBy, trashFilePath);

                return Ok(new
                {
                    success = true,
                    message = "File moved to trash successfully",
                    data = new { file.Id, file.FileName, file.VersionNumber },
                    errors = (object?)null
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error moving file to trash",
                    data = (object?)null,
                    errors = new { File = new[] { ex.Message } }
                });
            }
        }


        // Scheduled File Upload Endpoints
        [Authorize(Roles = "Admin")]
        [HttpGet("scheduled")]
        public async Task<IActionResult> GetScheduledFiles()
        {
            try
            {
                var scheduledFiles = await archiveFileRepository.GetScheduledFilesAsync();

                var response = scheduledFiles.Select(f => new AllFilesResponseDto
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
                    UpdatedBy = f.UpdatedBy,
                    IsScheduled = f.IsScheduled,
                    IsProcessed = f.IsProcessed,
                }).ToList();

                return Ok(new
                {
                    success = true,
                    message = "Scheduled files retrieved successfully.",
                    data = response,
                    errors = (object?)null
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred while retrieving scheduled files.",
                    data = (object?)null,
                    errors = (object?)null
                });
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("scheduled/{id}")]
        public async Task<IActionResult> CancelScheduledUpload(Guid id)
        {
            try
            {
                var scheduledFile = await archiveFileRepository.GetScheduledFileByIdAsync(id);
                if (scheduledFile == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Scheduled file not found.",
                        data = (object?)null,
                        errors = (object?)null
                    });
                }

                if (scheduledFile.IsProcessed)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Cannot cancel a processed scheduled upload.",
                        data = (object?)null,
                        errors = (object?)null
                    });
                }

                // Delete the temporary file
                if (System.IO.File.Exists(scheduledFile.FilePath))
                {
                    System.IO.File.Delete(scheduledFile.FilePath);
                }

                await archiveFileRepository.DeleteScheduledFileAsync(id);

                return Ok(new
                {
                    success = true,
                    message = "Scheduled upload cancelled successfully.",
                    data = (object?)null,
                    errors = (object?)null
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred while cancelling the scheduled upload.",
                    data = (object?)null,
                    errors = (object?)null
                });
            }
        }

        // Trash Management Endpoints

        [Authorize(Roles = "Admin")]
        [HttpGet("trash")]
        public async Task<IActionResult> GetTrashedFiles()
        {
            try
            {
                var trashedFiles = await archiveFileRepository.GetTrashedFilesAsync();
                
                var trashedFilesDtos = trashedFiles.Select(file => new TrashFileResponseDto
                {
                    Id = file.Id,
                    FileName = file.FileName,
                    FileExtension = file.FileExtension,
                    FileSizeInBytes = file.FileSizeInBytes,
                    VersionNumber = file.VersionNumber,
                    Description = file.Description,
                    Category = file.Category,
                    ItemId = file.ItemId,
                    ItemName = file.Item?.Name,
                    CreatedAt = file.CreatedAt,
                    CreatedBy = file.CreatedBy,
                    DeletedAt = file.DeletedAt,
                    DeletedBy = file.DeletedBy,
                    DaysInTrash = file.DeletedAt.HasValue ? (int)(DateTime.UtcNow - file.DeletedAt.Value).TotalDays : 0,
                    DaysRemaining = file.DeletedAt.HasValue ? Math.Max(0, 7 - (int)(DateTime.UtcNow - file.DeletedAt.Value).TotalDays) : 0,
                    CanRestore = file.DeletedAt.HasValue && (DateTime.UtcNow - file.DeletedAt.Value).TotalDays < 7
                }).ToList();

                return Ok(new
                {
                    success = true,
                    message = "Trashed files retrieved successfully",
                    data = trashedFilesDtos,
                    errors = (object?)null
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error retrieving trashed files",
                    data = (object?)null,
                    errors = new { General = new[] { ex.Message } }
                });
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("trash/{id:guid}/restore")]
        public async Task<IActionResult> RestoreFromTrash(Guid id)
        {
            try
            {
                // Get the trashed file
                var trashedFiles = await archiveFileRepository.GetTrashedFilesAsync();
                var file = trashedFiles.FirstOrDefault(f => f.Id == id);

                if (file == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "File not found in trash",
                        data = (object?)null,
                        errors = new { Id = new[] { "Invalid file ID." } }
                    });
                }

                // Check if file can still be restored (within 7 days)
                if (file.DeletedAt.HasValue && (DateTime.UtcNow - file.DeletedAt.Value).TotalDays >= 7)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "File cannot be restored - exceeded 7-day limit",
                        data = (object?)null,
                        errors = new { File = new[] { "File has been in trash for more than 7 days and cannot be restored." } }
                    });
                }

                // Restore file from trash folder
                var restoredFilePath = await fileManagementService.RestoreFileFromTrashAsync(file);
                
                // Mark as not deleted in database and update file path
                await archiveFileRepository.RestoreFromTrashAsync(id, restoredFilePath);

                return Ok(new
                {
                    success = true,
                    message = "File restored from trash successfully",
                    data = new { file.Id, file.FileName, file.VersionNumber },
                    errors = (object?)null
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error restoring file from trash",
                    data = (object?)null,
                    errors = new { General = new[] { ex.Message } }
                });
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("trash/{id:guid}/permanent")]
        public async Task<IActionResult> PermanentlyDeleteFromTrash(Guid id)
        {
            try
            {
                // Get the trashed file
                var trashedFiles = await archiveFileRepository.GetTrashedFilesAsync();
                var file = trashedFiles.FirstOrDefault(f => f.Id == id);

                if (file == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "File not found in trash",
                        data = (object?)null,
                        errors = new { Id = new[] { "Invalid file ID." } }
                    });
                }

                // Delete physical file from trash
                await fileManagementService.DeletePhysicalFileFromTrashAsync(file);
                
                // Permanently delete from database
                await archiveFileRepository.PermanentlyDeleteAsync(id);

                return Ok(new
                {
                    success = true,
                    message = "File permanently deleted",
                    data = new { file.Id, file.FileName, file.VersionNumber },
                    errors = (object?)null
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error permanently deleting file",
                    data = (object?)null,
                    errors = new { General = new[] { ex.Message } }
                });
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("trash/stats")]
        public async Task<IActionResult> GetTrashStats()
        {
            try
            {
                var trashedFiles = await archiveFileRepository.GetTrashedFilesAsync();
                
                var stats = new TrashStatsDto
                {
                    TotalTrashedFiles = trashedFiles.Count(),
                    FilesExpiringSoon = trashedFiles.Count(f => f.DeletedAt.HasValue && 
                        (DateTime.UtcNow - f.DeletedAt.Value).TotalDays >= 6 && 
                        (DateTime.UtcNow - f.DeletedAt.Value).TotalDays < 7),
                    TotalSizeInBytes = trashedFiles.Sum(f => f.FileSizeInBytes),
                    OldestFileDate = trashedFiles.Any() ? trashedFiles.Min(f => f.DeletedAt) : null
                };

                return Ok(new
                {
                    success = true,
                    message = "Trash statistics retrieved successfully",
                    data = stats,
                    errors = (object?)null
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error retrieving trash statistics",
                    data = (object?)null,
                    errors = new { General = new[] { ex.Message } }
                });
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("trash/cleanup")]
        public async Task<IActionResult> ForceCleanupTrash()
        {
            try
            {
                var expiredFiles = await archiveFileRepository.GetExpiredTrashedFilesAsync(7);
                int deletedCount = 0;

                foreach (var file in expiredFiles)
                {
                    try
                    {
                        await fileManagementService.DeletePhysicalFileFromTrashAsync(file);
                        await archiveFileRepository.PermanentlyDeleteAsync(file.Id);
                        deletedCount++;
                    }
                    catch (Exception ex)
                    {
                        // Log the error but continue with other files
                        // Could log to ILogger here
                        continue;
                    }
                }

                return Ok(new
                {
                    success = true,
                    message = $"Cleanup completed. {deletedCount} expired files permanently deleted.",
                    data = new { DeletedCount = deletedCount },
                    errors = (object?)null
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error during trash cleanup",
                    data = (object?)null,
                    errors = new { General = new[] { ex.Message } }
                });
            }
        }
    }
}