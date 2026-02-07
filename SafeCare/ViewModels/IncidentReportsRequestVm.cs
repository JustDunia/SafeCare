using MudBlazor;
using SafeCare.Enums;

namespace SafeCare.ViewModels
{
    public class IncidentReportsRequestVm
    {
        private static SortDefinition<IncidentReportsGridItem> DefaultSort => new(
            nameof(IncidentReportsGridItem.Id), true, 0, x => x.Id);


        public int Page { get; set; } = 0;
        public int PageSize { get; set; } = 10;
        public ICollection<SortDefinition<IncidentReportsGridItem>> SortDefinitions { get; set; } = [DefaultSort];
        public IncidentReportFiler Filter { get; set; } = new();
    }

    public class IncidentReportFiler
    {
        public string FullName { get; set; } = string.Empty;
        public string PatientFullName { get; set; } = string.Empty;
        public Gender? Gender { get; set; }
        public string Department { get; set; } = string.Empty;
        public IEnumerable<IncidentCategory?> Categories { get; set; } = [];
    }
}
