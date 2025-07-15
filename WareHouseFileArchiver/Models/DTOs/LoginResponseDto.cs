// namespace WareHouseFileArchiver.Models.DTOs
// {
//     public class LoginResponseDto
//     {
//         public string JwtToken { get; set; }
//         public string RefreshToken { get; set; }
//     }
// }


namespace WareHouseFileArchiver.Models.DTOs
{
    public class LoginResponseDto
    {
        public string JwtToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTime JwtExpiryTime { get; set; }
    }
}
