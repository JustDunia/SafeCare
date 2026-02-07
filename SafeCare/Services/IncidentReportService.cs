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
        Task<IncidentReportsGridVm> GetReports(IncidentReportsRequestVm request, CancellationToken token = default);
        Task<IncidentReportDetails> GetReportDetails(int id, CancellationToken token = default);
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

        public async Task<IncidentReportDetails> GetReportDetails(int id, CancellationToken token = default)
        {
            using var dbContext = _dbContextFactory.CreateDbContext();

            var report = await dbContext.IncidentReports
                .Include(x => x.Department)
                .Include(x => x.IncidentDefinitions)
                .FirstOrDefaultAsync(x => x.Id == id, token)
                ?? throw new DomainException($"Entity of type {nameof(IncidentReport)} with Id {id} not found");

            var model = new IncidentReportDetails
            {
                CreatedAt = report.CreatedAt,
                Name = report.Name,
                Surname = report.Surname,
                Phone = report.Phone,
                Email = report.Email,
                PatientName = report.PatientName,
                PatientSurname = report.PatientSurname,
                PatientDob = report.PatientDob,
                PatientGender = report.PatientGender.GetDisplayName(),
                DateFrom = report.DateFrom,
                DateTo = report.DateTo,
                Date = report.Date,
                Department = report.Department.Name,
                Incidents = report.IncidentDefinitions.Select(i => new IncidentReportDetailsItem
                {
                    Category = i.Category.GetDisplayName(),
                    Name = i.Name
                }).ToList(),
                Description = report.IncidentDescription
            };

            if (!string.IsNullOrEmpty(report.OtherIncidentDefinition))
            {
                model.Incidents.Add(new IncidentReportDetailsItem
                {
                    Category = IncidentCategory.Other.GetDisplayName(),
                    Name = report.OtherIncidentDefinition
                });
            }

            return model;
        }

        public async Task<IncidentReportsGridVm> GetReports(IncidentReportsRequestVm request, CancellationToken token = default)
        {
            using var dbContext = _dbContextFactory.CreateDbContext();

            var sortDef = request.SortDefinitions.FirstOrDefault();
            var sortItem = sortDef?.SortBy;
            var isDescending = !sortDef?.Descending ?? false;

            IQueryable<IncidentReport> mainQuery = dbContext.IncidentReports;

            if (request.Filter.FullName is not null)
            {
                mainQuery = mainQuery.Where(x => (x.Name + " " + x.Surname).ToLower().Contains(request.Filter.FullName.ToLower()));
            }

            if (request.Filter.PatientFullName is not null)
            {
                mainQuery = mainQuery.Where(x => (x.PatientName + " " + x.PatientSurname).ToLower().Contains(request.Filter.PatientFullName.ToLower()));
            }

            if (request.Filter.Gender is not null)
            {
                mainQuery = mainQuery.Where(x => x.PatientGender == request.Filter.Gender);
            }

            if (request.Filter.Department is not null)
            {
                mainQuery = mainQuery.Where(x => x.Department.Name.ToLower().Contains(request.Filter.Department.ToLower()));
            }

            if (request.Filter.Categories is not null && request.Filter.Categories.Any())
            {
                mainQuery = mainQuery.Where(x => x.IncidentDefinitions.Select(i => i.Category).Any(c => request.Filter.Categories.Contains(c)));
            }

            var totalItems = await mainQuery.CountAsync(token);

            IOrderedQueryable<IncidentReport> sortedQuery = mainQuery.OrderByDescending(x => x.Id);

            if (sortItem is not null)
            {
                if (isDescending)
                {
                    sortedQuery = sortItem switch
                    {
                        nameof(IncidentReportsGridItem.Id) => mainQuery.OrderByDescending(x => x.Id),
                        nameof(IncidentReportsGridItem.FullName) => mainQuery.OrderByDescending(x => x.Surname).ThenByDescending(x => x.Name),
                        nameof(IncidentReportsGridItem.PatientFullName) => mainQuery.OrderByDescending(x => x.PatientSurname).ThenByDescending(x => x.PatientName),
                        nameof(IncidentReportsGridItem.PatientAge) => mainQuery.OrderBy(x => x.PatientDob),
                        nameof(IncidentReportsGridItem.PatientGender) => mainQuery.OrderByDescending(x => x.PatientGender),
                        nameof(IncidentReportsGridItem.Date) => mainQuery.OrderByDescending(x => x.Date)
                                                                     .ThenByDescending(x => x.DateFrom),
                        nameof(IncidentReportsGridItem.Department) => mainQuery.OrderByDescending(x => x.Department),
                        _ => mainQuery.OrderByDescending(x => x.Id),
                    };
                }
                else
                {
                    sortedQuery = sortItem switch
                    {
                        nameof(IncidentReportsGridItem.Id) => mainQuery.OrderBy(x => x.Id),
                        nameof(IncidentReportsGridItem.FullName) => mainQuery.OrderBy(x => x.Surname).ThenBy(x => x.Name),
                        nameof(IncidentReportsGridItem.PatientFullName) => mainQuery.OrderBy(x => x.PatientSurname).ThenBy(x => x.PatientName),
                        nameof(IncidentReportsGridItem.PatientAge) => mainQuery.OrderByDescending(x => x.PatientDob),
                        nameof(IncidentReportsGridItem.PatientGender) => mainQuery.OrderBy(x => x.PatientGender),
                        nameof(IncidentReportsGridItem.Date) => mainQuery.OrderBy(x => x.Date)
                                                                     .ThenBy(x => x.DateFrom),
                        nameof(IncidentReportsGridItem.Department) => mainQuery.OrderBy(x => x.Department),
                        _ => mainQuery.OrderBy(x => x.Id),
                    };
                }
            }

            var query = sortedQuery.Select(x => new IncidentReportsGridItem
            {
                Id = x.Id,
                FullName = $"{x.Name} {x.Surname}",
                PatientFullName = $"{x.PatientName} {x.PatientSurname}",
                PatientAge = x.PatientDob.HasValue
                    ? Math.Round((DateTime.Now - x.PatientDob.Value).TotalDays / 365.25, 1)
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
            });



            var items = await query
             .Skip(Math.Max(request.Page, 0) * request.PageSize)
             .Take(request.PageSize)
             .ToListAsync(token);

            foreach (var item in items)
            {
                if (item.HasOtherCategory)
                {
                    item.Categories = [.. item.Categories, IncidentCategory.Other];
                }
            }

            var result = new IncidentReportsGridVm
            {
                Items = items,
                ItemTotalCount = totalItems
            };

            return result;
        }
    }
}
