using Inventar.Data;
using Inventar.Interfaces;
using Inventar.Models;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Crypto.Macs;

namespace Inventar.Repository
{
    
    public class DugRepository : IDugRepository
    {
        private readonly ApplicationDbContext _context;

        public DugRepository(ApplicationDbContext context)
        {
            this._context = context;
        }
        public bool Add(Dug dug)
        {
            _context.Add(dug);
            return Save();
        }

        public bool Delete(Dug dug)
        {
            _context?.Remove(dug);
            return Save();
        }

        public async Task<IEnumerable<Dug>> GetAll()
        {
            return await _context.Dugovanja.ToListAsync();
        }

        public async Task<IEnumerable<Dug>> GetAllByNameAsync(string name)
        {
            return await _context.Dugovanja.Where(i => i.CustomerFullName == name).ToListAsync();
        }

        public async Task<Dug> GetByIdAsync(int id)
        {
            return await _context.Dugovanja.FirstOrDefaultAsync(i => i.Id == id);
        }

        public async Task<Dug> GetByIdAsyncNoTracking(int id)
        {
            return await _context.Dugovanja.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id);
        }

        public async Task<Dug> GetByNameAsync(string name)
        {
            return await _context.Dugovanja.FirstOrDefaultAsync(i => i.CustomerFullName == name);
        }

        public bool Save()
        {
            var saved = _context.SaveChanges();
            return saved > 0 ? true : false;
        }

        public bool Update(Dug dug)
        {
            _context.Update(dug);
            return Save();
        }
    }
}
