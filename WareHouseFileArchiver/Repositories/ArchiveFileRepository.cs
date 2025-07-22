using Microsoft.EntityFrameworkCore;
using WareHouseFileArchiver.Data;
using WareHouseFileArchiver.Interfaces;
using WareHouseFileArchiver.Models.Domains;

namespace WareHouseFileArchiver.Repositories
{
    public class ArchiveFileRepository : IArchiveFileRepository
    {
        private readonly WareHouseDbContext dbContext;

        public ArchiveFileRepository(WareHouseDbContext dbContext)
        {
            this.dbContext = dbContext;
        }

        public async Task<ArchiveFile> UploadAsync(ArchiveFile archiveFile)
        {
            if (archiveFile.CreatedAt == default(DateTime))
                archiveFile.CreatedAt = DateTime.UtcNow;

            int version = 1;
            bool isUnique = false;

            while (!isUnique)
            {
                // Check if this version is unique
                bool exists = await dbContext.ArchiveFiles.AnyAsync(f =>
                    f.FileName == archiveFile.FileName &&
                    f.Category == archiveFile.Category &&
                    f.VersionNumber == version);

                if (!exists)
                {
                    archiveFile.VersionNumber = version;
                    isUnique = true;
                }
                else
                {
                    version++; // Try the next version
                }
            }

            await dbContext.ArchiveFiles.AddAsync(archiveFile);
            await dbContext.SaveChangesAsync();

            return archiveFile;
        }

        public async Task<IEnumerable<ArchiveFile>> GetFilesByItemIdAsync(Guid itemId)
        {
            return await dbContext.ArchiveFiles
                .Include(f => f.Item)
                .Where(f => f.ItemId == itemId && !f.IsDeleted)
                .OrderByDescending(f => f.VersionNumber)
                .ToListAsync();
        }

        public async Task<ArchiveFile?> GetFileByNameAndVersionAsync(string fileName, int version)
        {
            return await dbContext.ArchiveFiles
                .Include(f => f.Item)
                .FirstOrDefaultAsync(f => f.FileName == fileName && f.VersionNumber == version && !f.IsDeleted);
        }


        public async Task<bool> ItemExistsAsync(Guid itemId)
        {
            return await dbContext.Items.AnyAsync(i => i.Id == itemId);
        }

        public async Task<ArchiveFile?> GetLatestVersionAsync(string fileName, Guid itemId)
        {
            return await dbContext.ArchiveFiles
                .Where(f => f.FileName == fileName && f.ItemId == itemId && !f.IsDeleted)
                .OrderByDescending(f => f.VersionNumber)
                .FirstOrDefaultAsync();
        }

        public async Task UpdateAsync(ArchiveFile file)
        {
            dbContext.ArchiveFiles.Update(file);
            await dbContext.SaveChangesAsync();
        }

        public async Task<Item?> GetItemByIdAsync(Guid itemId)
        {
            return await dbContext.Items.FirstOrDefaultAsync(i => i.Id == itemId);
        }

        public async Task<IEnumerable<ArchiveFile>> GetAllFilesAsync()
        {
            return await dbContext.ArchiveFiles
                .Include(f => f.Item)
                .Where(f => !f.IsDeleted && (!f.IsScheduled || f.IsProcessed)) // Exclude deleted files AND show only processed scheduled files
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();
        }

        public async Task<ArchiveFile?> GetFileByIdAsync(Guid id)
        {
            return await dbContext.ArchiveFiles.FirstOrDefaultAsync(f => f.Id == id && !f.IsDeleted);
        }

        public async Task DeleteAsync(ArchiveFile file)
        {
            var logs = dbContext.FileDownloadLogs.Where(log => log.ArchiveFileId == file.Id);
            dbContext.FileDownloadLogs.RemoveRange(logs); // Remove associated logs
            dbContext.ArchiveFiles.Remove(file);
            await dbContext.SaveChangesAsync();
        }

