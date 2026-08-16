using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SafeCare.Data.Entities
{
    /// <summary>
    /// A member of hospital staff with access to the admin module. Extends the Identity user
    /// with a display name and a notification preference.
    /// </summary>
    public class User : IdentityUser<Guid>
    {
        public string FirstName { get; set; } = null!;

        public string LastName { get; set; } = null!;

        /// <summary>
        /// Opt-in for the e-mail sent whenever a new report arrives. Users who leave this off
        /// are excluded from the BCC list, so the notification is strictly opt-in.
        /// </summary>
        public bool ReceiveEmailNotifications { get; set; } = false;
    }

    /// <summary>
    /// Maps <see cref="User"/> and seeds the built-in <c>admin</c> account.
    /// </summary>
    /// <remarks>
    /// The administrator is seeded here through <c>HasData</c>, which bakes it into the initial
    /// migration rather than into <c>SeedData.sql</c>. <c>IdentitySeeder</c> throws on startup
    /// if the account is missing, so this seed must not be removed. Its password hash is a
    /// fixed literal because <c>HasData</c> requires deterministic values — this is a
    /// development credential and must be rotated before any real deployment.
    /// </remarks>
    public class UserEntityConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.Property(u => u.FirstName)
                .HasMaxLength(20);

            builder.Property(u => u.LastName)
                .HasMaxLength(30);

            builder.Property(u => u.ReceiveEmailNotifications)
                .HasDefaultValue(false);

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
                ReceiveEmailNotifications = false,
            };

            builder.HasData(admin);
        }
    }
}
