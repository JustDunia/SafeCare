using SafeCare.Enums;

namespace SafeCare.Dtos
{
    public class IncidentDefinitionDto
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public IncidentCategory Category { get; set; }
    }
}
