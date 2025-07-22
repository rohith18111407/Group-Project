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
    [Route("api/v1/archive")]
    public class ArchiveManagementController : ControllerBase
    {
        private readonly IArchiveFileRepository archiveFileRepository;
        private readonly UserManager<ApplicationUser> userManager;
        private readonly IHubContext<NotificationsHub> hubContext;
        private readonly ILogger<ArchiveManagementController> logger;

        public ArchiveManagementController(
            IArchiveFileRepository archiveFileRepository,
            UserManager<ApplicationUser> userManager,
            IHubContext<NotificationsHub> hubContext,
            ILogger<ArchiveManagementController> logger)
        {
            this.archiveFileRepository = archiveFileRepository;
            this.userManager = userManager;
            this.hubContext = hubContext;
            this.logger = logger;
        }

        [Authorize(Roles = "Admin,Staff")]
        [HttpGet("files")]
        public async Task<IActionResult> GetArchivedFiles()
        {
            try
            {
                var files = await archiveFileRepository.GetArchivedFilesAsync();

                var response = files.Select(f => new ArchivedFileResponseDto
                {
                    Id = f.Id,
                    FileName = f.FileName,
                    FileExtension = f.FileExtension,
                    FileSizeInBytes = f.FileSizeInBytes,
                    VersionNumber = f.VersionNumber,
                    Description = f.Description,
                    Category = f.Category,
                    ItemId = f.ItemId,
                    ItemName = f.Item?.Name,
                    CreatedAt = f.CreatedAt,
                    CreatedBy = f.CreatedBy,
                    ArchivedAt = f.ArchivedAt,
                    ArchivedReason = f.ArchivedReason
                });

                return Ok(new
                {
                    success = true,
                    message = "Archived files fetched successfully",
                    data = response,
                    errors = (object?)null
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error fetching archived files");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error fetching archived files",
                    data = (object?)null,
                    errors = new { General = new[] { ex.Message } }
                });
            }
        }

        [Authorize(Roles = "Admin,Staff")]
        [HttpGet("files/by-admin/{adminName}")]
        public async Task<IActionResult> GetArchivedFilesByAdmin(string adminName)
        {
            try
            {
                var files = await archiveFileRepository.GetArchivedFilesByAdminAsync(adminName);

                var response = files.Select(f => new ArchivedFileResponseDto
                {
                    Id = f.Id,
                    FileName = f.FileName,
                    FileExtension = f.FileExtension,
                    FileSizeInBytes = f.FileSizeInBytes,
                    VersionNumber = f.VersionNumber,
                    Description = f.Description,
                    Category = f.Category,
                    ItemId = f.ItemId,
                    ItemName = f.Item?.Name,
                    CreatedAt = f.CreatedAt,
                    CreatedBy = f.CreatedBy,
                    ArchivedAt = f.ArchivedAt,
                    ArchivedReason = f.ArchivedReason
                });

                return Ok(new
                {
                    success = true,
                    message = $"Archived files for {adminName} fetched successfully",
                    data = response,
                    errors = (object?)null
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error fetching archived files by admin {AdminName}", adminName);
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error fetching archived files",
                    data = (object?)null,
                    errors = new { General = new[] { ex.Message } }
                });
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("files/{fileId}/unarchive")]
        public async Task<IActionResult> UnarchiveFile(Guid fileId)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var user = await userManager.FindByIdAsync(userId);
                var userName = user?.UserName ?? "Unknown";

                // Get the file before unarchiving to include in notification
                var file = await archiveFileRepository.GetFileByIdAsync(fileId);
                if (file == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "File not found",
                        data = (object?)null,
                        errors = new { FileId = new[] { "File not found." } }
                    });
                }

                var success = await archiveFileRepository.UnarchiveFileAsync(fileId, userName);

                if (!success)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "File not found or not archived",
                        data = (object?)null,
                        errors = new { FileId = new[] { "Invalid file ID or file is not archived." } }
                    });
                }

                logger.LogInformation("File {FileId} unarchived by {UserName}", fileId, userName);

                // Send SignalR notification
                try
                {
                    await hubContext.Clients.All.SendAsync("ReceiveNotification", new
                    {
                        Action = "File Unarchived",
                        FileName = file?.FileName ?? "Unknown file",
                        FileExtension = file?.FileExtension ?? "",
                        ItemName = file?.Item?.Name ?? "Unknown item",
                        ItemId = file?.ItemId,
                        VersionNumber = file?.VersionNumber ?? 1,
                        FileSizeInBytes = file?.FileSizeInBytes ?? 0,
                        CreatedBy = file?.CreatedBy ?? "Unknown",
                        CreatedAt = file?.CreatedAt ?? DateTime.UtcNow,
                        UnarchivedBy = userName,
                        UnarchivedAt = DateTime.UtcNow,
                        Message = $"File '{file?.FileName}{file?.FileExtension}' was unarchived by {userName}"
                    });

                    logger.LogInformation("Unarchive notification sent for file {FileId}", fileId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to send unarchive notification for file {FileId}", fileId);
                }

                return Ok(new
                {
                    success = true,
                    message = "File unarchived successfully",
                    data = new { fileId, unarchivedBy = userName },
                    errors = (object?)null
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error unarchiving file {FileId}", fileId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error unarchiving file",
                    data = (object?)null,
                    errors = new { General = new[] { ex.Message } }
                });
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("files/{fileId}/archive")]
        public async Task<IActionResult> ArchiveFileManually(Guid fileId, [FromBody] ArchiveFileRequestDto request)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var user = await userManager.FindByIdAsync(userId);
                var userName = user?.UserName ?? "Unknown";

                // Get the file before archiving to include in notification
                var file = await archiveFileRepository.GetFileByIdAsync(fileId);
                if (file == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "File not found",
                        data = (object?)null,
                        errors = new { FileId = new[] { "File not found." } }
                    });
                }

                var success = await archiveFileRepository.ArchiveFileManuallyAsync(fileId, userName, request.Reason);

                if (!success)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "File not found or already archived",
                        data = (object?)null,
                        errors = new { FileId = new[] { "Invalid file ID or file is already archived." } }
                    });
                }

                logger.LogInformation("File {FileId} manually archived by {UserName}", fileId, userName);

                // Send SignalR notification
                try
                {
                    await hubContext.Clients.All.SendAsync("ReceiveNotification", new
                    {
                        Action = "File Manually Archived",
                        FileName = file?.FileName ?? "Unknown file",
                        FileExtension = file?.FileExtension ?? "",
                        ItemName = file?.Item?.Name ?? "Unknown item",
                        ItemId = file?.ItemId,
                        VersionNumber = file?.VersionNumber ?? 1,
                        FileSizeInBytes = file?.FileSizeInBytes ?? 0,
                        CreatedBy = file?.CreatedBy ?? "Unknown",
                        CreatedAt = file?.CreatedAt ?? DateTime.UtcNow,
                        ArchivedBy = userName,
                        ArchivedAt = DateTime.UtcNow,
                        Reason = request.Reason,
                        Message = $"File '{file?.FileName}{file?.FileExtension}' was archived by {userName}"
                    });

                    logger.LogInformation("Archive notification sent for file {FileId}", fileId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to send archive notification for file {FileId}", fileId);
                }

                return Ok(new
                {
                    success = true,
                    message = "File archived successfully",
                    data = new { fileId, archivedBy = userName, reason = request.Reason },
                    errors = (object?)null
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error archiving file {FileId}", fileId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error archiving file",
                    data = (object?)null,
                    errors = new { General = new[] { ex.Message } }
                });
            }
        }

        [Authorize(Roles = "Admin,Staff")]
        [HttpGet("stats")]
        public async Task<IActionResult> GetArchivalStats()
        {
            try
            {
                var statsByAdmin = await archiveFileRepository.GetArchivalStatsByAdminAsync();

                return Ok(new
                {
                    success = true,
                    message = "Archival statistics fetched successfully",
                    data = new
                    {
                        totalArchivedFiles = statsByAdmin.Values.Sum(),
                        archivedFilesByAdmin = statsByAdmin
                    },
                    errors = (object?)null
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error fetching archival stats");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error fetching archival statistics",
                    data = (object?)null,
                    errors = new { General = new[] { ex.Message } }
                });
            }
        }
        
        [Authorize(Roles = "Admin")]
        [HttpPost("admin-inactivity/trigger")]
        public async Task<IActionResult> TriggerInactiveAdminArchival([FromBody] TriggerArchivalRequestDto request)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var user = await userManager.FindByIdAsync(userId);
                var userName = user?.UserName ?? "Unknown";

                var cutoffDate = DateTime.UtcNow.AddDays(-(request.InactiveDaysThreshold ?? 30));
                
                // Get inactive admins
                var allAdmins = await userManager.GetUsersInRoleAsync("Admin");
                var inactiveAdmins = allAdmins.Where(admin =>
                    admin.LastLoginAt.HasValue && admin.LastLoginAt.Value < cutoffDate)
                    .ToList();

                if (!inactiveAdmins.Any())
                {
                    return Ok(new
                    {
                        success = true,
                        message = "No inactive admins found",
                        data = new { archivedCount = 0, inactiveAdminsCount = 0 },
                        errors = (object?)null
                    });
                }

                var inactiveAdminUsernames = inactiveAdmins.Select(a => a.UserName).ToList();
                var archiveReason = $"Manually triggered archival - Admin inactive since {cutoffDate:yyyy-MM-dd} (Triggered by {userName})";
                
                var archivedCount = await archiveFileRepository.ArchiveInactiveAdminFilesAsync(inactiveAdminUsernames, archiveReason);

                logger.LogInformation("Manual inactive admin archival triggered by {UserName}: {ArchivedCount} files archived", userName, archivedCount);

                // Send SignalR notification
                try
                {
                    await hubContext.Clients.All.SendAsync("ReceiveNotification", new
                    {
                        Action = "Manual Inactive Admin Archival Triggered",
                        TriggeredBy = userName,
                        InactiveAdminsCount = inactiveAdmins.Count,
                        FilesArchivedCount = archivedCount,
                        InactiveAdmins = inactiveAdminUsernames,
                        ArchivedAt = DateTime.UtcNow,
                        Reason = archiveReason,
                        Message = $"Manual archival triggered: {archivedCount} files from {inactiveAdmins.Count} inactive admins"
                    });

                    logger.LogInformation("Manual inactive admin archival notification sent");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to send manual inactive admin archival notification");
                }

                return Ok(new
                {
                    success = true,
                    message = $"Successfully archived {archivedCount} files from {inactiveAdmins.Count} inactive admins",
                    data = new 
                    { 
                        archivedCount, 
                        inactiveAdminsCount = inactiveAdmins.Count,
                        inactiveAdmins = inactiveAdmins.Select(a => new { a.UserName, a.LastLoginAt })
                    },
                    errors = (object?)null
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during inactive admin archival process");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error occurred during archival process",
                    data = (object?)null,
                    errors = new { General = new[] { ex.Message } }
                });
            }
        }

        [Authorize(Roles = "Admin,Staff")]
        [HttpGet("admin-inactivity/preview")]
        public async Task<IActionResult> PreviewInactiveAdminArchival([FromQuery] int inactiveDaysThreshold = 30)
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-inactiveDaysThreshold);
                
                // Get inactive admins
                var allAdmins = await userManager.GetUsersInRoleAsync("Admin");
                var inactiveAdmins = allAdmins.Where(admin =>
                    admin.LastLoginAt.HasValue && admin.LastLoginAt.Value < cutoffDate)
                    .ToList();

                if (!inactiveAdmins.Any())
                {
                    return Ok(new
                    {
                        success = true,
                        message = "No inactive admins found",
                        data = new 
                        { 
                            inactiveAdmins = new List<object>(),
                            filesToBeArchived = new List<object>(),
                            totalFilesCount = 0
                        },
                        errors = (object?)null
                    });
                }

                var inactiveAdminUsernames = inactiveAdmins.Select(a => a.UserName).ToList();
                var filesToArchive = await archiveFileRepository.GetFilesForInactiveAdminsAsync(inactiveAdminUsernames);

                var response = new
                {
                    inactiveAdmins = inactiveAdmins.Select(a => new 
                    { 
                        a.UserName, 
                        a.LastLoginAt,
                        DaysInactive = (DateTime.UtcNow - a.LastLoginAt.Value).Days
                    }),
                    filesToBeArchived = filesToArchive.Select(f => new 
                    {
                        f.Id,
                        f.FileName,
                        f.FileExtension,
                        f.CreatedBy,
                        f.CreatedAt,
                        ItemName = f.Item?.Name,
                        f.VersionNumber,
                        FileSizeMB = Math.Round(f.FileSizeInBytes / 1048576.0, 2)
                    }),
                    totalFilesCount = filesToArchive.Count()
                };

                return Ok(new
                {
                    success = true,
                    message = $"Found {inactiveAdmins.Count} inactive admins with {filesToArchive.Count()} files to be archived",
                    data = response,
                    errors = (object?)null
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during inactive admin archival preview");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error occurred during preview",
                    data = (object?)null,
                    errors = new { General = new[] { ex.Message } }
                });
            }
        }

        [Authorize(Roles = "Admin,Staff")]
        [HttpGet("admin-inactivity/inactive-admins")]
        public async Task<IActionResult> GetInactiveAdmins([FromQuery] int inactiveDaysThreshold = 30)
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-inactiveDaysThreshold);
                var allAdmins = await userManager.GetUsersInRoleAsync("Admin");
                
                var inactiveAdmins = allAdmins.Where(admin =>
                    admin.LastLoginAt.HasValue && admin.LastLoginAt.Value < cutoffDate)
                    .Select(a => new 
                    { 
                        a.Id,
                        a.UserName, 
                        a.Email,
                        a.LastLoginAt,
                        DaysInactive = a.LastLoginAt.HasValue ? (DateTime.UtcNow - a.LastLoginAt.Value).Days : (int?)null
                    })
                    .OrderByDescending(a => a.DaysInactive)
                    .ToList();

                return Ok(new
                {
                    success = true,
                    message = $"Found {inactiveAdmins.Count} inactive admins",
                    data = new 
                    { 
                        inactiveAdmins,
                        thresholdDays = inactiveDaysThreshold,
                        cutoffDate
                    },
                    errors = (object?)null
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving inactive admins");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error retrieving inactive admins",
                    data = (object?)null,
                    errors = new { General = new[] { ex.Message } }
                });
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("general-inactivity/trigger")]
        public async Task<IActionResult> TriggerGeneralInactivityArchival([FromBody] TriggerArchivalRequestDto request)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var user = await userManager.FindByIdAsync(userId);
                var userName = user?.UserName ?? "Unknown";

                var inactiveDays = request.InactiveDaysThreshold ?? 30;
                var archivedCount = await archiveFileRepository.ArchiveInactiveUsersFilesAsync(inactiveDays);

                logger.LogInformation("General inactivity archival triggered by {UserName}: {ArchivedCount} files archived", userName, archivedCount);

                // Send SignalR notification
                try
                {
                    await hubContext.Clients.All.SendAsync("ReceiveNotification", new
                    {
                        Action = "General Inactivity Archival Triggered",
                        TriggeredBy = userName,
                        FilesArchivedCount = archivedCount,
                        InactiveDaysThreshold = inactiveDays,
                        ArchivedAt = DateTime.UtcNow,
                        Message = $"General inactivity archival: {archivedCount} files archived due to {inactiveDays} days of inactivity"
                    });

                    logger.LogInformation("General inactivity archival notification sent");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to send general inactivity archival notification");
                }

                return Ok(new
                {
                    success = true,
                    message = $"Successfully archived {archivedCount} files due to {inactiveDays} days of inactivity",
                    data = new { archivedCount, inactiveDaysThreshold = inactiveDays },
                    errors = (object?)null
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during general inactivity archival process");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error occurred during general archival process",
                    data = (object?)null,
                    errors = new { General = new[] { ex.Message } }
                });
            }
        }

    }
}