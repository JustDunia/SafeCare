using Microsoft.EntityFrameworkCore;
using SafeCare.Data;
using SafeCare.Dtos;
using SafeCare.Mappings;

namespace SafeCare.Services
{
    /// <summary>
    /// Read access to the hospital ward dictionary that backs the "Miejsce zdarzenia"
    /// autocomplete on the public form.
    /// </summary>
    public interface IDepartmentService
    {
        /// <summary>
        /// Returns every ward. The dictionary is small and changes rarely, so the public form
        /// fetches it once per circuit and filters it in memory.
        /// </summary>
        public Task<IList<DepartmentDto>> GetAll();
    }

    /// <summary>
    /// Entity Framework implementation of <see cref="IDepartmentService"/>.
    /// </summary>
    public class DepartmentService(IDbContextFactory<AppDbContext> dbContextFactory) : IDepartmentService
    {
        private readonly IDbContextFactory<AppDbContext> _dbContextFactory = dbContextFactory;

        public async Task<IList<DepartmentDto>> GetAll()
        {
            using var dbContext = _dbContextFactory.CreateDbContext();
            return await dbContext.Departments
                .AsNoTracking()
                .Select(x => x.ToDto())
                .ToListAsync();
        }
    }
}
