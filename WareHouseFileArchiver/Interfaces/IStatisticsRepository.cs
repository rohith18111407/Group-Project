using WareHouseFileArchiver.Models.DTOs;

namespace WareHouseFileArchiver.Interfaces
{
    public interface IStatisticsRepository
    {
        // Updated existing methods with optional includeArchived parameter
        Task<IEnumerable<FileExtensionStatDto>> GetFileExtensionStatsAsync(bool includeArchived = false);
        Task<UploadDownloadTrendDto> GetUploadDownloadTrendsAsync();
        Task<IEnumerable<ActivityLogDto>> GetRecentActivitiesAsync(int count);
        Task<IEnumerable<RecentFileDto>> GetRecentFilesAsync(int count, bool includeArchived = false);

        // Existing method unchanged (Items are not archived)
        Task<IEnumerable<RecentItemDto>> GetRecentItemsAsync(int count);

        // NEW methods for archival functionality    
    }
}