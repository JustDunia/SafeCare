using FluentValidation;
using SafeCare.ViewModels;

namespace SafeCare.Validators;

public class IncidentRegistrationFormValidator : AbstractValidator<IncidentRegistrationFormVm>
{
    public IncidentRegistrationFormValidator()
    {
        RuleFor(x => x.Name)
            .Matches(@"^[a-zA-Z¹æê³ñóœŸ¿¥ÆÊ£ÑÓœ¯]+$")
            .WithMessage("Imiê mo¿e zawieraæ tylko litery")
            .Length(2, 50)
            .WithMessage("Imiê musi mieæ od 2 do 50 znaków")
            .When(x => !string.IsNullOrEmpty(x.Name));

        RuleFor(x => x.Surname)
            .Matches(@"^[a-zA-Z¹æê³ñóœŸ¿¥ÆÊ£ÑÓŒ¯]+$")
            .WithMessage("Nazwisko mo¿e zawieraæ tylko litery")
            .Length(2, 50)
            .WithMessage("Nazwisko musi mieæ od 2 do 50 znaków")
            .When(x => !string.IsNullOrEmpty(x.Surname));

        RuleFor(x => x.Phone)
            .Matches(@"^(\+48)?[\s\-]?[1-9]\d{8}$")
            .WithMessage("Numer telefonu musi byæ poprawnym polskim numerem telefonu")
            .When(x => !string.IsNullOrEmpty(x.Phone));

        RuleFor(x => x.Email)
            .EmailAddress()
            .WithMessage("Adres e-mail musi byæ poprawny")
            .When(x => !string.IsNullOrEmpty(x.Email));

        RuleFor(x => x.PatientName)
            .Matches(@"^[a-zA-Z¹æê³ñóœŸ¿¥ÆÊ£ÑÓŒ¯]+$")
            .WithMessage("Imiê mo¿e zawieraæ tylko litery")
            .Length(2, 50)
            .WithMessage("Imiê musi mieæ od 2 do 50 znaków")
            .When(x => !string.IsNullOrEmpty(x.PatientName));

        RuleFor(x => x.PatientSurname)
            .Matches(@"^[a-zA-Z¹æê³ñóœŸ¿¥ÆÊ£ÑÓŒ¯]+$")
            .WithMessage("Nazwisko mo¿e zawieraæ tylko litery")
            .Length(2, 50)
            .WithMessage("Nazwisko musi mieæ od 2 do 50 znaków")
            .When(x => !string.IsNullOrEmpty(x.PatientSurname));

        RuleFor(x => x.PatientDob)
            .LessThanOrEqualTo(DateTime.Today)
            .WithMessage("Data urodzenia nie mo¿e byæ w przysz³oœci")
            .GreaterThanOrEqualTo(DateTime.Today.AddYears(-120))
            .WithMessage("Data urodzenia nie mo¿e byæ starsza ni¿ 120 lat")
            .When(x => x.PatientDob.HasValue);

        When(x => x.IsDatePeriod, () =>
        {
            RuleFor(x => x.DateFrom)
                .NotNull()
                .WithMessage("Data rozpoczêcia jest wymagana");

            RuleFor(x => x.DateFrom)
                .LessThanOrEqualTo(DateTime.Today)
                .WithMessage("Data rozpoczêcia nie mo¿e byæ w przysz³oœci")
                .When(x => x.DateFrom.HasValue);

            RuleFor(x => x.DateTo)
                .NotNull()
                .WithMessage("Data zakoñczenia jest wymagana");

            RuleFor(x => x.DateTo)
                .LessThanOrEqualTo(DateTime.Today)
                .WithMessage("Data zakoñczenia nie mo¿e byæ w przysz³oœci")
                .When(x => x.DateTo.HasValue);

            RuleFor(x => x.DateTo)
                .GreaterThanOrEqualTo(x => x.DateFrom)
                .WithMessage("Data zakoñczenia musi byæ póŸniejsza lub równa dacie rozpoczêcia")
                .When(x => x.DateFrom.HasValue && x.DateTo.HasValue);
        });

        When(x => !x.IsDatePeriod, () =>
        {
            RuleFor(x => x.Date)
                .NotNull()
                .WithMessage("Data jest wymagana");

            RuleFor(x => x.Date)
                .LessThanOrEqualTo(DateTime.Today)
                .WithMessage("Data nie mo¿e byæ w przysz³oœci")
                .When(x => x.Date.HasValue);

            RuleFor(x => x.Time)
                .NotEmpty()
                .WithMessage("Czas jest wymagany");

            RuleFor(x => x)
                .Must(x => !IsFutureDateTime(x.Date, x.Time))
                .WithMessage("Data i czas nie mog¹ byæ w przysz³oœci")
                .When(x => x.Date.HasValue && !string.IsNullOrEmpty(x.Time))
                .WithName("Time");
        });

        RuleFor(x => x.Department)
            .NotNull()
            .WithMessage("Miejsce zdarzenia jest wymagane");

        RuleFor(x => x)
            .Must(x => x.SelectedIncidentDefinitions.Any() || !string.IsNullOrWhiteSpace(x.OtherIncidentDefinition))
            .WithMessage("Nale¿y wybraæ co najmniej jeden rodzaj zdarzenia lub podaæ w³asny opis")
            .WithName("SelectedIncidentDefinitions");

        RuleFor(x => x.IncidentDescription)
            .NotEmpty()
            .WithMessage("Opis zdarzenia jest wymagany")
            .MaximumLength(5000)
            .WithMessage("Opis zdarzenia mo¿e mieæ maksymalnie 5000 znaków");
    }

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
