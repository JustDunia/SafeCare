using Microsoft.EntityFrameworkCore;
using SafeCare.Data;
using SafeCare.Dtos;
using SafeCare.Enums;
using SafeCare.Mappings;

namespace SafeCare.Services
{
    /// <summary>
    /// Read access to the dictionary of predefined adverse events (for example "niewłaściwa
    /// identyfikacja pacjenta"), grouped into the categories of <see cref="IncidentCategory"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="IncidentCategory.Other"/> has no rows here. It represents the free-text
    /// description a reporter can type instead of picking from the dictionary, which is stored
    /// on the report itself as <c>OtherIncidentDefinition</c>.
    /// </remarks>
    public interface IIncidentDefinitionService
    {
        /// <summary>
        /// Returns every definition. The public form loads the whole dictionary once and groups
        /// it into expansion panels client-side.
        /// </summary>
        public Task<IList<IncidentDefinitionDto>> GetAll();

        /// <summary>
        /// Returns only the definitions belonging to a single category.
        /// </summary>
        public Task<IList<IncidentDefinitionDto>> GetByCategory(IncidentCategory category);
    }

    /// <summary>
    /// Entity Framework implementation of <see cref="IIncidentDefinitionService"/>.
    /// </summary>
    public class IncidentDefinitionService(IDbContextFactory<AppDbContext> dbContextFactory) : IIncidentDefinitionService
    {
        private readonly IDbContextFactory<AppDbContext> _dbContextFactory = dbContextFactory;

        public async Task<IList<IncidentDefinitionDto>> GetAll()
        {
            using var dbContext = _dbContextFactory.CreateDbContext();
            return await dbContext.IncidentDefinitions
                .AsNoTracking()
                .Select(x => x.ToDto())
                .ToListAsync();
        }

        public async Task<IList<IncidentDefinitionDto>> GetByCategory(IncidentCategory category)
        {
            using var dbContext = _dbContextFactory.CreateDbContext();
            return await dbContext.IncidentDefinitions
                .AsNoTracking()
                .Where(x => x.Category == category)
                .Select(x => x.ToDto())
                .ToListAsync();
        }
    }
}
