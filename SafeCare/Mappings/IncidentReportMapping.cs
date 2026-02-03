using SafeCare.Dtos;
using SafeCare.Services;
using SafeCare.ViewModels;

namespace SafeCare.Mappings
{
    public static class IncidentReportMapping
    {
        extension(IncidentRegistrationFormVm vm)
        {
            public async Task<IncidentReportDto> ToDtoAsync(ITimezoneService timezoneService)
            {
                var timezoneOffset = await timezoneService.GetUserTimezoneOffsetAsync();

                DateTime? dateFromUtc = null;
                DateTime? dateToUtc = null;
                DateTime? dateUtc = null;
                TimeOnly? time = null;

                if (vm.IsDatePeriod)
                {
                    if (vm.DateFrom.HasValue)
                    {
                        dateFromUtc = timezoneService.ConvertLocalToUtc(DateOnly.FromDateTime(vm.DateFrom.Value));
                    }

                    if (vm.DateTo.HasValue)
                    {
                        dateToUtc = timezoneService.ConvertLocalToUtc(DateOnly.FromDateTime(vm.DateTo.Value));
                    }
                }
                else
                {
                    if (vm.Date.HasValue && !string.IsNullOrWhiteSpace(vm.Time))
                    {
                        time = TimeOnly.TryParse(vm.Time, out var parsedTime)
                            ? parsedTime
                            : throw new ArgumentException("Nieprawidłowy format czasu", nameof(vm.Time));

                        dateUtc = timezoneService.ConvertLocalToUtc(
                            DateOnly.FromDateTime(vm.Date.Value),
                            time.Value,
                            timezoneOffset);
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
                    DateFrom = dateFromUtc,
                    DateTo = dateToUtc,
                    Date = dateUtc,
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
