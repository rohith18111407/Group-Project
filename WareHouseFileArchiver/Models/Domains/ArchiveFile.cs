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

        [NotMapped]
        public IFormFile? File { get; set; }
    }
}