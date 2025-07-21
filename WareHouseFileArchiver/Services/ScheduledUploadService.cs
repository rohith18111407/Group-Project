using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WareHouseFileArchiver.Interfaces;

namespace WareHouseFileArchiver.Services
{
    public class ScheduledUploadProcessorService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ScheduledUploadProcessorService> _logger;

        public ScheduledUploadProcessorService(
            IServiceProvider serviceProvider,
            ILogger<ScheduledUploadProcessorService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Scheduled Upload Processor Service started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var archiveFileRepository = scope.ServiceProvider.GetRequiredService<IArchiveFileRepository>();
                    var webHostEnvironment = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

                    var pendingFiles = await archiveFileRepository.GetPendingScheduledFilesAsync();

                    foreach (var scheduledFile in pendingFiles)
                    {
                        await ProcessScheduledFile(scheduledFile, archiveFileRepository, webHostEnvironment);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing scheduled uploads");
                }

                // Check every 30 seconds for due uploads
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        private async Task ProcessScheduledFile(
            Models.Domains.ArchiveFile scheduledFile,
            IArchiveFileRepository archiveFileRepository,
            IWebHostEnvironment webHostEnvironment)
        {
            try
            {
                _logger.LogInformation($"Processing scheduled file: {scheduledFile.FileName}");

                // Move file from temp location to final location
                var fileExtension = scheduledFile.FileExtension;
                var fileName = scheduledFile.FileName;
                var uniqueFileName = $"{fileName}_{DateTime.UtcNow.Ticks}{fileExtension}";
                var finalFilePath = Path.Combine(webHostEnvironment.ContentRootPath, "ArchiveFiles", uniqueFileName);

                // Ensure the Files directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(finalFilePath)!);

                // Move file from temp to final location
                if (File.Exists(scheduledFile.FilePath))
                {
                    File.Move(scheduledFile.FilePath, finalFilePath);
                    
                    // Update the file record
                    scheduledFile.FilePath = finalFilePath;
                    scheduledFile.IsProcessed = true;
                    scheduledFile.UpdatedAt = DateTime.UtcNow;
                    scheduledFile.UpdatedBy = "Schedule Service";

                    await archiveFileRepository.UpdateScheduledFileAsync(scheduledFile);

                    _logger.LogInformation($"Successfully processed scheduled file: {scheduledFile.FileName}");
                }
                else
                {
                    _logger.LogWarning($"Temporary file not found for scheduled upload: {scheduledFile.FileName}");
                    
                    // Mark as processed but with error (you might want to handle this differently)
                    scheduledFile.IsProcessed = true;
                    scheduledFile.UpdatedAt = DateTime.UtcNow;
                    scheduledFile.UpdatedBy = "System";
                    await archiveFileRepository.UpdateScheduledFileAsync(scheduledFile);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to process scheduled file: {scheduledFile.FileName}");
            }
        }
    }
}