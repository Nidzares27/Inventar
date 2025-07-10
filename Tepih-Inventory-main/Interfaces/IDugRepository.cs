using Inventar.Models;

namespace Inventar.Interfaces
{
    public interface IDugRepository
    {
        Task<IEnumerable<Dug>> GetAll();
        Task<Dug> GetByIdAsync(int id);
        Task<Dug> GetByIdAsyncNoTracking(int id);
        Task<Dug> GetByNameAsync(string name);
        Task<IEnumerable<Dug>> GetAllByNameAsync(string name);
        bool Delete(Dug dug);
        bool Add(Dug dug);
        bool Update(Dug dug);
        bool Save();
    }
}
