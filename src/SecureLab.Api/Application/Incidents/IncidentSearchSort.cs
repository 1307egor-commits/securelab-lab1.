namespace SecureLab.Api.Application.Incidents;

/// <summary>
/// Логічні варіанти сортування пошуку. Клієнт обирає один із них через
/// allowlist у endpoint; назва колонки чи SQL-вираз від клієнта не приймаються.
/// </summary>
public enum IncidentSearchSort
{
    CreatedAtUtc,
    Severity,
    Status
}
