using Inventar.Models;

namespace Inventar.Interfaces
{
    public interface ITepihRepository
    {
        Task<IEnumerable<Tepih>> GetAll();
        Task<Tepih> GetByIdAsync(int id);
        Task<Tepih> GetByIdAsyncNoTracking(int id);
        Task<IEnumerable<Tepih>> GetAllUndisabledAsync();
        bool Delete(Tepih tepih);
        bool Add(Tepih tepih);
        bool Update(Tepih tepih);
        bool Save();
    }
}
