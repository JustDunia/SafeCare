using MudBlazor;

namespace SafeCare.ViewModels
{
    public class IncidentReportsRequestVm
    {
        public const int DefaultPage = 0;
        public const int DefaultPageSize = 10;
        public const string DefaultSortBy = nameof(IncidentReportsGridItem.Id);
        public const bool DefaultSortDescending = true;

        public int Page { get; set; } = DefaultPage;
        public int PageSize { get; set; } = DefaultPageSize;
        public ICollection<SortDefinition<IncidentReportsGridItem>> SortDefinitions { get; set; } =
            [new SortDefinition<IncidentReportsGridItem>(DefaultSortBy, DefaultSortDescending, 0, x => x.Id)];
        public IncidentReportFiler Filter { get; set; } = new();
    }
}
