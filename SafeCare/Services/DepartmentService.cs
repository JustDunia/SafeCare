using Microsoft.EntityFrameworkCore;
using SafeCare.Data;
using SafeCare.Dtos;
using SafeCare.Mappings;

namespace SafeCare.Services
{
    public interface IDepartmentService
    {
        public Task<IList<DepartmentDto>> GetAll();
    }

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
