using SafeCare.Dtos;
using SafeCare.Enums;

namespace SafeCare.ViewModels
{
    public class IncidentRegistrationFormVm
    {
        public string? Name { get; set; }
        public string? Surname { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? PatientName { get; set; }
        public string? PatientSurname { get; set; }
        public DateTime? PatientDob { get; set; }
        public Gender? PatientGender { get; set; }
        public bool IsDatePeriod { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public DateTime? Date { get; set; }
        public string? Time { get; set; }
        public DepartmentDto? Department { get; set; }
        public IList<IncidentDefinitionDto> SelectedIncidentDefinitions { get; set; } = [];
        public string? OtherIncidentDefinition { get; set; }
        public string? IncidentDescription { get; set; }
    }
}
