using System.ComponentModel.DataAnnotations;

namespace WareHouseFileArchiver.Models.DTOs
{
    public class CreateUserDto
    {
        [Required]
        [DataType(DataType.EmailAddress)]
        public string Username { get; set; } = string.Empty;
        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
        public IList<string> Roles { get; set; } = new List<string>();
    }
}