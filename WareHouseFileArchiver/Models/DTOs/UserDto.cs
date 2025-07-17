namespace WareHouseFileArchiver.Models.DTOs
{
    public class UserDto
    {
        public string Id { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public IList<string> Roles { get; set; } = new List<string>();

        public DateTime? LastLoginAt { get; set; }
        public string LastLoginFormatted { get; set; } = string.Empty;
        public string LoginStatus { get; set; } = string.Empty;
        public int? DaysSinceLastLogin { get; set; }
    }
}