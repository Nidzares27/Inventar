using Inventar.Data;
using Inventar.Interfaces;
using Inventar.Models;
using Microsoft.EntityFrameworkCore;

namespace Inventar.Repository
{
    public class TepihRepository : ITepihRepository
    {
        private readonly ApplicationDbContext _context;

        public TepihRepository(ApplicationDbContext context) 
        {
            this._context = context;
        }
        public bool Add(Tepih tepih)
        {
            _context.Add(tepih);
            return Save();
        }

        public bool Delete(Tepih tepih)
        {
            _context?.Remove(tepih);
            return Save();
        }

        public async Task<IEnumerable<Tepih>> GetAll()
        {
            return await _context.Tepisi.ToListAsync();
        }

        public async Task<Tepih> GetByIdAsync(int id)
        {
            return await _context.Tepisi.FirstOrDefaultAsync(i => i.Id == id);
        }

        public async Task<Tepih> GetByIdAsyncNoTracking(int id)
        {
            return await _context.Tepisi.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id);
        }

        public bool Save()
        {
            var saved = _context.SaveChanges();
            return saved > 0 ? true : false;
        }

        public bool Update(Tepih tepih)
        {
            _context.Update(tepih);
            return Save();
        }
    }
}
