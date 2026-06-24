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

        public async Task<IEnumerable<Tepih>> GetAllUndisabledAsync()
        {
            return await _context.Tepisi
                .Where(i => i.Disabled == false && !i.CreatedForDirectSale)
                .ToListAsync();
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
            if (tepih.RowVersion == null)
            {
                tepih.RowVersion = _context.Tepisi
                    .AsNoTracking()
                    .Where(x => x.Id == tepih.Id)
                    .Select(x => x.RowVersion)
                    .FirstOrDefault();
            }

            _context.Update(tepih);
            if (tepih.RowVersion != null)
            {
                _context.Entry(tepih).Property(x => x.RowVersion).OriginalValue = tepih.RowVersion;
            }
            return Save();
        }
    }
}
