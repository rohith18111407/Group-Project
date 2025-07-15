using WareHouseFileArchiver.Models.DTOs;

namespace WareHouseFileArchiver.Interfaces
{
    public interface IUserRepository
    {
        Task<List<UserDto>> GetAllAsync(int pageNumber, int pageSize,
                                        string? roleFilter,
                                        string? sortBy, string? sortOrder);
        Task<UserDto?> GetByIdAsync(string id);
        Task<UserDto?> CreateAsync(CreateUserDto dto);
        Task<UserDto?> UpdateAsync(string id, UpdateUserDto dto);
        Task<UserDto?> DeleteAsync(string id);
    }
}