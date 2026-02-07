using SafeCare.Dtos;
using SafeCare.ViewModels;

namespace SafeCare.Mappings
{
    public static class IncidentReportMapping
    {
        extension(IncidentRegistrationFormVm vm)
        {
            public IncidentReportDto ToDto()
            {
                DateTime? dateFrom = null;
                DateTime? dateTo = null;
                DateTime? date = null;

                if (vm.IsDatePeriod)
                {
                    if (vm.DateFrom.HasValue)
                    {
                        dateFrom = vm.DateFrom.Value.Date;
                    }

                    if (vm.DateTo.HasValue)
                    {
                        dateTo = vm.DateTo.Value.Date;
                    }
                }
                else
                {
                    if (vm.Date.HasValue && !string.IsNullOrWhiteSpace(vm.Time))
                    {
                        var time = TimeOnly.TryParse(vm.Time, out var parsedTime)
                            ? parsedTime
                            : throw new ArgumentException("Nieprawidłowy format czasu", nameof(vm.Time));

                        date = vm.Date.Value.Date.Add(time.ToTimeSpan());
                    }
                }

                return new IncidentReportDto
                {
                    Name = vm.Name,
                    Surname = vm.Surname,
                    Phone = vm.Phone,
                    Email = vm.Email,
                    PatientName = vm.PatientName,
                    PatientSurname = vm.PatientSurname,
                    PatientDob = vm.PatientDob,
                    PatientGender = vm.PatientGender
                        ?? Enums.Gender.NotProvided,
                    DateFrom = dateFrom,
                    DateTo = dateTo,
                    Date = date,
                    Department = vm.Department
                        ?? throw new ArgumentNullException(nameof(IncidentReportDto.Department)),
                    SelectedIncidentDefinitions = vm.SelectedIncidentDefinitions
                        ?? [],
                    OtherIncidentDefinition = vm.OtherIncidentDefinition,
                    IncidentDescription = !string.IsNullOrWhiteSpace(vm.IncidentDescription)
                        ? vm.IncidentDescription
                        : throw new ArgumentNullException(nameof(IncidentReportDto.IncidentDescription))
                };
            }
        }
    }
}
