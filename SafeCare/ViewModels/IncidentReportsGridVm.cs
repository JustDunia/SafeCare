using SafeCare.Enums;

namespace SafeCare.ViewModels
{
    public class IncidentReportsGridVm
    {
        public int Id { get; set; }
        public string? FullName { get; set; }
        public string? PatientFullName { get; set; }
        public double? PatientAge { get; set; }
        public string? PatientGender { get; set; }
        public DateTime? Date { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public string Department { get; set; } = null!;
        public IncidentCategory[] Categories { get; set; } = [];
        public bool HasOtherCategory { get; set; }
    }
}
