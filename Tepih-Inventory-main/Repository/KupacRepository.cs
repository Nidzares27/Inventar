using Inventar.Data;
using Inventar.Interfaces;
using Inventar.Models;
using Microsoft.EntityFrameworkCore;
using static iText.StyledXmlParser.Jsoup.Select.Evaluator;

namespace Inventar.Repository
{
    public class KupacRepository : IKupacRepository
    {
        private readonly ApplicationDbContext _context;

        public KupacRepository(ApplicationDbContext context)
        {
            this._context = context;
        }
        public bool Add(Kupac kupac)
        {
            _context.Add(kupac);
            return Save();
        }

        public bool Delete(Kupac kupac)
        {
            _context?.Remove(kupac);
            return Save();
        }

        public async Task<IEnumerable<Kupac>> GetAll()
        {
            return await _context.Kupci.ToListAsync();
        }

        public async Task<Kupac> GetByIdAsync(int id)
        {
            return await _context.Kupci.FirstOrDefaultAsync(i => i.Id == id);
        }

        public async Task<Kupac> GetByIdAsyncNoTracking(int id)
        {
            return await _context.Kupci.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id);
        }

        public async Task<Kupac> GetByNameAsync(string name)
        {
            return await _context.Kupci.FirstOrDefaultAsync(i => i.CustomerFullName == name);
        }

        public bool Save()
        {
            var saved = _context.SaveChanges();
            return saved > 0 ? true : false;
        }

        public bool Update(Kupac kupac)
        {
            _context.Update(kupac);
            return Save();
        }
    }
}
