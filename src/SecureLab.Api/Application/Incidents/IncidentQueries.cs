using Microsoft.EntityFrameworkCore;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;
using SecureLab.Api.Presentation.Contracts;

namespace SecureLab.Api.Application.Incidents;

/// <summary>
/// Read-only сценарії читання інцидентів. Кожен метод починає запит від
/// <c>dbContext.Incidents</c>, позначає його <see cref="EntityFrameworkQueryableExtensions.AsNoTracking{TEntity}"/>,
/// проєктує лише дозволені поля у response DTO і виконує запит асинхронно.
/// </summary>
public sealed class IncidentQueries(SecureLabDbContext dbContext, ILogger<IncidentQueries> logger)
{
    /// <summary>
    /// Явний, сталий порядок елементів підсумку — за зростанням критичності.
    /// Не залежить від лексикографічного порядку тексту в SQL.
    /// </summary>
    private static readonly IncidentSeverity[] SeverityOrder =
        [IncidentSeverity.Low, IncidentSeverity.Medium, IncidentSeverity.High, IncidentSeverity.Critical];

    /// <summary>
    /// Список інцидентів із необов'язковим фільтром за статусом. Повертає
    /// колекцію DTO (можливо порожню), а не entity.
    /// </summary>
    public async Task<IReadOnlyList<IncidentListItemResponse>> GetListAsync(
        IncidentStatus? status,
        CancellationToken cancellationToken)
    {
        IQueryable<Incident> query = dbContext.Incidents.AsNoTracking();

        if (status is not null)
        {
            query = query.Where(incident => incident.Status == status);
        }

        return await query
            .OrderBy(incident => incident.OccurredAtUtc)
            .Select(incident => new IncidentListItemResponse(
                incident.Id,
                incident.Title,
                incident.Severity.ToString(),
                incident.Status.ToString(),
                incident.OccurredAtUtc,
                incident.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Максимальна кількість записів у відповіді пошуку.</summary>
    public const int SearchLimit = 50;

    /// <summary>Escape-символ для шаблонів ILIKE.</summary>
    private const string LikeEscapeCharacter = "\\";

    /// <summary>
    /// Пошук за підрядком у Title і Description (контракт 2-A). Значення <paramref name="q"/>
    /// ніколи не стає частиною SQL-тексту: EF Core передає шаблон окремим
    /// параметром, а SQL-структуру (ORDER BY) обирає серверний <paramref name="sort"/>.
    /// </summary>
    public async Task<IReadOnlyList<IncidentSearchItemResponse>> SearchAsync(
        string? q,
        IncidentSearchSort sort,
        CancellationToken cancellationToken)
    {
        IQueryable<Incident> query = dbContext.Incidents.AsNoTracking();

        if (!string.IsNullOrEmpty(q))
        {
            // Literal substring: %, _ та сам escape-символ екрануються, тому
            // "%" шукає знак відсотка, а не "будь-що".
            var pattern = "%" + EscapeLikePattern(q) + "%";
            query = query.Where(incident =>
                EF.Functions.ILike(incident.Title, pattern, LikeEscapeCharacter)
                || EF.Functions.ILike(incident.Description, pattern, LikeEscapeCharacter));
        }

        // Severity і Status зберігаються як текст, тому звичайний ORDER BY дав би
        // лексикографічний порядок. Явне ранжування задає порядок контракту.
        var ordered = sort switch
        {
            IncidentSearchSort.Severity => query
                .OrderBy(incident =>
                    incident.Severity == IncidentSeverity.Critical ? 0
                    : incident.Severity == IncidentSeverity.High ? 1
                    : incident.Severity == IncidentSeverity.Medium ? 2
                    : 3)
                .ThenByDescending(incident => incident.CreatedAtUtc),
            IncidentSearchSort.Status => query
                .OrderBy(incident =>
                    incident.Status == IncidentStatus.New ? 0
                    : incident.Status == IncidentStatus.Triaged ? 1
                    : incident.Status == IncidentStatus.InProgress ? 2
                    : incident.Status == IncidentStatus.Resolved ? 3
                    : 4)
                .ThenByDescending(incident => incident.CreatedAtUtc),
            _ => query.OrderByDescending(incident => incident.CreatedAtUtc)
        };

        return await ordered
            .ThenBy(incident => incident.Id)
            .Take(SearchLimit)
            .Select(incident => new IncidentSearchItemResponse(
                incident.Id,
                incident.Title,
                incident.Description,
                incident.Severity.ToString(),
                incident.Status.ToString(),
                incident.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    private static string EscapeLikePattern(string value) =>
        value
            .Replace(LikeEscapeCharacter, LikeEscapeCharacter + LikeEscapeCharacter, StringComparison.Ordinal)
            .Replace("%", LikeEscapeCharacter + "%", StringComparison.Ordinal)
            .Replace("_", LikeEscapeCharacter + "_", StringComparison.Ordinal);

    /// <summary>
    /// Деталі одного інциденту за id. Повертає <c>null</c>, якщо ресурсу немає —
    /// саме цей результат endpoint перетворює на 404. Проєкція навмисно не
    /// містить OwnerUserId, email власника та внутрішніх коментарів.
    /// </summary>
    public async Task<IncidentDetailsResponse?> GetDetailsAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await dbContext.Incidents
            .AsNoTracking()
            .Where(incident => incident.Id == id)
            .Select(incident => new IncidentDetailsResponse(
                incident.Id,
                incident.Title,
                incident.Description,
                incident.Severity.ToString(),
                incident.Status.ToString(),
                incident.OccurredAtUtc,
                incident.CreatedAtUtc,
                incident.Owner.DisplayName,
                incident.Comments
                    .Where(comment => !comment.IsInternal)
                    .OrderBy(comment => comment.CreatedAtUtc)
                    .Select(comment => new IncidentCommentResponse(
                        comment.AuthorDisplayName,
                        comment.Body,
                        comment.CreatedAtUtc))
                    .ToList()))
            .SingleOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Підсумок кількості інцидентів за рівнем критичності.
    /// Політика нульових груп — <b>повний перелік рівнів</b>: після агрегації в
    /// БД відсутні значення enum доповнюються елементами з <c>count: 0</c>.
    /// Порядок елементів — явний порядок критичності (<see cref="SeverityOrder"/>).
    /// </summary>
    /// <param name="status">
    /// Необов'язковий фільтр за статусом. null — усі інциденти. Значення вже
    /// перевірене endpoint за allowlist, тому тут приймається типізованим.
    /// </param>
    public async Task<IReadOnlyList<IncidentSeveritySummaryResponse>> GetSeveritySummaryAsync(
        IncidentStatus? status,
        CancellationToken cancellationToken)
    {
        IQueryable<Incident> query = dbContext.Incidents.AsNoTracking();

        if (status is not null)
        {
            query = query.Where(incident => incident.Status == status);
        }

        // Крок 1: матеріалізуємо агрегат із БД. GroupBy у SQL повертає лише
        // значення severity, наявні в таблиці (можливо, жодного).
        var aggregated = await query
            .GroupBy(incident => incident.Severity)
            .Select(group => new { Severity = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var counts = aggregated.ToDictionary(item => item.Severity, item => item.Count);

        // Крок 2: доповнюємо відсутні рівні нулями і впорядковуємо за критичністю.
        var summary = SeverityOrder
            .Select(severity => new IncidentSeveritySummaryResponse(
                severity.ToString(),
                counts.TryGetValue(severity, out var count) ? count : 0))
            .ToList();

        // Структуроване журналювання: шаблон повідомлення окремо від значень.
        // Придатні для summary параметри — назва операції та кількість груп;
        // описи інцидентів, connection string, cookies і токени сюди не потрапляють.
        logger.LogInformation(
            "Сформовано підсумок інцидентів за severity: {GroupCount} рівнів, фільтр статусу {StatusFilter}, знайдено груп у БД {NonEmptyGroups}",
            summary.Count,
            status?.ToString() ?? "усі",
            aggregated.Count);

        return summary;
    }
}
