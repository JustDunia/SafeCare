using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafeCare.Enums;

namespace SafeCare.Data.Entities
{
    public class IncidentDefinition
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public IncidentCategory Category { get; set; }

        public IList<IncidentReport> ReportsWithIncident { get; set; } = [];
    }

    public class IncidentDefinitionEntityConfiguration : IEntityTypeConfiguration<IncidentDefinition>
    {
        public void Configure(EntityTypeBuilder<IncidentDefinition> builder)
        {
            builder.Property(e => e.Category)
                 .HasConversion<string>()
                 .HasMaxLength(30);

            builder.Property(e => e.Name)
                .HasMaxLength(255);
        }
    }
}
