using MudBlazor;
using System.ComponentModel.DataAnnotations;

namespace SafeCare.Enums
{
    public enum ReportStatus
    {
        [Display(Name = "Nowe")]
        New,

        [Display(Name = "W trakcie rozpatrywania")]
        InProgress,

        [Display(Name = "Zakończone")]
        Resolved,

        [Display(Name = "Odrzucone")]
        Rejected
    }

    public static class ReportStatusExtenstions
    {
        extension(ReportStatus status)
        {
            public Color GetColor()
            {
                return status switch
                {
                    ReportStatus.New => Color.Info,
                    ReportStatus.InProgress => Color.Warning,
                    ReportStatus.Resolved => Color.Success,
                    ReportStatus.Rejected => Color.Error,
                    _ => Color.Default
                };
            }

            public string GetIcon()
            {
                return status switch
                {
                    ReportStatus.New => Icons.Material.Outlined.Lightbulb,
                    ReportStatus.InProgress => Icons.Material.Filled.Autorenew,
                    ReportStatus.Resolved => Icons.Material.Filled.Check,
                    ReportStatus.Rejected => Icons.Material.Filled.Close,
                    _ => string.Empty
                };
            }
        }
    }
}
