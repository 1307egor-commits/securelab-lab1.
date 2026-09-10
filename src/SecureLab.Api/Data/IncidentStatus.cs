namespace SecureLab.Api.Data;

/// <summary>
/// Стан обробки інциденту. Зберігається як текст у PostgreSQL. Клієнтський
/// фільтр надсилає це значення рядком; сервер перевіряє його через
/// Enum.TryParse + Enum.IsDefined і повертає 400 для невідомого значення.
/// </summary>
public enum IncidentStatus
{
    New = 0,
    Triaged = 1,
    InProgress = 2,
    Resolved = 3,
    Closed = 4
}
