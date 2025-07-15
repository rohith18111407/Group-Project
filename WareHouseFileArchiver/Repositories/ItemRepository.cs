using Microsoft.EntityFrameworkCore;
using WareHouseFileArchiver.Data;
using WareHouseFileArchiver.Interfaces;
using WareHouseFileArchiver.Models.Domains;

namespace WareHouseFileArchiver.Repositories
{
    public class ItemRepository : IItemRepository
    {
        private readonly WareHouseDbContext dbContext;

        public ItemRepository(WareHouseDbContext dbContext)
        {
            this.dbContext = dbContext;
        }

        public async Task<Item> CreateAsync(Item item)
        {
            await dbContext.Items.AddAsync(item);
            await dbContext.SaveChangesAsync();
            return item;
        }

        public async Task DeleteAsync(Item item)
        {
            // Fetch related files
            var archiveFiles = await dbContext.ArchiveFiles
                .Where(f => f.ItemId == item.Id)
                .ToListAsync();

            // Delete physical files
            foreach (var file in archiveFiles)
            {
                if (System.IO.File.Exists(file.FilePath))
                {
                    try
                    {
                        System.IO.File.Delete(file.FilePath);
                    }
                    catch (Exception ex)
                    {
                        // Optionally log the error or ignore
                        Console.WriteLine($"Error deleting file: {file.FilePath}. Exception: {ex.Message}");
                    }
                }
                // Delete related download logs
                var logs = dbContext.FileDownloadLogs.Where(log => log.ArchiveFileId == file.Id);
                dbContext.FileDownloadLogs.RemoveRange(logs);
            }

            // Remove from DB
            dbContext.ArchiveFiles.RemoveRange(archiveFiles);
            dbContext.Items.Remove(item);
            await dbContext.SaveChangesAsync();
        }

        public async Task<List<Item>> GetAllAsync(string? sortBy, bool isDescending, int pageNumber, int pageSize)
        {
            var query = dbContext.Items.AsQueryable();

            if (!string.IsNullOrWhiteSpace(sortBy))
            {
                query = sortBy.ToLower() switch
                {
                    "name" => isDescending ? query.OrderByDescending(i => i.Name) : query.OrderBy(i => i.Name),
                    "createdat" => isDescending ? query.OrderByDescending(i => i.CreatedAt) : query.OrderBy(i => i.CreatedAt),
                    _ => query.OrderBy(i => i.Id)
                };
            }
            else
            {
                query = query.OrderBy(i => i.Id);
            }

            return await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<Item?> GetByIdAsync(Guid id)
        {
            return await dbContext.Items.FirstOrDefaultAsync(i => i.Id == id);
        }

        public async Task<int> GetTotalCountAsync()
        {
            return await dbContext.Items.CountAsync();
        }

        public async Task UpdateAsync(Item item)
        {
            dbContext.Items.Update(item);
            await dbContext.SaveChangesAsync();
        }

        public async Task<bool> ExistsAsync(string name, string category)
        {
            return await dbContext.Items
                .AnyAsync(i => i.Name.ToLower() == name.ToLower() && i.Category.ToLower() == category.ToLower());
        }
    }
}