        public async Task LogDownloadAsync(FileDownloadLog log)
        {
            await dbContext.FileDownloadLogs.AddAsync(log);
            await dbContext.SaveChangesAsync();
        }

        // Scheduled File upload methods
        public async Task<IEnumerable<ArchiveFile>> GetScheduledFilesAsync()
        {
            return await dbContext.ArchiveFiles
                .Where(f => f.IsScheduled)
                .Include(f => f.Item)
                .OrderBy(f => f.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<ArchiveFile>> GetPendingScheduledFilesAsync()
        {
            return await dbContext.ArchiveFiles
                .Where(f => f.IsScheduled && !f.IsProcessed && f.CreatedAt <= DateTime.UtcNow)
                .Include(f => f.Item)
                .ToListAsync();
        }

        public async Task<ArchiveFile?> GetScheduledFileByIdAsync(Guid id)
        {
            return await dbContext.ArchiveFiles
                .Include(f => f.Item)
                .FirstOrDefaultAsync(f => f.Id == id && f.IsScheduled);
        }

        public async Task UpdateScheduledFileAsync(ArchiveFile archiveFile)
        {
            dbContext.ArchiveFiles.Update(archiveFile);
            await dbContext.SaveChangesAsync();
        }

        public async Task DeleteScheduledFileAsync(Guid id)
        {
            var file = await dbContext.ArchiveFiles.FindAsync(id);
            if (file != null)
            {
                dbContext.ArchiveFiles.Remove(file);
                await dbContext.SaveChangesAsync();
            }
        }

        // Trash/Soft Delete methods implementation
        public async Task<IEnumerable<ArchiveFile>> GetTrashedFilesAsync()
        {
            return await dbContext.ArchiveFiles
                .Include(f => f.Item)
                .Where(f => f.IsDeleted)
                .OrderByDescending(f => f.DeletedAt)
                .ToListAsync();
        }

        public async Task MoveToTrashAsync(ArchiveFile file, string deletedBy, string trashFilePath)
        {
            // Store original path and mark file as deleted in database
            file.OriginalFilePath = file.FilePath; // Store original path
            file.FilePath = trashFilePath; // Update to trash path
            file.IsDeleted = true;
            file.DeletedAt = DateTime.UtcNow;
            file.DeletedBy = deletedBy;

            dbContext.ArchiveFiles.Update(file);
            await dbContext.SaveChangesAsync();
        }

        public async Task RestoreFromTrashAsync(Guid fileId, string restoredFilePath)
        {
            var file = await dbContext.ArchiveFiles.FirstOrDefaultAsync(f => f.Id == fileId && f.IsDeleted);
            if (file != null)
            {
                file.IsDeleted = false;
                file.DeletedAt = null;
                file.DeletedBy = null;
                // Restore to the original location or the provided restored path
                file.FilePath = restoredFilePath;
                file.OriginalFilePath = null; // Clear the original path since file is restored

                dbContext.ArchiveFiles.Update(file);
                await dbContext.SaveChangesAsync();
            }
        }

        public async Task PermanentlyDeleteAsync(Guid fileId)
        {
            var file = await dbContext.ArchiveFiles.FindAsync(fileId);
            if (file != null)
            {
                // Remove associated download logs
                var logs = dbContext.FileDownloadLogs.Where(log => log.ArchiveFileId == file.Id);
                dbContext.FileDownloadLogs.RemoveRange(logs);
                
                // Remove the file record
                dbContext.ArchiveFiles.Remove(file);
                await dbContext.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<ArchiveFile>> GetExpiredTrashedFilesAsync(int daysOld = 7)
        {
            var expiryDate = DateTime.UtcNow.AddDays(-daysOld);
            return await dbContext.ArchiveFiles
                .Where(f => f.IsDeleted && f.DeletedAt <= expiryDate)
                .ToListAsync();
        }

    }
}