using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using SecureLab.Api.Application.Incidents;
using SecureLab.Api.Data;
using SecureLab.Api.Presentation.Endpoints;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    // Браузерний клієнт лежить у Client/ поряд із кодом API.
    WebRootPath = "Client"
});

// Рядок з'єднання: базове значення — у appsettings.Development.json, але його
// можна перевизначити змінною середовища ConnectionStrings__SecureLab, не
// змінюючи файли конфігурації й не записуючи реальний секрет до Git.
var connectionString = builder.Configuration.GetConnectionString("SecureLab")
    ?? throw new InvalidOperationException(
        "Не задано рядок з'єднання ConnectionStrings:SecureLab (див. appsettings.Development.json " +
        "або змінну середовища ConnectionStrings__SecureLab).");

builder.Services.AddDbContext<SecureLabDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<IncidentQueries>();

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

// Разова службова команда: застосувати migrations, очистити відомі навчальні
// таблиці й повернути seed. Працює лише в явно налаштованому Development.
if (args.Contains("--reset-database"))
{
    if (!app.Environment.IsDevelopment())
    {
        app.Logger.LogError("Команду --reset-database дозволено лише в середовищі Development.");
        return 1;
    }

    using var resetScope = app.Services.CreateScope();
    var resetDb = resetScope.ServiceProvider.GetRequiredService<SecureLabDbContext>();
    await DbSeeder.ResetAsync(resetDb);
    app.Logger.LogInformation("Навчальні дані повернуто до відомого seed-стану.");
    return 0;
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();

    // Перший Development-запуск застосовує готову migration і додає seed-дані.
    using var seedScope = app.Services.CreateScope();
    var seedDb = seedScope.ServiceProvider.GetRequiredService<SecureLabDbContext>();
    await DbSeeder.SeedAsync(seedDb);
}

app.UseDefaultFiles();
app.UseStaticFiles();

// /health перевіряє, чи API може встановити з'єднання з БД у конкретний момент.
app.MapGet("/health", async (SecureLabDbContext db, CancellationToken ct) =>
{
    var canConnect = await db.Database.CanConnectAsync(ct);
    return canConnect
        ? Results.Ok(new { status = "healthy", database = "reachable" })
        : Results.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Базу даних недоступно",
            detail: "API запущено, але не може встановити з'єднання з PostgreSQL.");
})
.WithName("Health")
.WithTags("Diagnostics");

app.MapIncidentEndpoints();

// Запасний маршрут: усе, що не збіглося з API чи статичним файлом, віддає
// index.html. Саме тому помилковий шлях /api/... може повернути 200 з HTML —
// звіряйте Content-Type, а не лише status code.
app.MapFallbackToFile("index.html");

app.Run();
return 0;

/// <summary>Потрібен для WebApplicationFactory у інтеграційних тестах.</summary>
public partial class Program;
