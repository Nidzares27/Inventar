using Inventar.Data;
using Inventar.Interfaces;
using Inventar.Models;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Crypto.Macs;

namespace Inventar.Repository
{
    public class PlacanjeRepository : IPlacanjeRepository
    {
        private readonly ApplicationDbContext _context;

        public PlacanjeRepository(ApplicationDbContext context)
        {
            this._context = context;
        }
        public bool Add(Placanje placanje)
        {
            _context.Add(placanje);
            return Save();
        }

        public bool Delete(Placanje placanje)
        {
            _context?.Remove(placanje);
            return Save();
        }

        public async Task<IEnumerable<Placanje>> GetAll()
        {
            return await _context.Placanja.ToListAsync();
        }

        public async Task<IEnumerable<Placanje>> GetAllByNameAsync(string name)
        {
            return await _context.Placanja.Where(i => i.CustomerName == name).ToListAsync();
        }

        public async Task<Placanje> GetByIdAsync(int id)
        {
            return await _context.Placanja.FirstOrDefaultAsync(i => i.Id == id);
        }

        public async Task<Placanje> GetByIdAsyncNoTracking(int id)
        {
            return await _context.Placanja.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id);
        }

        public async Task<Placanje> GetByNameAsync(string name)
        {
            return await _context.Placanja.FirstOrDefaultAsync(i => i.CustomerName == name);
        }

        public bool Save()
        {
            var saved = _context.SaveChanges();
            return saved > 0 ? true : false;
        }

        public bool Update(Placanje placanje)
        {
            _context.Update(placanje);
            return Save();
        }
    }
}
