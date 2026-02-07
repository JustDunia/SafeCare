using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using MudBlazor.Services;
using SafeCare.Components;
using SafeCare.Data;
using SafeCare.Endpoints;
using SafeCare.Middlewares;
using SafeCare.Services;
using SafeCare.Utils;
using Serilog;

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


    builder.Services.AddSingleton<IBotDetectionService, BotDetectionService>();

    builder.Services.AddScoped<IIncidentDefinitionService, IncidentDefinitionService>();
    builder.Services.AddScoped<IDepartmentService, DepartmentService>();
    builder.Services.AddScoped<IIncidentReportService, IncidentReportService>();

    var app = builder.Build();

    // Seed database
    using (var scope = app.Services.CreateScope())
    {
        Log.Information("Start DB migration.");
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync();
        await DbSeeder.SeedAsync(dbContext);
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
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
