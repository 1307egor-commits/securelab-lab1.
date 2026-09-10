namespace SecureLab.Api.Data;

/// <summary>
/// Запис про зміну статусу інциденту. У ЛР 1 не має власного endpoint,
/// існує лише як приклад пов'язаної таблиці в seed.
/// </summary>
public class IncidentStatusChange
{
    public Guid Id { get; set; }

    public Guid IncidentId { get; set; }

    public Incident Incident { get; set; } = null!;

    public IncidentStatus FromStatus { get; set; }

    public IncidentStatus ToStatus { get; set; }

    public string ChangedByDisplayName { get; set; } = string.Empty;

    public DateTimeOffset ChangedAtUtc { get; set; }
}
