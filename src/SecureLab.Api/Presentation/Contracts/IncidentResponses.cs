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
