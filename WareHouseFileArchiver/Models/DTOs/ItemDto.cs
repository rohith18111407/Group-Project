using WareHouseFileArchiver.Models.Domains;

namespace WareHouseFileArchiver.Models.DTOs
{
    public class ItemDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }
        public List<string> Categories { get; set; }

        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }
}