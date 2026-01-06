using SafeCare.Data.Entities;
using SafeCare.Dtos;

namespace SafeCare.Mappings
{
    public static class IncidentDefinitionMapping
    {
        extension(IncidentDefinition entity)
        {
            public IncidentDefinitionDto ToDto()
            {
                return new IncidentDefinitionDto
                {
                    Id = entity.Id,
                    Name = entity.Name,
                    Category = entity.Category
                };
            }
        }
    }
}
