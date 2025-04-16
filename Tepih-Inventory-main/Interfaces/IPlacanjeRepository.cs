using Inventar.Models;

namespace Inventar.Interfaces
{
    public interface IPlacanjeRepository
    {
        Task<IEnumerable<Placanje>> GetAll();
        Task<Placanje> GetByIdAsync(int id);
        Task<Placanje> GetByIdAsyncNoTracking(int id);
        Task<IEnumerable<Placanje>> GetAllByNameAsync(string name);
        bool Delete(Placanje placanje);
        bool Add(Placanje placanje);
        bool Update(Placanje placanje);
        bool Save();
    }
}
