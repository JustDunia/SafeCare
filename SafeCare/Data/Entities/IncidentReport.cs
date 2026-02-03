using SafeCare.Enums;
using SafeCare.Exceptions;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace SafeCare.Data.Entities
{
    [Display(Name = "Zgłoszenie zdarzenia")]
    public class IncidentReport
    {
        protected IncidentReport()
        {
        }

        [SetsRequiredMembers]
        public IncidentReport(
            string? name,
            string? surname,
            string? phone,
            string? email,
            string? patientName,
            string? patientSurname,
            DateTime? patientDob,
            Gender patientGender,
            DateTime? dateFrom,
            DateTime? dateTo,
            DateTime? date,
            Department department,
            IList<IncidentDefinition> incidentDefinitions,
            string? otherIncidentDefinition,
            string incidentDescription)
        {
            Name = name;
            Surname = surname;
            Phone = phone;
            Email = email;
            PatientName = patientName;
            PatientSurname = patientSurname;
            PatientDob = patientDob;
            PatientGender = patientGender;

            // Normalize dates to remove time component for dateFrom and dateTo
            dateFrom = dateFrom?.Date;
            dateTo = dateTo?.Date;

            ValidateDates(dateFrom, dateTo, date);
            DateFrom = dateFrom;
            DateTo = dateTo;
            Date = date;

            Department = department;
            IncidentDefinitions = incidentDefinitions;
            OtherIncidentDefinition = otherIncidentDefinition;
            IncidentDescription = incidentDescription;
        }

        public int Id { get; set; }
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
        public required Department Department { get; set; }
        public int DepartmentId { get; set; }
        public IList<IncidentDefinition> IncidentDefinitions { get; set; } = [];
        public string? OtherIncidentDefinition { get; set; }
        public required string IncidentDescription { get; set; }


        private void ValidateDates(
            DateTime? dateFrom,
            DateTime? dateTo,
            DateTime? date)
        {
            if ((!dateFrom.HasValue || !dateTo.HasValue) && !date.HasValue)
            {
                throw new DomainException("Należy podać zakres dat lub konkretną datę i godzinę.");
            }

            if (dateFrom.HasValue && dateTo.HasValue)
            {
                if (dateFrom > dateTo)
                {
                    throw new DomainException("Data początkowa nie może być późniejsza niż data końcowa.");
                }

                if (dateFrom > DateTime.UtcNow.Date || dateTo > DateTime.UtcNow.Date)
                {
                    throw new DomainException("Zakres dat nie może być w przyszłości.");
                }
            }
            else
            {
                if (date!.Value > DateTime.UtcNow)
                {
                    throw new DomainException("Konkretna data i godzina nie może być w przyszłości.");
                }
            }
        }
    }
}
