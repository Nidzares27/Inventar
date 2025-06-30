using Inventar.Models;

namespace Inventar.Interfaces
{
    public interface ISalesRepository
    {
        Task<IEnumerable<Prodaja>> GetAll();
        Task<Prodaja> GetByIdAsync(int id);
        Task<Prodaja> GetByIdAsyncNoTracking(int id);
        Task<IEnumerable<Prodaja>> GetAllByNameAsync(string name);
        Task<List<Prodaja>> GetAllWithTepih();
        bool Delete(Prodaja prodaja);
        bool Add(Prodaja prodaja);
        bool Update(Prodaja prodaja);
        bool Save();
    }
}
