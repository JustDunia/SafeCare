using SafeCare.Exceptions;

namespace SafeCare.Tests.Unit.Entities;

public class IncidentReportTests
{
    [Fact]
    public void AcceptsAnExactPointInTime()
    {
        var date = DateTime.Now.AddHours(-3);
        var report = ReportFactory.Create(date: date);

        Assert.Equal(date, report.Date);
        Assert.Null(report.DateFrom);
    }

    [Fact]
    public void AcceptsACompleteRange()
    {
        var report = ReportFactory.Create(
            dateFrom: DateTime.Today.AddDays(-5),
            dateTo: DateTime.Today.AddDays(-2));

        Assert.Equal(DateTime.Today.AddDays(-5), report.DateFrom);
        Assert.Equal(DateTime.Today.AddDays(-2), report.DateTo);
    }

    [Fact]
    public void StripsTheTimeComponentFromRangeEnds()
    {
        var report = ReportFactory.Create(
            dateFrom: DateTime.Today.AddDays(-5).AddHours(13),
            dateTo: DateTime.Today.AddDays(-2).AddHours(17));

        Assert.Equal(DateTime.Today.AddDays(-5), report.DateFrom);
        Assert.Equal(DateTime.Today.AddDays(-2), report.DateTo);
    }

    [Fact]
    public void RejectsAHalfOpenRangeWithNoExactDate()
    {
        var exception = Assert.Throws<DomainException>(() =>
            ReportFactory.Create(dateFrom: DateTime.Today.AddDays(-5), dateTo: null, date: null));

        Assert.Contains("zakres dat", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsAnInvertedRange()
    {
        Assert.Throws<DomainException>(() =>
            ReportFactory.Create(
                dateFrom: DateTime.Today.AddDays(-2),
                dateTo: DateTime.Today.AddDays(-5)));
    }

    [Fact]
    public void RejectsARangeInTheFuture()
    {
        Assert.Throws<DomainException>(() =>
            ReportFactory.Create(
                dateFrom: DateTime.Today.AddDays(1),
                dateTo: DateTime.Today.AddDays(2)));
    }

    [Fact]
    public void RejectsAnExactDateInTheFuture()
    {
        Assert.Throws<DomainException>(() => ReportFactory.Create(date: DateTime.Now.AddHours(2)));
    }

    [Fact]
    public void StampsNewReportsAsNew()
    {
        var report = ReportFactory.Create();

        Assert.Equal(SafeCare.Enums.ReportStatus.New, report.Status);
    }
}
