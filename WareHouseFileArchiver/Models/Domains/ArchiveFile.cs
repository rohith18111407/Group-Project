using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WareHouseFileArchiver.Models.Domains
{
    public class ArchiveFile
    {
        public Guid Id { get; set; }

        [Required]
        public string FileName { get; set; }

        public CategoryType Category { get; set; }

        public string FileExtension { get; set; }

        public long FileSizeInBytes { get; set; }

        public string FilePath { get; set; }

        public int VersionNumber { get; set; }

        public string? Description { get; set; }

        public Guid? ItemId { get; set; }
        public Item? Item { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        // Fields for scheduled update
        public bool IsScheduled { get; set; } = false;
        public bool IsProcessed { get; set; } = false; // For scheduled files

        // Fields for trash/soft delete functionality
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }
        public string? OriginalFilePath { get; set; } // Store original path before moving to trash

        [NotMapped]
        public IFormFile? File { get; set; }
    }
}