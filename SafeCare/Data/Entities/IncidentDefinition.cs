using SafeCare.Enums;

namespace SafeCare.Data.Entities
{
    public class IncidentDefinition
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public IncidentCategory Category { get; set; }

        public IList<IncidentReport> ReportsWithIncident { get; set; } = [];
    }
}
