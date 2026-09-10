namespace SecureLab.Api.Data;

/// <summary>
/// Інцидент безпеки — основна entity стенда. Зберігається в таблиці
/// <c>incidents</c>. Enum-поля <see cref="Severity"/> та <see cref="Status"/>
/// зберігаються як текст (див. <see cref="SecureLabDbContext.OnModelCreating"/>).
/// </summary>
public class Incident
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Довільний текст, що міг походити від попереднього користувацького вводу.
    /// Seed-інцидент навмисно містить тут фрагмент &lt;script&gt;, який клієнт
    /// має показати як звичайний текст.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    public IncidentSeverity Severity { get; set; }

    public IncidentStatus Status { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    // Власник інциденту. OwnerUserId та Owner.Email є внутрішніми полями
    // і не входять до зовнішнього контракту деталей.
    public Guid OwnerUserId { get; set; }

    public User Owner { get; set; } = null!;

    public ICollection<IncidentComment> Comments { get; set; } = new List<IncidentComment>();

    public ICollection<IncidentStatusChange> StatusChanges { get; set; } = new List<IncidentStatusChange>();
}
