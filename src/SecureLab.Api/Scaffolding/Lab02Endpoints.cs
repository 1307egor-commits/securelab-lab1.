using SecureLab.Api.Application.Incidents;
using SecureLab.Api.Presentation.Contracts;

namespace SecureLab.Api.Scaffolding;

// Навчальний старт ЛР 02. Запускати лише з локальними штучними даними.
public static class Lab02Endpoints
{
    public static void MapLab02Endpoints(this WebApplication app)
    {
        app.MapGet("/api/incidents/search", SearchIncidentsAsync)
            .WithName("SearchIncidents")
            .WithTags("Incidents")
            .Produces<IReadOnlyList<IncidentSearchItemResponse>>()
            .ProducesValidationProblem();
        app.MapPost("/api/incidents", CreateIncidentAsync)
            .WithName("CreateIncident")
            .WithTags("Incidents")
            .Produces<CreatedIncidentResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    /// <summary>Максимальна довжина пошукового значення q.</summary>
    private const int SearchQueryMaxLength = 200;

    /// <summary>
    /// Allowlist логічних значень sortBy. Кожна назва відображається на варіант,
    /// для якого сервер має власний LINQ-вираз; невідома назва до SQL не доходить.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, IncidentSearchSort> SortAllowlist =
        new Dictionary<string, IncidentSearchSort>(StringComparer.Ordinal)
        {
            ["createdAtUtc"] = IncidentSearchSort.CreatedAtUtc,
            ["severity"] = IncidentSearchSort.Severity,
            ["status"] = IncidentSearchSort.Status
        };

    /// <summary>
    /// GET /api/incidents/search?q=...&amp;sortBy=... — q є лише значенням
    /// параметризованого предиката, sortBy — лише вибором з allowlist.
    /// </summary>
    private static async Task<IResult> SearchIncidentsAsync(
        string? q,
        string? sortBy,
        IncidentQueries queries,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();

        if (q is { Length: > SearchQueryMaxLength })
        {
            errors["q"] = [$"Пошукове значення q має містити не більше {SearchQueryMaxLength} символів."];
        }

        var sort = IncidentSearchSort.CreatedAtUtc;
        if (!string.IsNullOrEmpty(sortBy) && !SortAllowlist.TryGetValue(sortBy, out sort))
        {
            errors["sortBy"] = [$"Допустимі значення sortBy: {string.Join(", ", SortAllowlist.Keys)}."];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var results = await queries.SearchAsync(q, sort, cancellationToken);
        return Results.Ok(results);
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
