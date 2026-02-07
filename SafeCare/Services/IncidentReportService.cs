using Microsoft.EntityFrameworkCore;
using SafeCare.Data;
using SafeCare.Data.Entities;
using SafeCare.Dtos;
using SafeCare.Enums;
using SafeCare.Exceptions;
using SafeCare.Utils;
using SafeCare.ViewModels;

namespace SafeCare.Services
{
    public interface IIncidentReportService
    {
        Task<int> CreateReport(IncidentReportDto incidentReportDto, CancellationToken token = default);
        Task<List<IncidentReportsGridVm>> GetReports(IncidentReportsRequestVm request, CancellationToken token = default);
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

        public async Task<List<IncidentReportsGridVm>> GetReports(IncidentReportsRequestVm request, CancellationToken token = default)
        {
            using var dbContext = _dbContextFactory.CreateDbContext();

            var result = await dbContext.IncidentReports.Select(x => new IncidentReportsGridVm
            {
                Id = x.Id,
                FullName = $"{x.Name} {x.Surname}",
                PatientFullName = $"{x.PatientName} {x.PatientSurname}",
                PatientAge = x.PatientDob.HasValue
                    ? Math.Round((DateTime.UtcNow - x.PatientDob.Value).TotalDays / 365.25, 1)
                    : null,
                PatientGender = x.PatientGender != Enums.Gender.NotProvided
                    ? x.PatientGender.GetDisplayName()
                    : null,
                Date = x.Date,
                DateFrom = x.DateFrom,
                DateTo = x.DateTo,
                Department = x.Department.Name,
                Categories = x.IncidentDefinitions
                        .Select(y => y.Category)
                        .Distinct()
                        .ToArray(),
                HasOtherCategory = !string.IsNullOrEmpty(x.OtherIncidentDefinition)
            })
            .OrderByDescending(x => x.Id)
            .ToListAsync(token);

            foreach (var item in result)
            {
                if (item.HasOtherCategory)
                {
                    item.Categories = [.. item.Categories, IncidentCategory.Other];
                }
            }

            return result;
        }
    }
}
