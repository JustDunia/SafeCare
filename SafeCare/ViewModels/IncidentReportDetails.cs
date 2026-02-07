using SafeCare.Enums;

namespace SafeCare.ViewModels
{
    public class IncidentReportDetails
    {
        public DateTime CreatedAt { get; set; }
        public string? Name { get; set; }
        public string? Surname { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? PatientName { get; set; }
        public string? PatientSurname { get; set; }
        public DateTime? PatientDob { get; set; }
        public string PatientGender { get; set; } = null!;
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public DateTime? Date { get; set; }
        public string Department { get; set; } = null!;
        public IList<IncidentReportDetailsItem> Incidents { get; set; } = [];
        public string Description { get; set; } = null!;
        public ReportStatus Status { get; set; }
    }

    public class IncidentReportDetailsItem
    {
        public string Category { get; set; } = null!;
        public string Name { get; set; } = null!;
    }
}
