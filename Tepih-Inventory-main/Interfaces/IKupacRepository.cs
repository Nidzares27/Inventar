using Inventar.Models;

namespace Inventar.Interfaces
{
    public interface IKupacRepository
    {
        Task<IEnumerable<Kupac>> GetAll();
        Task<Kupac> GetByIdAsync(int id);
        Task<Kupac> GetByIdAsyncNoTracking(int id);
        Task<Kupac> GetByNameAsync(string name);
        bool Delete(Kupac kupac);
        bool Add(Kupac kupac);
        bool Update(Kupac kupac);
        bool Save();
    }
}
