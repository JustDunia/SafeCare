using SafeCare.Enums;

namespace SafeCare.Dtos
{
    public class IncidentReportDto
    {
        public string? Name { get; set; }
        public string? Surname { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? PatientName { get; set; }
        public string? PatientSurname { get; set; }
        public DateTime? PatientDob { get; set; }
        public Gender PatientGender { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public DateTime? Date { get; set; }
        public required DepartmentDto Department { get; set; }
        public IList<IncidentDefinitionDto> SelectedIncidentDefinitions { get; set; } = [];
        public string? OtherIncidentDefinition { get; set; }
        public required string IncidentDescription { get; set; }
    }
}
