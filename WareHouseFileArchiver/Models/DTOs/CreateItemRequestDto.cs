using System.ComponentModel.DataAnnotations;
using WareHouseFileArchiver.Models.Domains;

namespace WareHouseFileArchiver.Models.DTOs
{
    public class CreateItemRequestDto
    {
        public string Name { get; set; }
        public string? Description { get; set; }
        public List<string> Categories { get; set; }
    }
}