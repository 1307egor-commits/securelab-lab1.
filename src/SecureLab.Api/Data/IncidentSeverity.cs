namespace SecureLab.Api.Data;

/// <summary>
/// Рівень критичності інциденту. Значення зберігається в PostgreSQL як текст
/// (HasConversion&lt;string&gt;()), тому сортування в SQL за цим стовпцем є
/// лексикографічним, а не порядком критичності.
/// </summary>
public enum IncidentSeverity
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}
