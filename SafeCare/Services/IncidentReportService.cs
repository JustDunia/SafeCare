using Microsoft.EntityFrameworkCore;
using SafeCare.Data;
using SafeCare.Data.Entities;
using SafeCare.Dtos;
using SafeCare.Exceptions;

namespace SafeCare.Services
{
    public interface IIncidentReportService
    {
        Task<int> CreateReport(IncidentReportDto incidentReportDto, CancellationToken token = default);
    }

    public class IncidentReportService(IDbContextFactory<AppDbContext> dbContextFactory) : IIncidentReportService
    {
        private readonly IDbContextFactory<AppDbContext> _dbContextFactory = dbContextFactory;

        public async Task<int> CreateReport(IncidentReportDto incidentReportDto, CancellationToken token = default)
        {
            using var dbContext = _dbContextFactory.CreateDbContext();

            Department department = await dbContext.Departments
                .FirstOrDefaultAsync(x => x.Id == incidentReportDto.Department.Id, token)
                ?? throw new EntityNotFoundException(typeof(Department), incidentReportDto.Department.Id);

            List<IncidentDefinition> incidentDefinitions = await dbContext.IncidentDefinitions
                .Where(x => incidentReportDto.SelectedIncidentDefinitions.Select(y => y.Id).Contains(x.Id))
                .ToListAsync(token);

            if (incidentDefinitions.Count != incidentReportDto.SelectedIncidentDefinitions.Count)
            {
                var foundIds = incidentDefinitions.Select(x => x.Id);
                var missingIds = incidentReportDto.SelectedIncidentDefinitions
                    .Select(x => x.Id)
                    .Where(x => !foundIds.Contains(x));
                throw new EntityNotFoundException(typeof(IncidentDefinition), [.. missingIds]);
            }

            var incidentReport = new IncidentReport(
                incidentReportDto.Name,
                incidentReportDto.Surname,
                incidentReportDto.Phone,
                incidentReportDto.Email,
                incidentReportDto.PatientName,
                incidentReportDto.PatientSurname,
                incidentReportDto.PatientDob,
                incidentReportDto.PatientGender,
                incidentReportDto.DateFrom,
                incidentReportDto.DateTo,
                incidentReportDto.Date,
                department,
                incidentDefinitions,
                incidentReportDto.OtherIncidentDefinition,
                incidentReportDto.IncidentDescription
            );

            await dbContext.IncidentReports.AddAsync(incidentReport, token);
            await dbContext.SaveChangesAsync(token);

            return incidentReport.Id;
        }
    }
}
