using System.ComponentModel.DataAnnotations;

namespace WareHouseFileArchiver.Models.DTOs
{
    public class BulkDownloadRequestDto
    {
        [Required]
        [MinLength(1, ErrorMessage = "At least one file must be selected")]
        public List<Guid> FileIds { get; set; } = new();

        [MaxLength(100, ErrorMessage = "ZIP file name cannot exceed 100 characters")]
        public string? ZipFileName { get; set; }
    }
}