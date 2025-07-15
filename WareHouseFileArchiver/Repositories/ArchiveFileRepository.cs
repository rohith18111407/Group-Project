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

        public async Task<IEnumerable<ArchiveFile>> GetFilesByItemIdAsync(Guid itemId)
        {
            return await dbContext.ArchiveFiles
                .Include(f => f.Item)
                .Where(f => f.ItemId == itemId)
                .OrderByDescending(f => f.VersionNumber)
                .ToListAsync();
        }

        public async Task<ArchiveFile?> GetFileByNameAndVersionAsync(string fileName, int version)
        {
            return await dbContext.ArchiveFiles
                .Include(f => f.Item)
                .FirstOrDefaultAsync(f => f.FileName == fileName && f.VersionNumber == version);
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

        public async Task<IEnumerable<ArchiveFile>> GetAllFilesAsync()
        {
            return await dbContext.ArchiveFiles
                .Include(f => f.Item)
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


    }
}