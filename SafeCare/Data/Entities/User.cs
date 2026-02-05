using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SafeCare.Data.Entities
{
    public class User : IdentityUser<Guid>
    {
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
    }

    public class UserEntityConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.Property(u => u.FirstName)
                .HasMaxLength(20);

            builder.Property(u => u.LastName)
                .HasMaxLength(30);

            var admin = new User
            {
                Id = Guid.Parse("62228aa3-8032-4d31-8b99-719629d26bb7"),
                FirstName = "System",
                LastName = "Administrator",
                UserName = "admin",
                NormalizedUserName = "ADMIN",
                Email = "system@admin.pl",
                NormalizedEmail = "SYSTEM@ADMIN.PL",
                PasswordHash = "AQAAAAIAAYagAAAAEJlVfW5MzpSxR7nZGXwG5XwJp/Zk5inQ901o2pQZ4/7ATt0KP3LqfkmXiWsnmrgWig==",
                SecurityStamp = "18eafee5-4a09-4928-92b7-9abaf1b1cf2e",
                ConcurrencyStamp = "90b9f3e7-5173-4433-8421-0e7867871dc3",
            };

            builder.HasData(admin);
        }
    }
}
