using SafeCare.Data.Entities;
using SafeCare.Enums;

namespace SafeCare.Tests.Unit;

/// <summary>
/// Builds valid <see cref="IncidentReport"/> entities for tests. The entity constructor
/// enforces the date rules, so every field it validates gets a sane default here.
/// </summary>
public static class ReportFactory
{
    public static Department Department(string name = "Chirurgia", string code = "CH") =>
        new() { Name = name, Code = code };

    public static IncidentDefinition Definition(
        string name = "Upadek pacjenta",
        IncidentCategory category = IncidentCategory.Clinical) =>
        new() { Name = name, Category = category };

    public static IncidentReport Create(
        Department? department = null,
        IList<IncidentDefinition>? definitions = null,
        string? name = "Anna",
        string? surname = "Kowalska",
        string? patientName = "Piotr",
        string? patientSurname = "Wiśniewski",
        DateTime? patientDob = null,
        Gender gender = Gender.Male,
        DateTime? date = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        string? otherIncidentDefinition = null,
        string description = "Opis zdarzenia.") =>
        new(
            name,
            surname,
            "123456789",
            "anna@example.com",
            patientName,
            patientSurname,
            patientDob ?? new DateTime(1980, 5, 12),
            gender,
            dateFrom,
            dateTo,
            dateFrom is null && dateTo is null ? date ?? DateTime.Now.AddDays(-1) : null,
            department ?? Department(),
            definitions ?? [Definition()],
            otherIncidentDefinition,
            description);
}
