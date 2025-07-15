using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WareHouseFileArchiver.Interfaces;

namespace WareHouseFileArchiver.Controllers
{
    [ApiController]
    [Route("api/v1/statistics")]
    [Authorize(Roles = "Admin,Staff")]
    public class StatisticsController : ControllerBase
    {
        private readonly IStatisticsRepository statisticsRepository;

        public StatisticsController(IStatisticsRepository statisticsRepository)
        {
            this.statisticsRepository = statisticsRepository;
        }

        [HttpGet("file-extension-stats")]
        public async Task<IActionResult> GetFileExtensionStats()
        {
            var result = await statisticsRepository.GetFileExtensionStatsAsync();
            return Ok(result);
        }

        [HttpGet("upload-download-trends")]
        public async Task<IActionResult> GetUploadDownloadTrends()
        {
            var result = await statisticsRepository.GetUploadDownloadTrendsAsync();
            return Ok(result);
        }

        [HttpGet("recent-activities")]
        public async Task<IActionResult> GetRecentActivities([FromQuery] int count = 10)
        {
            var result = await statisticsRepository.GetRecentActivitiesAsync(count);
            return Ok(result);
        }

        [HttpGet("recent-files")]
        public async Task<IActionResult> GetRecentFiles([FromQuery] int count = 5)
        {
            var result = await statisticsRepository.GetRecentFilesAsync(count);
            return Ok(result);
        }

        [HttpGet("recent-items")]
        public async Task<IActionResult> GetRecentItems([FromQuery] int count = 5)
        {
            var result = await statisticsRepository.GetRecentItemsAsync(count);
            return Ok(result);
        }
    }
}