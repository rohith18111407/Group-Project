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
    }

}