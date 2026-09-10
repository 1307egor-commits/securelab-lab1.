namespace SecureLab.Api.Data;

/// <summary>
/// Обліковий запис користувача навчального стенда. Entity описує модель
/// збереження: тут є поля (Email), які не належать до зовнішнього контракту
/// жодної відповіді API.
/// </summary>
public class User
{
    public Guid Id { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Внутрішнє поле. НЕ потрапляє до жодного response DTO.</summary>
    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = "Analyst";

    public ICollection<Incident> OwnedIncidents { get; set; } = new List<Incident>();
}
