using Microsoft.EntityFrameworkCore;
using SafeCare.Data.Entities;

namespace SafeCare.Data
{
    public class AppDbContext(IConfiguration config, DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<IncidentDefinition> IncidentDefinitions { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<IncidentReport> IncidentReports { get; set; }

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

            modelBuilder
                .Entity<IncidentReport>()
                .Property(ir => ir.Name)
                .HasMaxLength(50);

            modelBuilder
                .Entity<IncidentReport>()
                .Property(ir => ir.Surname)
                .HasMaxLength(50);

            modelBuilder
                .Entity<IncidentReport>()
                .Property(ir => ir.Phone)
                .HasMaxLength(15);

            modelBuilder
                .Entity<IncidentReport>()
                .Property(ir => ir.Email)
                .HasMaxLength(100);

            modelBuilder
                .Entity<IncidentReport>()
                .Property(ir => ir.PatientName)
                .HasMaxLength(50);

            modelBuilder
                .Entity<IncidentReport>()
                .Property(ir => ir.PatientSurname)
                .HasMaxLength(50);

            modelBuilder
                .Entity<IncidentReport>()
                .Property(ir => ir.PatientGender)
                .IsRequired()
                .HasConversion<string>();

            modelBuilder
                .Entity<IncidentReport>()
                .Property(ir => ir.IncidentDescription)
                .HasMaxLength(5000);

            modelBuilder
                .Entity<IncidentReport>()
                .Property(ir => ir.PatientDob)
                .HasColumnType("date");

            modelBuilder
                .Entity<IncidentReport>()
                .Property(ir => ir.OtherIncidentDefinition)
                .HasMaxLength(255);

            modelBuilder
                .Entity<IncidentReport>()
                .HasOne(ir => ir.Department)
                .WithMany()
                .HasForeignKey(ir => ir.DepartmentId)
                .IsRequired();

            modelBuilder
                .Entity<IncidentReport>()
                .HasMany(ir => ir.IncidentDefinitions)
                .WithMany(id => id.ReportsWithIncident);
        }
    }
}
