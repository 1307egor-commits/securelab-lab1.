using SecureLab.Api.Application.Incidents;
using SecureLab.Api.Data;
using SecureLab.Api.Presentation.Contracts;

namespace SecureLab.Api.Presentation.Endpoints;

/// <summary>
/// HTTP-контур інцидентів. Група має префікс <c>/api/incidents</c>.
/// Літеральний сегмент <c>/severity-summary</c> не конфліктує з <c>/{id:guid}</c>,
/// бо текст "severity-summary" не задовольняє маршрутне обмеження <c>:guid</c>.
/// </summary>
public static class IncidentEndpoints
{
    public static IEndpointRouteBuilder MapIncidentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/incidents").WithTags("Incidents");

        group.MapGet("", GetListAsync)
            .WithName("GetIncidents")
            .Produces<IReadOnlyList<IncidentListItemResponse>>()
            .ProducesValidationProblem();

        group.MapGet("/{id:guid}", GetDetailsAsync)
            .WithName("GetIncidentDetails")
            .Produces<IncidentDetailsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/severity-summary", GetSeveritySummaryAsync)
            .WithName("GetIncidentSeveritySummary")
            // ЕТАП 3: реалізована точка розширення. Успішна відповідь — типізований
            // масив підсумку; некоректний ?status= дає 400 Validation Problem Details.
            .Produces<IReadOnlyList<IncidentSeveritySummaryResponse>>()
            .ProducesValidationProblem();

        return app;
    }

    /// <summary>
    /// Дозволені значення фільтра <c>?status=</c> для підсумку за severity.
    /// Явний allowlist: усе поза ним — 400, а не тиха підстановка.
    /// </summary>
    private static readonly string[] SummaryStatusAllowlist =
        [nameof(IncidentStatus.New), nameof(IncidentStatus.Triaged), nameof(IncidentStatus.InProgress),
         nameof(IncidentStatus.Resolved), nameof(IncidentStatus.Closed)];

    /// <summary>
    /// GET /api/incidents[?status=...]. Значення status контролює клієнт, тому
    /// сервер перевіряє його через Enum.TryParse + Enum.IsDefined: невідоме
    /// значення дає 400 Validation Problem Details.
    /// </summary>
    private static async Task<IResult> GetListAsync(
        string? status,
        IncidentQueries queries,
        CancellationToken cancellationToken)
    {
        IncidentStatus? parsedStatus = null;

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<IncidentStatus>(status, ignoreCase: true, out var parsed)
                || !Enum.IsDefined(parsed))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["status"] = [$"Невідоме значення статусу: '{status}'."]
                });
            }

            parsedStatus = parsed;
        }

        var incidents = await queries.GetListAsync(parsedStatus, cancellationToken);
        return Results.Ok(incidents);
    }

    /// <summary>
    /// GET /api/incidents/{id}. Маршрутне обмеження :guid відсіює синтаксично
    /// некоректний id ще до обробника. Якщо query повертає null — це наявний
    /// маршрут із відсутнім ресурсом, тобто 404 Problem Details, а не 400.
    /// </summary>
    private static async Task<IResult> GetDetailsAsync(
        Guid id,
        IncidentQueries queries,
        CancellationToken cancellationToken)
    {
        var details = await queries.GetDetailsAsync(id, cancellationToken);

        return details is null
            ? Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Інцидент не знайдено",
                detail: $"Інцидент з ідентифікатором {id} не знайдено.")
            : Results.Ok(details);
    }

    /// <summary>
    /// GET /api/incidents/severity-summary[?status=...]. Повертає кількість
    /// інцидентів за кожним рівнем критичності (політика повного переліку
    /// рівнів, явний порядок критичності). Необов'язковий <c>?status=</c>
    /// перевіряється за allowlist: невідоме значення — 400 Validation Problem Details.
    /// </summary>
    private static async Task<IResult> GetSeveritySummaryAsync(
        string? status,
        IncidentQueries queries,
        CancellationToken cancellationToken)
    {
        IncidentStatus? parsedStatus = null;

        if (!string.IsNullOrWhiteSpace(status))
        {
            var match = SummaryStatusAllowlist.FirstOrDefault(
                allowed => string.Equals(allowed, status, StringComparison.OrdinalIgnoreCase));

            if (match is null || !Enum.TryParse(match, out IncidentStatus parsed))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["status"] =
                    [
                        $"Невідоме значення статусу: '{status}'. Дозволені: {string.Join(", ", SummaryStatusAllowlist)}."
                    ]
                });
            }

            parsedStatus = parsed;
        }

        var summary = await queries.GetSeveritySummaryAsync(parsedStatus, cancellationToken);
        return Results.Ok(summary);
    }
}
