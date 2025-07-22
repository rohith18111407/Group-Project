using WareHouseFileArchiver.Models.Domains;

namespace WareHouseFileArchiver.Services
{
    public interface IFileManagementService
    {
        Task<string> MoveFileToTrashAsync(ArchiveFile file);
        Task<string> RestoreFileFromTrashAsync(ArchiveFile file);
        Task DeletePhysicalFileFromTrashAsync(ArchiveFile file);
    }

    public class FileManagementService : IFileManagementService
    {
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ILogger<FileManagementService> _logger;

        public FileManagementService(IWebHostEnvironment webHostEnvironment, ILogger<FileManagementService> logger)
        {
            _webHostEnvironment = webHostEnvironment;
            _logger = logger;
        }

        public async Task<string> MoveFileToTrashAsync(ArchiveFile file)
        {
            try
            {
                // Create trash folder path based on current date
                var trashDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
                var trashFolderPath = Path.Combine(_webHostEnvironment.ContentRootPath, "Trash", trashDate);
                
                // Ensure trash folder exists
                Directory.CreateDirectory(trashFolderPath);

                // Generate unique filename for trash
                var originalFileName = Path.GetFileNameWithoutExtension(file.FileName);
                var extension = file.FileExtension;
                var timestamp = DateTime.UtcNow.Ticks;
                var trashFileName = $"{originalFileName}_{timestamp}{extension}";
                var trashFilePath = Path.Combine(trashFolderPath, trashFileName);

                // Move file to trash
                if (File.Exists(file.FilePath))
                {
                    File.Move(file.FilePath, trashFilePath);
                    _logger.LogInformation("Moved file to trash: {OriginalPath} -> {TrashPath}", file.FilePath, trashFilePath);
                }
                else
                {
                    _logger.LogWarning("Original file not found during trash move: {FilePath}", file.FilePath);
                }

                return trashFilePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to move file to trash: {FileName}", file.FileName);
                throw;
            }
        }

        public async Task<string> RestoreFileFromTrashAsync(ArchiveFile file)
        {
            try
            {
                if (!file.DeletedAt.HasValue)
                {
                    throw new InvalidOperationException("File does not have a deletion date");
                }

                // Find file in trash using current FilePath (which should be the trash path)
                var trashFilePath = file.FilePath;
                
                if (!File.Exists(trashFilePath))
                {
                    throw new FileNotFoundException($"Trashed file not found at: {trashFilePath}");
                }

                string restoredFilePath;
                
                // Try to restore to original location if available
                if (!string.IsNullOrEmpty(file.OriginalFilePath) && Directory.Exists(Path.GetDirectoryName(file.OriginalFilePath)))
                {
                    // Check if original location is available
                    if (!File.Exists(file.OriginalFilePath))
                    {
                        restoredFilePath = file.OriginalFilePath;
                    }
                    else
                    {
                        // Generate a new name if original location is occupied
                        var directory = Path.GetDirectoryName(file.OriginalFilePath);
                        var fileName = Path.GetFileNameWithoutExtension(file.OriginalFilePath);
                        var extension = Path.GetExtension(file.OriginalFilePath);
                        var timestamp = DateTime.UtcNow.Ticks;
                        restoredFilePath = Path.Combine(directory, $"{fileName}_restored_{timestamp}{extension}");
                    }
                }
                else
                {
                    // Restore to default ArchiveFiles folder with timestamp
                    var archiveFilesPath = Path.Combine(_webHostEnvironment.ContentRootPath, "ArchiveFiles");
                    Directory.CreateDirectory(archiveFilesPath);
                    
                    var timestamp = DateTime.UtcNow.Ticks;
                    var restoredFileName = $"{file.FileName}_{timestamp}{file.FileExtension}";
                    restoredFilePath = Path.Combine(archiveFilesPath, restoredFileName);
                }

                // Move file back from trash
                File.Move(trashFilePath, restoredFilePath);
                _logger.LogInformation("Restored file from trash: {TrashPath} -> {RestoredPath}", trashFilePath, restoredFilePath);

                return restoredFilePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore file from trash: {FileName}", file.FileName);
                throw;
            }
        }

        public async Task DeletePhysicalFileFromTrashAsync(ArchiveFile file)
        {
            try
            {
                if (file.DeletedAt.HasValue)
                {
                    var trashDate = file.DeletedAt.Value.ToString("yyyy-MM-dd");
                    var trashFolderPath = Path.Combine(_webHostEnvironment.ContentRootPath, "Trash", trashDate);
                    
                    if (Directory.Exists(trashFolderPath))
                    {
                        var trashFiles = Directory.GetFiles(trashFolderPath, $"*{file.FileName}*{file.FileExtension}");
                        
                        foreach (var trashFile in trashFiles)
                        {
                            if (File.Exists(trashFile))
                            {
                                File.Delete(trashFile);
                                _logger.LogInformation("Permanently deleted file from trash: {FilePath}", trashFile);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete physical file from trash: {FileName}", file.FileName);
                throw;
            }
        }
    }
}
