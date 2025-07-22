using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using WareHouseFileArchiver.Interfaces;
using WareHouseFileArchiver.Models.Domains;
using WareHouseFileArchiver.SignalRHub;

namespace WareHouseFileArchiver.Services
{
    public class AdminArchivalService : BackgroundService
    {
        private readonly IServiceProvider serviceProvider;
        private readonly ILogger<AdminArchivalService> logger;
        private readonly TimeSpan checkInterval = TimeSpan.FromDays(1); // Run daily
        private readonly int inactivityThresholdDays = 30;

        public AdminArchivalService(
            IServiceProvider serviceProvider,
            ILogger<AdminArchivalService> logger)
        {
            this.serviceProvider = serviceProvider;
            this.logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("Admin Archival Service started. Check interval: {CheckInterval}, Threshold: {ThresholdDays} days", 
                checkInterval, inactivityThresholdDays);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    logger.LogInformation("Starting admin inactivity check at {Timestamp}", DateTime.UtcNow);
                    await CheckAndArchiveInactiveAdminFiles();
                    logger.LogInformation("Completed admin inactivity check. Next check in {CheckInterval}", checkInterval);
                    await Task.Delay(checkInterval, stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error occurred while processing inactive admin archival");
                    // Retry after 30 minutes on error
                    await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken); 
                }
            }
        }

        private async Task CheckAndArchiveInactiveAdminFiles()
        {
            using var scope = serviceProvider.CreateScope();
            
            try
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var archiveFileRepository = scope.ServiceProvider.GetRequiredService<IArchiveFileRepository>();
                var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<NotificationsHub>>();

                var cutoffDate = DateTime.UtcNow.AddDays(-inactivityThresholdDays);

                logger.LogInformation("Checking for admins inactive since {CutoffDate}", cutoffDate);

                // Get all admins who haven't logged in for more than 30 days
                var allAdmins = await userManager.GetUsersInRoleAsync("Admin");
                var inactiveAdmins = allAdmins.Where(admin =>
                    admin.LastLoginAt.HasValue && admin.LastLoginAt.Value < cutoffDate)
                    .ToList();

                logger.LogInformation("Found {TotalAdmins} total admins, {InactiveAdmins} are inactive", 
                    allAdmins.Count, inactiveAdmins.Count);

                if (!inactiveAdmins.Any())
                {
                    logger.LogInformation("No inactive admins found for archival");
                    return;
                }

                // Get list of inactive admin usernames
                var inactiveAdminUsernames = inactiveAdmins.Select(a => a.UserName).ToList();
                
                logger.LogInformation("Inactive admins: {InactiveAdmins}", string.Join(", ", inactiveAdminUsernames));

                // Preview files that will be archived
                var filesToArchive = await archiveFileRepository.GetFilesForInactiveAdminsAsync(inactiveAdminUsernames);
                var filesCount = filesToArchive.Count();

                logger.LogInformation("Found {FilesCount} files to archive from inactive admins", filesCount);

                if (filesCount == 0)
                {
                    logger.LogInformation("No files found for inactive admins");
                    return;
                }

                // Archive the files using repository method
                var archiveReason = $"Admin inactive since {cutoffDate:yyyy-MM-dd}";
                var archivedCount = await archiveFileRepository.ArchiveInactiveAdminFilesAsync(inactiveAdminUsernames, archiveReason);

                logger.LogInformation("Successfully archived {ArchivedCount} files for {InactiveAdminsCount} inactive admins", 
                    archivedCount, inactiveAdmins.Count);

                // Send SignalR notification
                try
                {
                    var notificationPayload = new
                    {
                        Action = "Files Archived Due to Admin Inactivity",
                        InactiveAdminsCount = inactiveAdmins.Count,
                        InactiveAdmins = inactiveAdmins.Select(a => a.UserName).ToList(),
                        FilesArchivedCount = archivedCount,
                        ArchivedAt = DateTime.UtcNow,
                        Reason = archiveReason,
                        CutoffDate = cutoffDate,
                        InactiveDaysThreshold = inactivityThresholdDays,
                        Files = filesToArchive.Select(f => new
                        {
                            Id = f.Id,
                            FileName = f.FileName ?? "Unknown file",
                            FileExtension = f.FileExtension ?? "",
                            ItemName = f.Item?.Name ?? "Unknown item",
                            CreatedBy = f.CreatedBy ?? "Unknown",
                        }).Take(10).ToList(), // Limit to first 10 files for notification size
                        Message = $"{archivedCount} files archived from {inactiveAdmins.Count} inactive admins"
                    };

                    await hubContext.Clients.All.SendAsync("ReceiveNotification", notificationPayload);
                    
                    logger.LogInformation("SignalR notification sent for admin inactivity archival");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to send SignalR notification for admin inactivity archival");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in CheckAndArchiveInactiveAdminFiles");
                throw; // Re-throw to trigger retry logic
            }
        }
    }
}