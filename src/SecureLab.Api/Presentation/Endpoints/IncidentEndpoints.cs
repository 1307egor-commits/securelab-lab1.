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
            // BASELINE: точка розширення ще не реалізована. ЛР 1, етап 3
            // замінює цю заготовку робочим endpoint і прибирає цей 501.
            .ProducesProblem(StatusCodes.Status501NotImplemented);

        return app;
    }

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
    /// BASELINE-заготовка. Повертає 501 Not Implemented — це не помилка запуску,
    /// а навмисна позначка незавершеної функції.
    /// </summary>
    private static IResult GetSeveritySummaryAsync()
    {
        return Results.Problem(
            statusCode: StatusCodes.Status501NotImplemented,
            title: "Не реалізовано",
            detail: "Точку розширення GET /api/incidents/severity-summary ще не реалізовано в baseline.");
    }
}
