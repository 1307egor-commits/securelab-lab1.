using Microsoft.EntityFrameworkCore;

namespace SecureLab.Api.Data;

/// <summary>
/// Відтворюваний seed і reset навчальних даних. UUID фіксовані навмисно —
/// їх можна використовувати у .http-сценаріях і тестах.
/// </summary>
public static class DbSeeder
{
    // --- Користувачі -------------------------------------------------------
    public static readonly Guid AliceId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid BobId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    public static readonly Guid MorganId = Guid.Parse("10000000-0000-0000-0000-000000000003");
    public static readonly Guid AdminId = Guid.Parse("10000000-0000-0000-0000-000000000004");

    // --- Інциденти -------------------------------------------------------
    public static readonly Guid IncidentPhishingId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public static readonly Guid IncidentPortScanId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    public static readonly Guid IncidentClassLogId = Guid.Parse("20000000-0000-0000-0000-000000000003");

    /// <summary>
    /// Застосовує наявні migrations і, якщо таблиці порожні, заповнює їх
    /// фіксованими навчальними даними. Безпечно викликати під час кожного
    /// Development-запуску.
    /// </summary>
    public static async Task SeedAsync(SecureLabDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.MigrateAsync(cancellationToken);

        if (await db.Incidents.AnyAsync(cancellationToken))
        {
            return;
        }

        await PopulateAsync(db, cancellationToken);
    }

    /// <summary>
    /// Очищує лише відомі навчальні таблиці й повторно заповнює їх seed-значеннями.
    /// Викликається службовою командою <c>--reset-database</c> лише в Development.
    /// </summary>
    public static async Task ResetAsync(SecureLabDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.MigrateAsync(cancellationToken);

        db.IncidentStatusChanges.RemoveRange(db.IncidentStatusChanges);
        db.IncidentComments.RemoveRange(db.IncidentComments);
        db.Incidents.RemoveRange(db.Incidents);
        db.Users.RemoveRange(db.Users);
        await db.SaveChangesAsync(cancellationToken);

        await PopulateAsync(db, cancellationToken);
    }

    private static async Task PopulateAsync(SecureLabDbContext db, CancellationToken cancellationToken)
    {
        var alice = new User { Id = AliceId, DisplayName = "Alice", Email = "alice@securelab.local", Role = "Analyst" };
        var bob = new User { Id = BobId, DisplayName = "Bob", Email = "bob@securelab.local", Role = "Analyst" };
        var morgan = new User { Id = MorganId, DisplayName = "Morgan", Email = "morgan@securelab.local", Role = "Analyst" };
        var admin = new User { Id = AdminId, DisplayName = "Admin", Email = "admin@securelab.local", Role = "Admin" };

        db.Users.AddRange(alice, bob, morgan, admin);

        var phishing = new Incident
        {
            Id = IncidentPhishingId,
            Title = "Підозрілий лист із вкладенням",
            Description = "Користувач повідомив про лист із вкладенням .docm від невідомого відправника. " +
                          "Вкладення не відкривали, лист переміщено на карантин для аналізу.",
            Severity = IncidentSeverity.Medium,
            Status = IncidentStatus.Triaged,
            OccurredAtUtc = new DateTimeOffset(2026, 8, 1, 8, 30, 0, TimeSpan.Zero),
            CreatedAtUtc = new DateTimeOffset(2026, 8, 1, 9, 0, 0, TimeSpan.Zero),
            OwnerUserId = AliceId
        };

        var portScan = new Incident
        {
            Id = IncidentPortScanId,
            Title = "Масове сканування портів із зовнішньої адреси",
            Description = "Межевий фаєрвол зафіксував послідовне сканування 1–65535 портів з однієї зовнішньої IP-адреси. " +
                          "Активних сесій не встановлено, адресу додано до списку блокування.",
            Severity = IncidentSeverity.High,
            Status = IncidentStatus.New,
            OccurredAtUtc = new DateTimeOffset(2026, 8, 3, 22, 15, 0, TimeSpan.Zero),
            CreatedAtUtc = new DateTimeOffset(2026, 8, 3, 22, 40, 0, TimeSpan.Zero),
            OwnerUserId = BobId
        };

        var classLog = new Incident
        {
            Id = IncidentClassLogId,
            Title = "Перевірка журналу комп'ютерного класу",
            // Навмисно містить фрагмент <script>. Клієнт має показати його як текст,
            // а не виконати. Дані з БД не є автоматично довіреним HTML.
            Description = "Лаборант повідомив про сторонній запис у журналі доступу. " +
                          "Приклад підозрілого рядка з журналу: <script>alert('xss')</script> — його слід показувати як звичайний текст.",
            Severity = IncidentSeverity.Low,
            Status = IncidentStatus.New,
            OccurredAtUtc = new DateTimeOffset(2026, 8, 5, 11, 0, 0, TimeSpan.Zero),
            CreatedAtUtc = new DateTimeOffset(2026, 8, 5, 11, 20, 0, TimeSpan.Zero),
            OwnerUserId = MorganId
        };

        db.Incidents.AddRange(phishing, portScan, classLog);

        // Два публічні коментарі...
        db.IncidentComments.AddRange(
            new IncidentComment
            {
                Id = Guid.Parse("30000000-0000-0000-0000-000000000001"),
                IncidentId = IncidentPhishingId,
                AuthorDisplayName = "Bob",
                Body = "Перевірив заголовки листа: SPF fail, домен зареєстровано два дні тому.",
                IsInternal = false,
                CreatedAtUtc = new DateTimeOffset(2026, 8, 1, 9, 15, 0, TimeSpan.Zero)
            },
            new IncidentComment
            {
                Id = Guid.Parse("30000000-0000-0000-0000-000000000002"),
                IncidentId = IncidentClassLogId,
                AuthorDisplayName = "Alice",
                Body = "Журнал вивантажено, аномальний запис локалізовано за часовою міткою.",
                IsInternal = false,
                CreatedAtUtc = new DateTimeOffset(2026, 8, 5, 12, 0, 0, TimeSpan.Zero)
            },
            // ...і один внутрішній коментар, який НЕ входить у details response.
            new IncidentComment
            {
                Id = Guid.Parse("30000000-0000-0000-0000-000000000003"),
                IncidentId = IncidentClassLogId,
                AuthorDisplayName = "Admin",
                Body = "Внутрішня примітка: не розголошувати ім'я лаборанта у зовнішньому звіті.",
                IsInternal = true,
                CreatedAtUtc = new DateTimeOffset(2026, 8, 5, 12, 30, 0, TimeSpan.Zero)
            });

        // Одна зміна статусу.
        db.IncidentStatusChanges.Add(new IncidentStatusChange
        {
            Id = Guid.Parse("40000000-0000-0000-0000-000000000001"),
            IncidentId = IncidentPhishingId,
            FromStatus = IncidentStatus.New,
            ToStatus = IncidentStatus.Triaged,
            ChangedByDisplayName = "Morgan",
            ChangedAtUtc = new DateTimeOffset(2026, 8, 1, 9, 5, 0, TimeSpan.Zero)
        });

        await db.SaveChangesAsync(cancellationToken);
    }
}
