using Microsoft.EntityFrameworkCore;
using SecureLab.Api.Application.Incidents;
using SecureLab.Api.Data;
using SecureLab.Api.Presentation.Contracts;

namespace SecureLab.Api.Scaffolding;

// Навчальний старт ЛР 02. Запускати лише з локальними штучними даними.
public static class Lab02Endpoints
{
    public static void MapLab02Endpoints(this WebApplication app)
    {
        app.MapGet("/api/incidents/search", async (string? q, string? sortBy, SecureLabDbContext db, CancellationToken ct) =>
        {
            var order = sortBy switch
            {
                null or "" or "createdAtUtc" => "created_at_utc DESC",
                "severity" => "severity", "status" => "status", _ => sortBy
            };
            var sql = "SELECT * FROM incidents WHERE title ILIKE '%" + (q ?? "")
                + "%' OR description ILIKE '%" + (q ?? "") + "%' ORDER BY " + order + " LIMIT 50";
            var rows = await db.Incidents.FromSqlRaw(sql).AsNoTracking().ToListAsync(ct);
            return Results.Ok(rows.Select(row => new
            {
                row.Id, row.Title, row.Description,
                Severity = row.Severity.ToString(), Status = row.Status.ToString(), row.CreatedAtUtc
            }));
        });
        app.MapPost("/api/incidents", CreateIncidentAsync)
            .WithName("CreateIncident")
            .WithTags("Incidents")
            .Produces<CreatedIncidentResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    /// <summary>
    /// POST /api/incidents: JSON -> CreateIncidentRequest -> серверна перевірка ->
    /// предметний інваріант -> entity з server-managed полями -> 201 + response DTO.
    /// Помилки контракту -> 400 Validation Problem Details, активний дублікат -> 409.
    /// </summary>
    private static async Task<IResult> CreateIncidentAsync(
        CreateIncidentRequest request,
        IncidentCommands commands,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        // Поточний UTC-час фіксується один раз на початку обробки.
        var nowUtc = clock.GetUtcNow();

        var (validated, errors) = CreateIncidentValidator.Validate(request, nowUtc);
        if (validated is null)
        {
            return Results.ValidationProblem(errors, title: "Запит не відповідає контракту створення інциденту");
        }

        var created = await commands.CreateAsync(validated, nowUtc, cancellationToken);

        return created is null
            ? Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Конфлікт із поточним станом",
                detail: "Уже існує не закритий інцидент із таким самим заголовком.")
            : Results.Created($"/api/incidents/{created.Id}", created);
    }
}
