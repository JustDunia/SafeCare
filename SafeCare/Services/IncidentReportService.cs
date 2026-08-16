using Microsoft.EntityFrameworkCore;
using SafeCare.Data;
using SafeCare.Data.Entities;
using SafeCare.Dtos;
using SafeCare.Email;
using SafeCare.Enums;
using SafeCare.Exceptions;
using SafeCare.Utils;
using SafeCare.ViewModels;
using Serilog;

namespace SafeCare.Services
{
    /// <summary>
    /// Read and write access to adverse-event reports ("zdarzenia niepożądane"): the public
    /// form creates them, the admin dashboard lists, inspects, re-statuses and deletes them.
    /// </summary>
    public interface IIncidentReportService
    {
        /// <summary>
        /// Persists a new report and queues a notification e-mail to every user who opted in.
        /// </summary>
        /// <returns>The database identifier of the report that was created.</returns>
        /// <exception cref="EntityNotFoundException">
        /// The referenced department or one of the incident definitions does not exist.
        /// </exception>
        Task<int> CreateReport(IncidentReportDto incidentReportDto, CancellationToken token = default);

        /// <summary>
        /// Returns one page of reports for the dashboard grid, applying the caller's filters
        /// and sort definition, together with the total row count for the pager.
        /// </summary>
        Task<IncidentReportsGridVm> GetReports(IncidentReportsRequestVm request, CancellationToken token = default);

        /// <summary>
        /// Loads a single report with its department and incident definitions resolved for display.
        /// </summary>
        /// <exception cref="DomainException">No report exists with the given identifier.</exception>
        Task<IncidentReportDetails> GetReportDetails(int id, CancellationToken token = default);

        /// <summary>
        /// Moves a report to a new workflow status.
        /// </summary>
        /// <exception cref="DomainException">No report exists with the given identifier.</exception>
        Task UpdateStatus(int id, ReportStatus status, CancellationToken token = default);

        /// <summary>
        /// Permanently removes a report.
        /// </summary>
        /// <exception cref="DomainException">No report exists with the given identifier.</exception>
        Task DeleteReport(int id, CancellationToken token = default);
    }

