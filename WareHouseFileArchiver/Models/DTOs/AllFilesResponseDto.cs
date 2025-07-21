
using WareHouseFileArchiver.Models.Domains;

namespace WareHouseFileArchiver.Models.DTOs
{
    public class AllFilesResponseDto
    {
        public Guid Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FileExtension { get; set; } = string.Empty;
        public long FileSizeInBytes { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public int VersionNumber { get; set; }
        public string? Description { get; set; }
        public CategoryType Category { get; set; }
        public Guid? ItemId { get; set; }
        public string? ItemName { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        // Scheduled upload properties
        public bool IsScheduled { get; set; }
        public bool IsProcessed { get; set; }
    }

}
