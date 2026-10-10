namespace SecureLab.Api.Data;

/// <summary>
/// Єдина точка підготовки навчальної БД: migrations, (за потреби) reset і seed.
/// Її використовують і звичайний Development-запуск, і команда
/// <c>--reset-database</c>, і тестова фабрика, тому додаткові seed-кроки
/// scaffold-ів підключаються в одному місці.
/// </summary>
public static class DatabaseBootstrap
{
    public static async Task InitializeAsync(IServiceProvider services, bool reset)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SecureLabDbContext>();

        if (reset)
        {
            await DbSeeder.ResetAsync(dbContext);
        }

        await DbSeeder.SeedAsync(dbContext);
    }
}