    /// <summary>
    /// Entity Framework implementation of <see cref="IIncidentReportService"/>.
    /// </summary>
    /// <remarks>
    /// Every method opens its own short-lived <see cref="AppDbContext"/> through the factory.
    /// Blazor Server keeps a circuit alive across many interleaved user actions, so a single
    /// injected context would be shared between concurrent operations and is not safe here.
    /// </remarks>
    public class IncidentReportService(
        IDbContextFactory<AppDbContext> dbContextFactory,
        IEmailQueue emailQueue) : IIncidentReportService
    {
        private readonly IDbContextFactory<AppDbContext> _dbContextFactory = dbContextFactory;
        private readonly IEmailQueue _emailQueue = emailQueue;

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

            // Non-blocking email notification — never propagates failure to the caller
            try
            {
                var recipients = await dbContext.Users
                    .Where(u => u.ReceiveEmailNotifications && !string.IsNullOrWhiteSpace(u.Email))
                    .Select(u => u.Email!)
                    .ToListAsync(token);

                if (recipients.Count > 0)
                {
                    var emailMessage = IncidentEmailTemplate.Build(incidentReport, recipients);
                    _emailQueue.Enqueue(emailMessage);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to enqueue notification email for report #{ReportId}", incidentReport.Id);
            }

            return incidentReport.Id;
        }

        public async Task DeleteReport(int id, CancellationToken token = default)
        {
            using var dbContext = _dbContextFactory.CreateDbContext();

            var report = await dbContext.IncidentReports.FindAsync([id], cancellationToken: token)
                ?? throw new DomainException($"Entity of type {nameof(IncidentReport)} with Id {id} not found");

            dbContext.Remove(report);
            await dbContext.SaveChangesAsync(token);
        }

        /// <summary>
        /// Wraps a user-supplied filter term into a case-insensitive "contains" LIKE pattern,
        /// escaping the wildcards so that a term such as "100%" is matched literally.
        /// </summary>
        private static string ToContainsPattern(string term)
        {
            var escaped = term
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_");

            return $"%{escaped}%";
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
                Description = report.IncidentDescription,
                Status = report.Status
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

            // MudDataGrid supplies at most one sort definition (SortMode.Single). Descending
            // is taken at face value — it used to be negated here, which silently inverted
            // every column and made the default view show the oldest reports first.
            var sortDef = request.SortDefinitions.FirstOrDefault();
            var sortItem = sortDef?.SortBy;
            var isDescending = sortDef?.Descending ?? false;

            IQueryable<IncidentReport> mainQuery = dbContext.IncidentReports;

            if (request.Filter.FullName is not null)
            {
                var pattern = ToContainsPattern(request.Filter.FullName);
                mainQuery = mainQuery.Where(x => EF.Functions.ILike(x.Name + " " + x.Surname, pattern));
            }

            if (request.Filter.PatientFullName is not null)
            {
                var pattern = ToContainsPattern(request.Filter.PatientFullName);
                mainQuery = mainQuery.Where(x => EF.Functions.ILike(x.PatientName + " " + x.PatientSurname, pattern));
            }

            if (request.Filter.Gender is not null)
            {
                mainQuery = mainQuery.Where(x => x.PatientGender == request.Filter.Gender);
            }

            if (request.Filter.Department is not null)
            {
                var pattern = ToContainsPattern(request.Filter.Department);
                mainQuery = mainQuery.Where(x => EF.Functions.ILike(x.Department.Name, pattern));
            }

            if (request.Filter.Categories is not null && request.Filter.Categories.Any())
            {
                // "Other" is not a real IncidentDefinition category — a report belongs to it
                // when the reporter typed a free-text description instead of picking from the
                // dictionary. It therefore has to be matched on OtherIncidentDefinition, not
                // through the IncidentDefinitions join.
                var selected = request.Filter.Categories
                    .Where(c => c.HasValue)
                    .Select(c => c!.Value)
                    .ToList();

                var includeOther = selected.Contains(IncidentCategory.Other);
                var definitionCategories = selected.Where(c => c != IncidentCategory.Other).ToList();

                mainQuery = mainQuery.Where(x =>
                    x.IncidentDefinitions.Any(i => definitionCategories.Contains(i.Category))
                    || (includeOther && x.OtherIncidentDefinition != null && x.OtherIncidentDefinition != ""));
            }

            if (request.Filter.Statuses is not null && request.Filter.Statuses.Any())
            {
                mainQuery = mainQuery.Where(x => request.Filter.Statuses.Contains(x.Status));
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
                        // Age is derived from the date of birth, so the sort direction flips:
                        // the oldest patient is the one with the earliest PatientDob.
                        nameof(IncidentReportsGridItem.PatientAge) => mainQuery.OrderBy(x => x.PatientDob),
                        nameof(IncidentReportsGridItem.PatientGender) => mainQuery.OrderByDescending(x => x.PatientGender),
                        nameof(IncidentReportsGridItem.Date) => mainQuery.OrderByDescending(x => x.Date)
                                                                     .ThenByDescending(x => x.DateFrom),
                        nameof(IncidentReportsGridItem.Department) => mainQuery.OrderByDescending(x => x.Department.Name),
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
                        nameof(IncidentReportsGridItem.Department) => mainQuery.OrderBy(x => x.Department.Name),
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
                HasOtherCategory = !string.IsNullOrEmpty(x.OtherIncidentDefinition),
                Status = x.Status
            });

            var items = await query
                .Skip(Math.Max(request.Page, 0) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(token);

            // "Other" has no row in IncidentDefinitions, so the grid chip for it is appended
            // in memory once the page has been materialised. The matching filter branch above
            // has to reproduce this rule against OtherIncidentDefinition.
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

        public async Task UpdateStatus(int id, ReportStatus status, CancellationToken token = default)
        {
            using var dbContext = _dbContextFactory.CreateDbContext();

            var report = await dbContext.IncidentReports.FindAsync([id], cancellationToken: token)
                ?? throw new DomainException($"Entity of type {nameof(IncidentReport)} with Id {id} not found");
            report.Status = status;

            await dbContext.SaveChangesAsync(token);
        }
    }
}
