using MudBlazor;
using SafeCare.Enums;
using SafeCare.Utils;

namespace SafeCare.Tests.Unit.Enums;

public class EnumDisplayTests
{
    public static TheoryData<IncidentCategory> AllCategories() => [.. Enum.GetValues<IncidentCategory>()];

    public static TheoryData<ReportStatus> AllStatuses() => [.. Enum.GetValues<ReportStatus>()];

    public static TheoryData<Gender> AllGenders() => [.. Enum.GetValues<Gender>()];

    [Theory]
    [MemberData(nameof(AllCategories))]
    public void EveryCategoryCarriesAPolishLabel(IncidentCategory category)
    {
        var label = category.GetDisplayName();

        Assert.False(string.IsNullOrWhiteSpace(label));
        Assert.NotEqual(category.ToString(), label);
    }

    [Theory]
    [MemberData(nameof(AllStatuses))]
    public void EveryStatusCarriesAPolishLabel(ReportStatus status)
    {
        var label = status.GetDisplayName();

        Assert.False(string.IsNullOrWhiteSpace(label));
        Assert.NotEqual(status.ToString(), label);
    }

    [Theory]
    [MemberData(nameof(AllGenders))]
    public void EveryGenderCarriesAPolishLabel(Gender gender)
    {
        var label = gender.GetDisplayName();

        Assert.False(string.IsNullOrWhiteSpace(label));
        Assert.NotEqual(gender.ToString(), label);
    }

    [Theory]
    [MemberData(nameof(AllStatuses))]
    public void EveryStatusMapsToADedicatedColour(ReportStatus status)
    {
        // Color.Default is the switch's fallback arm — reaching it means a status was added
        // without extending the mapping.
        Assert.NotEqual(Color.Default, status.GetColor());
    }

    [Theory]
    [MemberData(nameof(AllStatuses))]
    public void EveryStatusMapsToAnIcon(ReportStatus status)
    {
        Assert.False(string.IsNullOrEmpty(status.GetIcon()));
    }

    [Fact]
    public void OtherCategoryUsesTheLabelTheGridAndFilterShareAsAContract()
    {
        // The dashboard chip and the category filter both key off this exact label.
        Assert.Equal("Inne", IncidentCategory.Other.GetDisplayName());
    }
}
