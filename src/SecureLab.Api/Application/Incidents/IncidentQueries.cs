using Microsoft.EntityFrameworkCore;
using SecureLab.Api.Data;
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
