using Inventar.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Inventar.Data
{
    public class ApplicationDbContext : IdentityDbContext<AppUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : base(options)
        {
        }
        public DbSet<Tepih> Tepisi { get; set; }
        public DbSet<Prodaja> Prodaje { get; set; }
        public DbSet<Kupac> Kupci { get; set; }
        public DbSet<Placanje> Placanja { get; set; }
    }
}