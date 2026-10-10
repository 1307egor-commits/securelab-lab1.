namespace SecureLab.Api.Application;

/// <summary>
/// Строга перевірка рядкового значення enum. <c>Enum.TryParse</c> + <c>Enum.IsDefined</c>
/// відсіюють "7", але пропускають числові псевдоніми ("2") і списки прапорців
/// ("Low, High"), бо їхній результат теж є визначеним елементом. Контракт API
/// дозволяє лише назви елементів, тому значення має збігатися з однією з назв.
/// </summary>
public static class EnumNames
{
    public static bool IsExactName<TEnum>(string? value) where TEnum : struct, Enum =>
        value is not null
        && Enum.GetNames<TEnum>().Any(name => string.Equals(name, value.Trim(), StringComparison.OrdinalIgnoreCase));
}
