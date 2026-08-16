using SafeCare.Enums;

namespace SafeCare.ViewModels
{
    public class IncidentReportFilter
    {
        public string? FullName { get; set; }
        public string? PatientFullName { get; set; }
        public Gender? Gender { get; set; }
        public string? Department { get; set; }
        public IReadOnlyCollection<IncidentCategory?> Categories { get; set; } = [];
        public IReadOnlyCollection<ReportStatus?> Statuses { get; set; } = [];
    }
}
