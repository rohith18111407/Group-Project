namespace WareHouseFileArchiver.Models.DTOs
{
    public class FileExtensionStatDto
    {
        public string Extension { get; set; } = "";
        public double TotalSizeMB { get; set; }
        public int Count { get; set; }
    }

    public class UploadDownloadTrendDto
    {
        public List<string> Days { get; set; } = new();
        public List<int> Uploads { get; set; } = new();
        public List<int> Downloads { get; set; } = new();
    }

    public class ActivityLogDto
    {
        public string Action { get; set; } = "";
        public string User { get; set; } = "";
        public string Target { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }

    public class RecentFileDto
    {
        public string FileName { get; set; } = "";
        public double sizeMB { get; set; }
        public string CreatedBy { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    public class RecentItemDto
    {
        public string Name { get; set; } = "";
        public List<string> Categories { get; set; } = new();
        public string CreatedBy { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    public class ArchivalStatisticsDto
    {
        public int TotalFiles { get; set; }
        public int ActiveFiles { get; set; }
        public int ArchivedFiles { get; set; }
        public double ArchivalPercentage { get; set; }
        public List<AdminArchivalStatDto> ArchivalsByAdmin { get; set; } = new();
        public List<DailyArchivalDto> RecentArchivalTrend { get; set; } = new();
    }

    public class AdminArchivalStatDto
    {
        public string AdminName { get; set; } = string.Empty;
        public int ArchivedFilesCount { get; set; }
        public double TotalSizeArchivedMB { get; set; }
        public DateTime? FirstArchivedDate { get; set; }
        public DateTime? LastArchivedDate { get; set; }
    }

    public class DailyArchivalDto
    {
        public DateTime Date { get; set; }
        public int ArchivedFilesCount { get; set; }
    }
}
