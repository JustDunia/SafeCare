using FluentValidation;
using SafeCare.ViewModels;

namespace SafeCare.Validators;

/// <summary>
/// Validation rules for the public incident form, with messages in Polish.
/// </summary>
/// <remarks>
/// Reporter and patient details are optional — anonymous reporting is intentional — so those
/// rules only apply once a field has been filled in. What is always required is the timing,
/// the ward, at least one event type (or a free-text description) and the narrative.
/// </remarks>
public class IncidentRegistrationFormValidator : AbstractValidator<IncidentRegistrationFormVm>
{
    /// <summary>
    /// Matches a personal name: one or more letters, optionally repeated in groups
    /// separated by a space, hyphen or apostrophe (e.g. "Anna-Maria", "O'Brien").
    /// </summary>
    /// <remarks>
    /// Uses the Unicode letter class <c>\p{L}</c> rather than a literal list of Polish
    /// characters. Spelling the diacritics out made the rule silently depend on the source
    /// file's encoding: when this file was saved as Windows-1250 the class decoded to
    /// replacement characters and every name containing "ą", "ł", "ż", … was rejected.
    /// <c>\p{L}</c> is pure ASCII in source form, so it cannot break that way again.
    /// </remarks>
    private const string NamePattern = @"^\p{L}+(?:[ '\-]\p{L}+)*$";

    public IncidentRegistrationFormValidator()
    {
        RuleFor(x => x.Name)
            .Matches(NamePattern)
            .WithMessage("Imię może zawierać tylko litery")
            .Length(2, 50)
            .WithMessage("Imię musi mieć od 2 do 50 znaków")
            .When(x => !string.IsNullOrEmpty(x.Name));

        RuleFor(x => x.Surname)
            .Matches(NamePattern)
            .WithMessage("Nazwisko może zawierać tylko litery")
            .Length(2, 50)
            .WithMessage("Nazwisko musi mieć od 2 do 50 znaków")
            .When(x => !string.IsNullOrEmpty(x.Surname));

        RuleFor(x => x.Phone)
            .Matches(@"^(\+48)?[\s\-]?[1-9]\d{8}$")
            .WithMessage("Numer telefonu musi być poprawnym polskim numerem telefonu")
            .When(x => !string.IsNullOrEmpty(x.Phone));

        RuleFor(x => x.Email)
            .EmailAddress()
            .WithMessage("Adres e-mail musi być poprawny")
            .When(x => !string.IsNullOrEmpty(x.Email));

        RuleFor(x => x.PatientName)
            .Matches(NamePattern)
            .WithMessage("Imię może zawierać tylko litery")
            .Length(2, 50)
            .WithMessage("Imię musi mieć od 2 do 50 znaków")
            .When(x => !string.IsNullOrEmpty(x.PatientName));

        RuleFor(x => x.PatientSurname)
            .Matches(NamePattern)
            .WithMessage("Nazwisko może zawierać tylko litery")
            .Length(2, 50)
            .WithMessage("Nazwisko musi mieć od 2 do 50 znaków")
            .When(x => !string.IsNullOrEmpty(x.PatientSurname));

        RuleFor(x => x.PatientDob)
            .LessThanOrEqualTo(DateTime.Today)
            .WithMessage("Data urodzenia nie może być w przyszłości")
            .GreaterThanOrEqualTo(DateTime.Today.AddYears(-120))
            .WithMessage("Data urodzenia nie może być starsza niż 120 lat")
            .When(x => x.PatientDob.HasValue);

        When(x => x.IsDatePeriod, () =>
        {
            RuleFor(x => x.DateFrom)
                .NotNull()
                .WithMessage("Data rozpoczęcia jest wymagana");

            RuleFor(x => x.DateFrom)
                .LessThanOrEqualTo(DateTime.Today)
                .WithMessage("Data rozpoczęcia nie może być w przyszłości")
                .When(x => x.DateFrom.HasValue);

            RuleFor(x => x.DateTo)
                .NotNull()
                .WithMessage("Data zakończenia jest wymagana");

            RuleFor(x => x.DateTo)
                .LessThanOrEqualTo(DateTime.Today)
                .WithMessage("Data zakończenia nie może być w przyszłości")
                .When(x => x.DateTo.HasValue);

            RuleFor(x => x.DateTo)
                .GreaterThanOrEqualTo(x => x.DateFrom)
                .WithMessage("Data zakończenia musi być późniejsza lub równa dacie rozpoczęcia")
                .When(x => x.DateFrom.HasValue && x.DateTo.HasValue);
        });

        When(x => !x.IsDatePeriod, () =>
        {
            RuleFor(x => x.Date)
                .NotNull()
                .WithMessage("Data jest wymagana");

            RuleFor(x => x.Date)
                .LessThanOrEqualTo(DateTime.Today)
                .WithMessage("Data nie może być w przyszłości")
                .When(x => x.Date.HasValue);

            RuleFor(x => x.Time)
                .NotEmpty()
                .WithMessage("Czas jest wymagany");

            RuleFor(x => x)
                .Must(x => !IsFutureDateTime(x.Date, x.Time))
                .WithMessage("Data i czas nie mogą być w przyszłości")
                .When(x => x.Date.HasValue && !string.IsNullOrEmpty(x.Time))
                .WithName("Time");
        });

        RuleFor(x => x.Department)
            .NotNull()
            .WithMessage("Miejsce zdarzenia jest wymagane");

        RuleFor(x => x)
            .Must(x => x.SelectedIncidentDefinitions.Any() || !string.IsNullOrWhiteSpace(x.OtherIncidentDefinition))
            .WithMessage("Należy wybrać co najmniej jeden rodzaj zdarzenia lub podać własny opis")
            .WithName("SelectedIncidentDefinitions");

        RuleFor(x => x.IncidentDescription)
            .NotEmpty()
            .WithMessage("Opis zdarzenia jest wymagany")
            .MaximumLength(5000)
            .WithMessage("Opis zdarzenia może mieć maksymalnie 5000 znaków");
    }

    /// <summary>
    /// Combines the separate date and time inputs to catch an event timestamped later today,
    /// which a date-only comparison would let through.
    /// </summary>
    private bool IsFutureDateTime(DateTime? date, string? time)
    {
        if (!date.HasValue || string.IsNullOrEmpty(time))
        {
            return false;
        }

        if (TimeSpan.TryParse(time, out var timeSpan))
        {
            var dateTime = date.Value.Add(timeSpan);
            return dateTime > DateTime.Now;
        }

        return false;
    }

    /// <summary>
    /// Adapter that lets MudBlazor drive this validator per field: MudForm calls it with the
    /// model and a property name and expects the messages for that property alone.
    /// </summary>
    public Func<object, string, Task<IEnumerable<string>>> ValidateValue => async (model, propertyName) =>
    {
        var validationCtx = ValidationContext<IncidentRegistrationFormVm>.CreateWithOptions(
            (IncidentRegistrationFormVm)model,
            x => x.IncludeProperties(propertyName));

        var result = await ValidateAsync(validationCtx);

        if (result.IsValid)
        {
            return [];
        }

        return result.Errors.Select(e => e.ErrorMessage);
    };
}
