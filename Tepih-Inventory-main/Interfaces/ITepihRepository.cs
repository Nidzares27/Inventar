using Inventar.Models;

namespace Inventar.Interfaces
{
    public interface ITepihRepository
    {
        Task<IEnumerable<Tepih>> GetAll();
        Task<Tepih> GetByIdAsync(int id);
        Task<Tepih> GetByIdAsyncNoTracking(int id);
        bool Delete(Tepih tepih);
        bool Add(Tepih tepih);
        bool Update(Tepih tepih);
        bool Save();
    }
}
