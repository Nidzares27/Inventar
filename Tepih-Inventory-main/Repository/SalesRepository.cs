using Inventar.Data;
using Inventar.Interfaces;
using Inventar.Models;
using Microsoft.EntityFrameworkCore;

namespace Inventar.Repository
{
    public class SalesRepository : ISalesRepository
    {
        private readonly ApplicationDbContext _context;

        public SalesRepository(ApplicationDbContext context)
        {
            this._context = context;
        }
        public bool Add(Prodaja prodaja)
        {
            _context.Add(prodaja);
            return Save();
        }

        public bool Delete(Prodaja prodaja)
        {
            _context?.Remove(prodaja);
            return Save();
        }

        public async Task<IEnumerable<Prodaja>> GetAll()
        {
            return await _context.Prodaje.ToListAsync();
        }

        public async Task<IEnumerable<Prodaja>> GetAllByNameAsync(string name)
        {
            return await _context.Prodaje.Where(i => i.CustomerFullName == name).ToListAsync();
        }

        public async Task<List<Prodaja>> GetAllWithTepih()
        {
            return await _context.Prodaje.Include(p => p.Tepih).ToListAsync();
        }

        public async Task<Prodaja> GetByIdAsync(int id)
        {
            return await _context.Prodaje.FirstOrDefaultAsync(i => i.Id == id);
        }

        public async Task<Prodaja> GetByIdAsyncNoTracking(int id)
        {
            return await _context.Prodaje.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id);
        }

        public bool Save()
        {
            var saved = _context.SaveChanges();
            return saved > 0 ? true : false;
        }

        public bool Update(Prodaja prodaja)
        {
            _context.Update(prodaja);
            return Save();
        }
    }
}
