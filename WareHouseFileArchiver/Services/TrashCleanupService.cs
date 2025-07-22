using WareHouseFileArchiver.Interfaces;

namespace WareHouseFileArchiver.Services
{
    public class TrashCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TrashCleanupService> _logger;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public TrashCleanupService(
            IServiceProvider serviceProvider, 
            ILogger<TrashCleanupService> logger,
            IWebHostEnvironment webHostEnvironment)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _webHostEnvironment = webHostEnvironment;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CleanupExpiredFiles();
                    
                    // Wait for 24 hours (run once daily)
                    await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during trash cleanup");
                    
                    // Wait 1 hour before retrying if there's an error
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
            }
        }

        private async Task CleanupExpiredFiles()
        {
            using var scope = _serviceProvider.CreateScope();
            var archiveFileRepository = scope.ServiceProvider.GetRequiredService<IArchiveFileRepository>();

            try
            {
                _logger.LogInformation("Starting trash cleanup process");

                // Get files older than 7 days
                var expiredFiles = await archiveFileRepository.GetExpiredTrashedFilesAsync(7);
                var deletedCount = 0;

                foreach (var file in expiredFiles)
                {
                    try
                    {
                        // Delete physical file from trash folder
                        await DeletePhysicalFileFromTrash(file);

                        // Permanently delete from database
                        await archiveFileRepository.PermanentlyDeleteAsync(file.Id);
                        
                        deletedCount++;
                        _logger.LogInformation("Permanently deleted expired file: {FileName} (ID: {FileId})", 
                            file.FileName, file.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to delete expired file: {FileName} (ID: {FileId})", 
                            file.FileName, file.Id);
                    }
                }

                _logger.LogInformation("Trash cleanup completed. Deleted {DeletedCount} expired files", deletedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during trash cleanup process");
                throw;
            }
        }

        private async Task DeletePhysicalFileFromTrash(Models.Domains.ArchiveFile file)
        {
            try
            {
                if (file.DeletedAt.HasValue)
                {
                    // Construct trash file path based on deletion date
                    var deletionDate = file.DeletedAt.Value.ToString("yyyy-MM-dd");
                    var trashFolderPath = Path.Combine(_webHostEnvironment.ContentRootPath, "Trash", deletionDate);
                    
                    // Try to find the file in the trash folder
                    if (Directory.Exists(trashFolderPath))
                    {
                        var trashFiles = Directory.GetFiles(trashFolderPath, $"*{file.FileName}*{file.FileExtension}");
                        
                        foreach (var trashFile in trashFiles)
                        {
                            if (File.Exists(trashFile))
                            {
                                File.Delete(trashFile);
                                _logger.LogInformation("Deleted physical file from trash: {FilePath}", trashFile);
                            }
                        }
                    }
                }

                // Also check if file still exists in original location (fallback)
                if (File.Exists(file.FilePath))
                {
                    File.Delete(file.FilePath);
                    _logger.LogInformation("Deleted physical file from original location: {FilePath}", file.FilePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete physical file for: {FileName}", file.FileName);
                // Don't throw - we still want to remove from database even if physical file cleanup fails
            }
        }
    }
}
