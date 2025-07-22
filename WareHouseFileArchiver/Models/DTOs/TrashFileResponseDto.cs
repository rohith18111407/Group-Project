using WareHouseFileArchiver.Models.Domains;

namespace WareHouseFileArchiver.Models.DTOs
{
    public class TrashFileResponseDto
    {
        public Guid Id { get; set; }
        public string FileName { get; set; }
        public string FileExtension { get; set; }
        public long FileSizeInBytes { get; set; }
        public int VersionNumber { get; set; }
        public string? Description { get; set; }
        public CategoryType Category { get; set; }
        public Guid? ItemId { get; set; }
        public string? ItemName { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }
        public int DaysInTrash { get; set; }
        public int DaysRemaining { get; set; }
        public bool CanRestore { get; set; }
    }

    public class TrashStatsDto
    {
        public int TotalTrashedFiles { get; set; }
        public int FilesExpiringSoon { get; set; } // Files expiring in next 24 hours
        public long TotalSizeInBytes { get; set; }
        public DateTime? OldestFileDate { get; set; }
    }
}
