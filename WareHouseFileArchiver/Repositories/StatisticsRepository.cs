using Microsoft.EntityFrameworkCore;
using WareHouseFileArchiver.Data;
using WareHouseFileArchiver.Interfaces;
using WareHouseFileArchiver.Models.DTOs;

namespace WareHouseFileArchiver.Repositories
{
    public class StatisticsRepository : IStatisticsRepository
    {
        private readonly WareHouseDbContext dbContext;

        public StatisticsRepository(WareHouseDbContext dbContext)
        {
            this.dbContext = dbContext;
        }

        public async Task<IEnumerable<FileExtensionStatDto>> GetFileExtensionStatsAsync()
        {
            var groupedData = await dbContext.ArchiveFiles
                .GroupBy(f => f.FileExtension)
                .Select(g => new
                {
                    Extension = g.Key,
                    TotalSize = g.Sum(f => f.FileSizeInBytes),
                    Count = g.Count()
                })
                .ToListAsync();

            return groupedData.Select(g => new FileExtensionStatDto
            {
                Extension = g.Extension,
                TotalSizeMB = Math.Round(g.TotalSize / 1048576.0, 2),
                Count = g.Count
            });
        }

        public async Task<UploadDownloadTrendDto> GetUploadDownloadTrendsAsync()
        {
            DateTime fromDate = DateTime.UtcNow.AddDays(-6).Date;

            var uploads = await dbContext.ArchiveFiles
                .Where(f => f.CreatedAt >= fromDate)
                .GroupBy(f => f.CreatedAt.Date)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

            var downloads = await dbContext.FileDownloadLogs
                .Where(f => f.DownloadedAt >= fromDate && dbContext.ArchiveFiles.Any(a => a.Id == f.ArchiveFileId)) // keep valid file logs
                .GroupBy(f => f.DownloadedAt.Date)
                .ToDictionaryAsync(g => g.Key, g => g.Count());


            var days = Enumerable.Range(0, 7)
                .Select(i => fromDate.AddDays(i))
                .ToList();

            return new UploadDownloadTrendDto
            {
                Days = days.Select(d => d.ToString("MMM dd")).ToList(),
                Uploads = days.Select(d => uploads.ContainsKey(d) ? uploads[d] : 0).ToList(),
                Downloads = days.Select(d => downloads.ContainsKey(d) ? downloads[d] : 0).ToList()
            };
        }


        public async Task<IEnumerable<ActivityLogDto>> GetRecentActivitiesAsync(int count)
        {
            var uploads = await dbContext.ArchiveFiles
                .OrderByDescending(f => f.CreatedAt)
                .Take(count)
                .Select(f => new ActivityLogDto
                {
                    Action = "Uploaded",
                    Timestamp = f.CreatedAt,
                    User = f.CreatedBy,
                    Target = f.FileName + f.FileExtension
                }).ToListAsync();

            var downloads = await dbContext.FileDownloadLogs
                .Include(f => f.ArchiveFile)
                .Where(f => f.ArchiveFile != null)
                .OrderByDescending(f => f.DownloadedAt)
                .Take(count)
                .Select(f => new ActivityLogDto
                {
                    Action = "Downloaded",
                    Timestamp = f.DownloadedAt,
                    User = f.DownloadedBy,
                    Target = f.ArchiveFile!.FileName + f.ArchiveFile.FileExtension
                }).ToListAsync();

            return uploads.Concat(downloads)
                .OrderByDescending(a => a.Timestamp)
                .Take(count);
        }


        public async Task<IEnumerable<RecentFileDto>> GetRecentFilesAsync(int count)
        {
            var files = await dbContext.ArchiveFiles
                .Include(f => f.Item)
                .OrderByDescending(f => f.CreatedAt)
                .Take(count)
                .ToListAsync();

            return files.Select(f => new RecentFileDto
            {
                FileName = f.FileName + f.FileExtension,
                sizeMB = Math.Round(f.FileSizeInBytes / 1048576.0, 2),
                CreatedBy = f.CreatedBy,
                CreatedAt = f.CreatedAt
            });
        }

        public async Task<IEnumerable<RecentItemDto>> GetRecentItemsAsync(int count)
        {
            var items = await dbContext.Items
                .OrderByDescending(i => i.CreatedAt)
                .Take(count)
                .ToListAsync();

            return items.Select(i => new RecentItemDto
            {
                Name = i.Name,
                Categories = new List<string> { i.Category },
                CreatedBy = i.CreatedBy,
                CreatedAt = i.CreatedAt
            });
        }
    }
}
