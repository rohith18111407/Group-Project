using System.ComponentModel.DataAnnotations;

namespace WareHouseFileArchiver.Models.Domains
{
    public class Item
    {
        public Guid Id { get; set; }
        [Required]
        public string Name { get; set; }
        public string? Description { get; set; }
        [Required]
        public string Category { get; set; }
        // Audit Fields
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        // Navigation: One-to-Many
        public ICollection<ArchiveFile>? ArchiveFiles { get; set; }
    }
}