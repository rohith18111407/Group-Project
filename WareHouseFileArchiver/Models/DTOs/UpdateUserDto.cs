using System.ComponentModel.DataAnnotations;

namespace WareHouseFileArchiver.Models.DTOs
{
    public class UpdateUserDto
    {
        [Required]
        [DataType(DataType.EmailAddress)]
        public string Username { get; set; } = string.Empty;
        public IList<string> Roles { get; set; } = new List<string>();
    }
}