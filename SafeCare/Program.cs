using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using MudBlazor.Services;
using SafeCare.Components;
using SafeCare.Data;
using SafeCare.Data.Entities;
using SafeCare.Endpoints;
using SafeCare.Middlewares;
using SafeCare.Services;
using SafeCare.Utils;
using Serilog;

// SafeCare — adverse-event reporting for Polish hospitals.
//
// Two modules share this one Blazor Server application:
//   * public  — "/" is an anonymous incident form, protected by rate limiting and honeypots
//   * admin   — "/dashboard", "/details/{id}", "/account" need a login;
//               "/admin/users" additionally needs the Admin role
//
// Serilog is configured before the host is built so that failures during construction are
// still recorded, and the whole body is wrapped so that a crash is flushed to the log.
LoggerConfig.ConfigureLogger();

try
{
    Log.Information("Starting SafeCare application");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    builder.Services.AddCustomRateLimiter();

    builder.Services.AddDbContextFactory<AppDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
    builder.Services.AddIdentity();

    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    builder.Services.AddMudServices(config =>
    {
        config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.TopCenter;
    });


    // Stateless, so a single shared instance is fine.
    builder.Services.AddSingleton<IBotDetectionService, BotDetectionService>();

    // Scoped: these open a DbContext per call through the factory.
    builder.Services.AddScoped<IIncidentDefinitionService, IncidentDefinitionService>();
    builder.Services.AddScoped<IDepartmentService, DepartmentService>();
    builder.Services.AddScoped<IIncidentReportService, IncidentReportService>();
    builder.Services.AddScoped<IUserManagementService, UserManagementService>();
    builder.Services.AddEmailNotifications(builder.Configuration);

    var app = builder.Build();

    // Bring the database up to date and make sure an administrator exists, before the first
    // request is served. Migrations are applied automatically, so a fresh clone needs nothing
    // beyond a reachable PostgreSQL instance.
    using (var scope = app.Services.CreateScope())
    {
        Log.Information("Start DB migration.");
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        await dbContext.Database.MigrateAsync();
        await DbSeeder.SeedAsync(dbContext);
        await IdentitySeeder.SeedRolesAndAdminAsync(roleManager, userManager);
        Log.Information("DB migration completed.");
    }

    app.UseMiddleware<SecurityHeadersMiddleware>();

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        app.UseHsts();
    }

    app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
    app.UseHttpsRedirection();

    app.UseRateLimiter();

    app.UseAuthentication();
    app.UseAuthorization();

    // Must follow authentication: the antiforgery token is bound to the authenticated user,
    // and the login form in Login.razor posts one to /signin.
    app.UseAntiforgery();

    app.MapStaticAssets();
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    app.MapSignInEndpoint();
    app.MapSignOutEndpoint();

    app.Run();
}
catch (Exception ex)
{
    // Covers startup failures too — an unreachable database or a missing admin account
    // surfaces here rather than as a silent exit.
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
