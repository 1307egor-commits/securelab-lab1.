namespace SecureLab.Api.Data;

/// <summary>
/// Коментар до інциденту. Частина коментарів позначена як внутрішня
/// (<see cref="IsInternal"/>) і не повертається зовнішньому клієнту.
/// </summary>
public class IncidentComment
{
    public Guid Id { get; set; }

    public Guid IncidentId { get; set; }

    public Incident Incident { get; set; } = null!;

    public string AuthorDisplayName { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    /// <summary>true — коментар видимий лише персоналу і не входить у details response.</summary>
    public bool IsInternal { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
