using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafeCare.Enums;
using SafeCare.Exceptions;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace SafeCare.Data.Entities
{
    /// <summary>
    /// A single adverse-event report submitted through the public form.
    /// </summary>
    /// <remarks>
    /// Reporter and patient details are all optional — reporting anonymously is deliberately
    /// allowed. What the report must carry is when it happened, where, and a description.
    /// <para>
    /// The timing is expressed one of two ways, never both: an exact <see cref="Date"/>
    /// (including the time of day), or a <see cref="DateFrom"/>–<see cref="DateTo"/> range for
    /// events the reporter can only place within a period. The constructor enforces this.
    /// </para>
    /// </remarks>
    [Display(Name = "Zgłoszenie zdarzenia")]
    public class IncidentReport
    {
        /// <summary>
        /// Required by EF Core for materialisation; not for application code.
        /// </summary>
        protected IncidentReport()
        {
        }

        /// <summary>
        /// Creates a report, validating the date combination and stamping it as
        /// <see cref="ReportStatus.New"/>.
        /// </summary>
        /// <exception cref="DomainException">
        /// Neither an exact date nor a complete range was supplied, the range is inverted, or
        /// the event is dated in the future.
        /// </exception>
        [SetsRequiredMembers]
        public IncidentReport(
            string? name,
            string? surname,
            string? phone,
            string? email,
            string? patientName,
            string? patientSurname,
            DateTime? patientDob,
            Gender patientGender,
            DateTime? dateFrom,
            DateTime? dateTo,
            DateTime? date,
            Department department,
            IList<IncidentDefinition> incidentDefinitions,
            string? otherIncidentDefinition,
            string incidentDescription)
        {
            Name = name;
            Surname = surname;
            Phone = phone;
            Email = email;
            PatientName = patientName;
            PatientSurname = patientSurname;
            PatientDob = patientDob;
            PatientGender = patientGender;

            // Normalize dates to remove time component for dateFrom and dateTo
            dateFrom = dateFrom?.Date;
            dateTo = dateTo?.Date;

            ValidateDates(dateFrom, dateTo, date);
            DateFrom = dateFrom;
            DateTo = dateTo;
            Date = date;

            Department = department;
            IncidentDefinitions = incidentDefinitions;
            OtherIncidentDefinition = otherIncidentDefinition;
            IncidentDescription = incidentDescription;

            CreatedAt = DateTime.Now;
            Status = ReportStatus.New;
        }

        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Surname { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? PatientName { get; set; }
        public string? PatientSurname { get; set; }
        public DateTime? PatientDob { get; set; }
        public Gender PatientGender { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public DateTime? Date { get; set; }
        public required Department Department { get; set; }
        public int DepartmentId { get; set; }
        public IList<IncidentDefinition> IncidentDefinitions { get; set; } = [];

        /// <summary>
        /// Free-text event description used when the reporter could not find a matching entry
        /// in the dictionary. A non-empty value is what places the report in the
        /// <see cref="IncidentCategory.Other"/> category, which has no dictionary rows of its own.
        /// </summary>
        public string? OtherIncidentDefinition { get; set; }
        public required string IncidentDescription { get; set; }
        public DateTime CreatedAt { get; set; }
        public ReportStatus Status { get; set; }


        /// <summary>
        /// Enforces the "exact date or complete range, never in the future" rule.
        /// </summary>
        private void ValidateDates(
            DateTime? dateFrom,
            DateTime? dateTo,
            DateTime? date)
        {
            if ((!dateFrom.HasValue || !dateTo.HasValue) && !date.HasValue)
            {
                throw new DomainException("Należy podać zakres dat lub konkretną datę i godzinę.");
            }

            if (dateFrom.HasValue && dateTo.HasValue)
            {
                if (dateFrom > dateTo)
                {
                    throw new DomainException("Data początkowa nie może być późniejsza niż data końcowa.");
                }

                if (dateFrom > DateTime.Now.Date || dateTo > DateTime.Now.Date)
                {
                    throw new DomainException("Zakres dat nie może być w przyszłości.");
                }
            }
            else
            {
                if (date!.Value > DateTime.Now)
                {
                    throw new DomainException("Konkretna data i godzina nie może być w przyszłości.");
                }
            }
        }
    }

    public class IncidentReportEntityConfiguration : IEntityTypeConfiguration<IncidentReport>
    {
        public void Configure(EntityTypeBuilder<IncidentReport> builder)
        {
            builder.Property(ir => ir.Name)
                .HasMaxLength(50);

            builder.Property(ir => ir.Surname)
                .HasMaxLength(50);

            builder.Property(ir => ir.Phone)
                .HasMaxLength(15);

            builder.Property(ir => ir.Email)
                .HasMaxLength(100);

            builder.Property(ir => ir.PatientName)
                .HasMaxLength(50);

            builder.Property(ir => ir.PatientSurname)
                .HasMaxLength(50);

            builder.Property(ir => ir.PatientGender)
                .IsRequired()
                .HasConversion<string>();

            builder.Property(ir => ir.IncidentDescription)
                .HasMaxLength(5000);

            builder.Property(ir => ir.PatientDob)
                .HasColumnType("date");

            builder.Property(ir => ir.Date)
                .HasColumnType("timestamp without time zone");

            builder.Property(ir => ir.DateFrom)
                .HasColumnType("timestamp without time zone");

            builder.Property(ir => ir.DateTo)
                .HasColumnType("timestamp without time zone");

            builder.Property(ir => ir.OtherIncidentDefinition)
                .HasMaxLength(255);

            builder.HasOne(ir => ir.Department)
                .WithMany()
                .HasForeignKey(ir => ir.DepartmentId)
                .IsRequired();

            builder.HasMany(ir => ir.IncidentDefinitions)
                .WithMany(id => id.ReportsWithIncident);

            builder.Property(ir => ir.CreatedAt)
                .HasColumnType("timestamp without time zone");

            builder.Property(ir => ir.Status)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasDefaultValue(ReportStatus.New);
        }
    }
}
