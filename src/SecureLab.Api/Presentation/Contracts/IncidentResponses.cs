namespace SecureLab.Api.Presentation.Contracts;

/// <summary>
/// Зовнішній контракт одного елемента списку інцидентів. Це НЕ entity:
///повертаються лише дозволені поля, severity та status — як рядки.
/// </summary>
public sealed record IncidentListItemResponse(
    Guid Id,
    string Title,
    string Severity,
    string Status,
    DateTimeOffset OccurredAtUtc,
    DateTimeOffset CreatedAtUtc);

/// <summary>
/// Зовнішній контракт деталей одного інциденту. Явна проєкція: тут немає
/// OwnerUserId, email власника чи внутрішніх коментарів.
/// </summary>
public sealed record IncidentDetailsResponse(
    Guid Id,
    string Title,
    string Description,
    string Severity,
    string Status,
    DateTimeOffset OccurredAtUtc,
    DateTimeOffset CreatedAtUtc,
    string OwnerDisplayName,
    IReadOnlyList<IncidentCommentResponse> Comments);

/// <summary>Дозволений до показу коментар (лише не внутрішні).</summary>
public sealed record IncidentCommentResponse(
    string AuthorDisplayName,
    string Body,
    DateTimeOffset CreatedAtUtc);

/// <summary>
/// Один елемент підсумку інцидентів за рівнем критичності. Це агрегат, а не
/// entity: лише пара severity + count. Порядок елементів у відповіді —
/// явний порядок критичності (Low, Medium, High, Critical), задокументований
/// у контракті, а не лексикографічний порядок SQL.
/// </summary>
public sealed record IncidentSeveritySummaryResponse(
    string Severity,
    int Count);

/// <summary>
/// Відповідь 201 Created для POST /api/incidents. Окремий response DTO, а не
/// entity: без OwnerUserId, зв'язків і внутрішніх полів.
/// </summary>
public sealed record CreatedIncidentResponse(
    Guid Id,
    string Title,
    string Severity,
    string Status,
    DateTimeOffset OccurredAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

/// <summary>
/// Елемент результату GET /api/incidents/search. Форма збігається з відповіддю
/// наданого scaffold, але це явний DTO, а не анонімна проєкція entity.
/// </summary>
public sealed record IncidentSearchItemResponse(
    Guid Id,
    string Title,
    string Description,
    string Severity,
    string Status,
    DateTimeOffset CreatedAtUtc);
