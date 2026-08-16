using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SafeCare.Data.Entities;

namespace SafeCare.Data
{
    /// <summary>
    /// The application database context: ASP.NET Core Identity tables plus the three domain
    /// tables of the reporting module.
    /// </summary>
    /// <remarks>
    /// Always resolve this through <see cref="IDbContextFactory{TContext}"/> and dispose it
    /// promptly. Injecting the context directly into a component or scoped service is unsafe
    /// under Blazor Server, where one circuit outlives many overlapping operations.
    /// Entity configuration lives in an <c>IEntityTypeConfiguration&lt;T&gt;</c> class inside
    /// each entity's own file and is discovered by assembly scan.
    /// </remarks>
    public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<User, IdentityRole<Guid>, Guid>(options)
    {
        public DbSet<IncidentDefinition> IncidentDefinitions { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<IncidentReport> IncidentReports { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        }
    }
}
