using Microsoft.EntityFrameworkCore;
using Npgsql;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;
using SecureLab.Api.Presentation.Contracts;

namespace SecureLab.Api.Application.Incidents;

/// <summary>
/// Команди зміни стану інцидентів. Отримує вже перевірені значення
/// (<see cref="ValidatedCreateIncident"/>), а всі server-managed поля визначає сам.
/// </summary>
public sealed class IncidentCommands(SecureLabDbContext dbContext, ILogger<IncidentCommands> logger)
{
    /// <summary>
    /// Тимчасовий навчальний контекст ЛР 02: власник нового інциденту — Alice.
    /// Значення не приходить із body; у ЛР 03 його замінить verified principal.
    /// </summary>
    private static readonly Guid DemoOwnerUserId = DbSeeder.AliceId;

    /// <summary>
    /// Створює інцидент. Повертає <c>null</c>, якщо вже існує не закритий інцидент
    /// з тим самим (після Trim, з урахуванням регістру) title — endpoint перетворює
    /// це на 409 Conflict.
    /// </summary>
    public async Task<CreatedIncidentResponse?> CreateAsync(
        ValidatedCreateIncident request,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        // Предметний інваріант: New, Triaged, InProgress і Resolved блокують повтор,
        // Closed — ні. Оператор == у PostgreSQL порівнює з урахуванням регістру.
        var hasActiveDuplicate = await dbContext.Incidents
            .AsNoTracking()
            .AnyAsync(
                incident => incident.Title == request.Title && incident.Status != IncidentStatus.Closed,
                cancellationToken);

        if (hasActiveDuplicate)
        {
            return null;
        }

        var incident = new Incident
        {
            Id = Guid.NewGuid(),
            OwnerUserId = DemoOwnerUserId,
            Title = request.Title,
            Description = request.Description,
            Severity = request.Severity,
            Status = IncidentStatus.New,
            OccurredAtUtc = request.OccurredAtUtc,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };

        dbContext.Incidents.Add(incident);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: SecureLabDbContext.ActiveIncidentTitleIndex
        })
        {
            // Паралельний запит встиг створити такий самий активний інцидент між
            // AnyAsync і INSERT. Це очікуваний предметний конфлікт, а не 500.
            logger.LogInformation("Паралельне створення інциденту з активним дублікатом title відхилено");
            return null;
        }

        logger.LogInformation("Створено інцидент {IncidentId} із severity {Severity}", incident.Id, incident.Severity);

        return new CreatedIncidentResponse(
            incident.Id,
            incident.Title,
            incident.Severity.ToString(),
            incident.Status.ToString(),
            incident.OccurredAtUtc,
            incident.CreatedAtUtc,
            incident.UpdatedAtUtc);
    }
}
