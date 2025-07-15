using WareHouseFileArchiver.Models.DTOs;

namespace WareHouseFileArchiver.Interfaces
{
    public interface IStatisticsRepository
    {
        Task<IEnumerable<FileExtensionStatDto>> GetFileExtensionStatsAsync();
        Task<UploadDownloadTrendDto> GetUploadDownloadTrendsAsync();
        Task<IEnumerable<ActivityLogDto>> GetRecentActivitiesAsync(int count);
        Task<IEnumerable<RecentFileDto>> GetRecentFilesAsync(int count);
        Task<IEnumerable<RecentItemDto>> GetRecentItemsAsync(int count);
    }
}