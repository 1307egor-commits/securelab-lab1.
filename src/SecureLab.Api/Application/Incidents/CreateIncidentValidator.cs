using SecureLab.Api.Data.Entities;
using SecureLab.Api.Presentation.Contracts;

namespace SecureLab.Api.Application.Incidents;

/// <summary>Значення create-запиту, які пройшли серверну перевірку й нормалізацію.</summary>
public sealed record ValidatedCreateIncident(
    string Title,
    string Description,
    IncidentSeverity Severity,
    DateTimeOffset OccurredAtUtc);

/// <summary>
/// Серверна перевірка контракту створення інциденту (baseline 2-A). Виконується
/// до створення entity й до будь-якого доступу до БД. Браузерні required,
/// maxlength чи &lt;select&gt; лише покращують UX: інший HTTP-клієнт їх обходить.
/// </summary>
public static class CreateIncidentValidator
{
    public const int TitleMaxLength = 160;
    public const int DescriptionMaxLength = 4000;

    /// <summary>Мінімальна довжина description (після Trim) для High/Critical.</summary>
    public const int HighSeverityMinDescriptionLength = 40;

    /// <summary>Допуск розсинхронізації годинників для occurredAtUtc.</summary>
    public static readonly TimeSpan MaxFutureSkew = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Повертає нормалізовані значення або словник помилок за полями
    /// (ключі — імена JSON-полів). <paramref name="nowUtc"/> фіксують один раз
    /// на початку обробки запиту.
    /// </summary>
    public static (ValidatedCreateIncident? Value, Dictionary<string, string[]> Errors) Validate(
        CreateIncidentRequest request,
        DateTimeOffset nowUtc)
    {
        var errors = new Dictionary<string, string[]>();

        // Title і Description нормалізуються через Trim() рівно один раз; ці самі
        // значення далі йдуть у cross-field правило, перевірку конфлікту й entity.
        var title = request.Title?.Trim();
        var description = request.Description?.Trim();

        if (string.IsNullOrEmpty(title))
        {
            errors["title"] = ["Поле title є обов'язковим і не може складатися лише з пробілів."];
        }
        else if (title.Length > TitleMaxLength)
        {
            errors["title"] = [$"Поле title має містити не більше {TitleMaxLength} символів."];
        }

        if (string.IsNullOrEmpty(description))
        {
            errors["description"] = ["Поле description є обов'язковим і не може складатися лише з пробілів."];
        }
        else if (description.Length > DescriptionMaxLength)
        {
            errors["description"] = [$"Поле description має містити не більше {DescriptionMaxLength} символів."];
        }

        IncidentSeverity? severity = null;
        if (string.IsNullOrWhiteSpace(request.Severity))
        {
            errors["severity"] = ["Поле severity є обов'язковим. Допустимі значення: Low, Medium, High, Critical."];
        }
        else if (!Enum.TryParse<IncidentSeverity>(request.Severity, ignoreCase: true, out var parsed)
                 || !Enum.IsDefined(parsed)
                 // TryParse приймає також числа ("2") і списки ("Low, High" -> 0|2 = High),
                 // які проходять IsDefined. Контракт дозволяє лише назви елементів.
                 || !EnumNames.IsExactName<IncidentSeverity>(request.Severity))
        {
            errors["severity"] = ["Допустимі значення: Low, Medium, High, Critical."];
        }
        else
        {
            severity = parsed;
        }

        DateTimeOffset? occurredAtUtc = null;
        if (request.OccurredAtUtc is null)
        {
            errors["occurredAtUtc"] = ["Поле occurredAtUtc є обов'язковим."];
        }
        else if (request.OccurredAtUtc.Value > nowUtc + MaxFutureSkew)
        {
            errors["occurredAtUtc"] =
                [$"Поле occurredAtUtc не може випереджати поточний час сервера більш ніж на {MaxFutureSkew.TotalMinutes:0} хвилин."];
        }
        else
        {
            occurredAtUtc = request.OccurredAtUtc.Value.ToUniversalTime();
        }

        // Cross-field правило 2-A: для High/Critical опис після Trim() >= 40 символів.
        // Перевіряється лише на вже валідних severity і description.
        if (severity is IncidentSeverity.High or IncidentSeverity.Critical
            && !errors.ContainsKey("description")
            && description!.Length < HighSeverityMinDescriptionLength)
        {
            errors["description"] =
                [$"Для severity High або Critical поле description має містити щонайменше {HighSeverityMinDescriptionLength} символів (без початкових і кінцевих пробілів)."];
        }

        // Додаткове предметне правило (T-10): опис має пояснювати інцидент, а не
        // дослівно повторювати заголовок (порівняння після Trim(), без урахування регістру).
        if (!errors.ContainsKey("title") && !errors.ContainsKey("description")
            && string.Equals(title, description, StringComparison.OrdinalIgnoreCase))
        {
            errors["description"] = ["Поле description не може дослівно повторювати title."];
        }

        if (errors.Count > 0)
        {
            return (null, errors);
        }

        return (new ValidatedCreateIncident(title!, description!, severity!.Value, occurredAtUtc!.Value), errors);
    }
}
