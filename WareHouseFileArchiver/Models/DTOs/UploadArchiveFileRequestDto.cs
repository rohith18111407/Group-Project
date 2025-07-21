using System.ComponentModel.DataAnnotations;
using WareHouseFileArchiver.Models.Domains;

namespace WareHouseFileArchiver.Models.DTOs
{
    public class UploadArchiveFileRequestDto
    {
        [Required]
        public IFormFile File { get; set; }

        [Required]
        public Guid ItemId { get; set; }

        public string? Description { get; set; }

        [Required]
        [EnumDataType(typeof(CategoryType))]
        public CategoryType Category { get; set; }

        // Fields for scheduled update
        [FutureDate(ErrorMessage = "Scheduled upload date must be in the future.")]
        public DateTime? ScheduledUploadDate { get; set; }
    }

    // Custom validation attribute
    public class FutureDateAttribute : ValidationAttribute
    {
        public override bool IsValid(object? value)
        {
            if (value == null) return true; // Not required
            if (value is DateTime dateTime)
            {
                return dateTime > DateTime.UtcNow;
            }
            return false;
        }
    }

}