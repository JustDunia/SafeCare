using Microsoft.EntityFrameworkCore;
using SafeCare.Data;
using SafeCare.Dtos;
using SafeCare.Enums;
using SafeCare.Mappings;

namespace SafeCare.Services
{
    public interface IIncidentDefinitionService
    {
        public Task<IList<IncidentDefinitionDto>> GetAll();
        public Task<IList<IncidentDefinitionDto>> GetByCategory(IncidentCategory category);
    }

    public class IncidentDefinitionService(IDbContextFactory<AppDbContext> dbContextFactory) : IIncidentDefinitionService
    {
        public async Task<IList<IncidentDefinitionDto>> GetAll()
        {
            using var dbContext = dbContextFactory.CreateDbContext();
            return await dbContext.IncidentDefinitions
                .AsNoTracking()
                .Select(x => x.ToDto())
                .ToListAsync();
        }

        public async Task<IList<IncidentDefinitionDto>> GetByCategory(IncidentCategory category)
        {
            using var dbContext = dbContextFactory.CreateDbContext();
            return await dbContext.IncidentDefinitions
                .AsNoTracking()
                .Where(x => x.Category == category)
                .Select(x => x.ToDto())
                .ToListAsync();
        }
    }
}
