using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SafeCare.Data.Entities
{
    public class Department
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Code { get; set; }
    }

    public class DepartmentEntityConfiguration : IEntityTypeConfiguration<Department>
    {
        public void Configure(EntityTypeBuilder<Department> builder)
        {
            builder.Property(d => d.Name)
                .HasMaxLength(255);

            builder.Property(d => d.Code)
                .HasMaxLength(10);
        }
    }
}
