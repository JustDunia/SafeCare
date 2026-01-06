using Microsoft.EntityFrameworkCore;
using SafeCare.Data.Entities;

namespace SafeCare.Data
{
    public class AppDbContext(IConfiguration config, DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<IncidentDefinition> IncidentDefinitions { get; set; }
        public DbSet<Department> Departments { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options.UseNpgsql(config.GetConnectionString("DefaultConnection"));
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder
                .Entity<IncidentDefinition>()
                .Property(e => e.Category)
                .HasConversion<string>()
                .HasMaxLength(30);

            modelBuilder
                .Entity<IncidentDefinition>()
                .Property(e => e.Name)
                .HasMaxLength(255);

            modelBuilder
                .Entity<Department>()
                .Property(d => d.Name)
                .HasMaxLength(255);

            modelBuilder
                .Entity<Department>()
                .Property(d => d.Code)
                .HasMaxLength(10);
        }
    }
}
