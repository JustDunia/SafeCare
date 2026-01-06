using SafeCare.Data.Entities;
using SafeCare.Dtos;

namespace SafeCare.Mappings
{
    public static class DepartmentMapping
    {
        extension(Department entity)
        {
            public DepartmentDto ToDto()
            {
                return new DepartmentDto
                {
                    Id = entity.Id,
                    Name = entity.Name,
                    Code = entity.Code
                };
            }
        }
    }
}
