using WareHouseFileArchiver.Models.Domains;

namespace WareHouseFileArchiver.Interfaces
{
    public interface IItemRepository
    {
        Task<Item> CreateAsync(Item item);
        Task DeleteAsync(Item item);
        Task<List<Item>> GetAllAsync(string? sortBy, bool isDescending, int pageNumber, int pageSize);
        Task<Item?> GetByIdAsync(Guid id);
        Task<int> GetTotalCountAsync();
        Task UpdateAsync(Item item);
        Task<bool> ExistsAsync(string name, string category);
    }
}