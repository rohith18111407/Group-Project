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

        public async Task<IEnumerable<ArchiveFile>> GetFilesByItemIdAsync(Guid itemId, bool includeArchived = false)
        {
            var query = dbContext.ArchiveFiles
                .Include(f => f.Item)
                .Where(f => f.ItemId == itemId);

            if (!includeArchived)
                query = query.Where(f => !f.IsArchivedDueToInactivity);

            return await query
                .OrderByDescending(f => f.VersionNumber)
                .ToListAsync();
        }

        public async Task<ArchiveFile?> GetFileByNameAndVersionAsync(string fileName, int version)
        {
            return await dbContext.ArchiveFiles
                .Include(f => f.Item)
                .FirstOrDefaultAsync(f => f.FileName == fileName && f.VersionNumber == version);
        }

        public async Task<ArchiveFile?> GetLatestVersionAsync(string fileName, Guid itemId, bool includeArchived = false)
        {
            var query = dbContext.ArchiveFiles
                .Where(f => f.FileName == fileName && f.ItemId == itemId);

            if (!includeArchived)
                query = query.Where(f => !f.IsArchivedDueToInactivity);

            return await query
                .OrderByDescending(f => f.VersionNumber)
                .FirstOrDefaultAsync();
        }


        public async Task<bool> ItemExistsAsync(Guid itemId)
        {
            return await dbContext.Items.AnyAsync(i => i.Id == itemId);
        }

        public async Task<ArchiveFile?> GetLatestVersionAsync(string fileName, Guid itemId)
        {
            return await dbContext.ArchiveFiles
                .Where(f => f.FileName == fileName && f.ItemId == itemId)
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

        public async Task<IEnumerable<ArchiveFile>> GetAllFilesAsync(bool includedArchived = false)
        {
            return await dbContext.ArchiveFiles
                .Include(f => f.Item)
                .Where(f => includedArchived || !f.IsArchivedDueToInactivity)
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();
        }

        public async Task<ArchiveFile?> GetFileByIdAsync(Guid id)
        {
            return await dbContext.ArchiveFiles.FirstOrDefaultAsync(f => f.Id == id);
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

        public async Task<IEnumerable<ArchiveFile>> GetArchivedFilesAsync()
        {
            return await dbContext.ArchiveFiles
                .Include(f => f.Item)
                .Where(f => f.IsArchivedDueToInactivity)
                .OrderByDescending(f => f.ArchivedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<ArchiveFile>> GetArchivedFilesByAdminAsync(string adminName)
        {
            return await dbContext.ArchiveFiles
                .Include(f => f.Item)
                .Where(f => f.IsArchivedDueToInactivity && f.CreatedBy == adminName)
                .OrderByDescending(f => f.ArchivedAt)
                .ToListAsync();
        }

        public async Task<bool> UnarchiveFileAsync(Guid fileId, string unarchiveBy)
        {
            var file = await dbContext.ArchiveFiles.FindAsync(fileId);
            if (file == null || !file.IsArchivedDueToInactivity)
                return false;

            file.IsArchivedDueToInactivity = false;
            file.ArchivedAt = null;
            file.ArchivedReason = null;
            file.UpdatedAt = DateTime.UtcNow;
            file.UpdatedBy = unarchiveBy;

            await dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ArchiveFileManuallyAsync(Guid fileId, string archiveBy, string reason)
        {
            var file = await dbContext.ArchiveFiles.FindAsync(fileId);
            if (file == null || file.IsArchivedDueToInactivity)
                return false;

            file.IsArchivedDueToInactivity = true;
            file.ArchivedAt = DateTime.UtcNow;
            file.ArchivedReason = reason;
            file.UpdatedAt = DateTime.UtcNow;
            file.UpdatedBy = archiveBy;

            await dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<Dictionary<string, int>> GetArchivalStatsByAdminAsync()
        {
            var archivedFiles = await dbContext.ArchiveFiles
                .Where(f => f.IsArchivedDueToInactivity)
                .ToListAsync();

            return archivedFiles
                .GroupBy(f => f.CreatedBy)
                .ToDictionary(g => g.Key ?? "Unknown", g => g.Count());
        }

        public async Task<int> ArchiveInactiveUsersFilesAsync(int inactiveDaysThreshold)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-inactiveDaysThreshold);

            // Find files where the user's last activity (based on file operations) was before cutoff
            var filesToArchive = await dbContext.ArchiveFiles
                .Where(f => !f.IsArchivedDueToInactivity &&
                        (f.UpdatedAt.HasValue ? f.UpdatedAt.Value < cutoffDate : f.CreatedAt < cutoffDate))
                .ToListAsync();

            foreach (var file in filesToArchive)
            {
                file.IsArchivedDueToInactivity = true;
                file.ArchivedAt = DateTime.UtcNow;
                file.ArchivedReason = $"Automatically archived due to {inactiveDaysThreshold} days of inactivity";
                file.UpdatedAt = DateTime.UtcNow;
                file.UpdatedBy = "System";
            }

            await dbContext.SaveChangesAsync();
            return filesToArchive.Count;
        }

        public async Task<int> ArchiveInactiveAdminFilesAsync(List<string> inactiveAdminUsernames, string archiveReason = null)
        {
            if (!inactiveAdminUsernames.Any())
                return 0;

            // Get all active files created by inactive admins
            var filesToArchive = await dbContext.ArchiveFiles
                .Include(f => f.Item)
                .Where(f => inactiveAdminUsernames.Contains(f.CreatedBy) && !f.IsArchivedDueToInactivity)
                .ToListAsync();

            if (!filesToArchive.Any())
                return 0;

            var archiveDate = DateTime.UtcNow;
            var defaultReason = archiveReason ?? "Automatically archived due to admin inactivity";

            // Archive the files
            foreach (var file in filesToArchive)
            {
                file.IsArchivedDueToInactivity = true;
                file.ArchivedAt = archiveDate;
                file.ArchivedReason = defaultReason;
                file.UpdatedAt = archiveDate;
                file.UpdatedBy = "System";
            }

            await dbContext.SaveChangesAsync();
            return filesToArchive.Count;
        }
        
        public async Task<IEnumerable<ArchiveFile>> GetFilesForInactiveAdminsAsync(List<string> inactiveAdminUsernames)
        {
            if (!inactiveAdminUsernames.Any())
                return new List<ArchiveFile>();

            return await dbContext.ArchiveFiles
                .Include(f => f.Item)
                .Where(f => inactiveAdminUsernames.Contains(f.CreatedBy) && !f.IsArchivedDueToInactivity)
                .ToListAsync();
        }
        

    }
}