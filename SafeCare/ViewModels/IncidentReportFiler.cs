using SafeCare.Enums;

namespace SafeCare.ViewModels
{
    public class IncidentReportFiler
    {
        public string? FullName { get; set; }
        public string? PatientFullName { get; set; }
        public Gender? Gender { get; set; }
        public string? Department { get; set; }
        public IEnumerable<IncidentCategory?> Categories { get; set; } = [];
        public IEnumerable<ReportStatus?> Statuses { get; set; } = [];
    }
}
