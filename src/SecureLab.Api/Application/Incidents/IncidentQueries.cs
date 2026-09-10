using Microsoft.EntityFrameworkCore;
using SecureLab.Api.Data;
using SecureLab.Api.Presentation.Contracts;

namespace SecureLab.Api.Application.Incidents;

/// <summary>
/// Read-only сценарії читання інцидентів. Кожен метод починає запит від
/// <c>dbContext.Incidents</c>, позначає його <see cref="EntityFrameworkQueryableExtensions.AsNoTracking{TEntity}"/>,
/// проєктує лише дозволені поля у response DTO і виконує запит асинхронно.
/// </summary>
public sealed class IncidentQueries(SecureLabDbContext dbContext)
{
    /// <summary>
    /// Список інцидентів із необов'язковим фільтром за статусом. Повертає
    /// колекцію DTO (можливо порожню), а не entity.
    /// </summary>
    public async Task<IReadOnlyList<IncidentListItemResponse>> GetListAsync(
        IncidentStatus? status,
        CancellationToken cancellationToken)
    {
        IQueryable<Incident> query = dbContext.Incidents.AsNoTracking();

        if (status is not null)
        {
            query = query.Where(incident => incident.Status == status);
        }

        return await query
            .OrderBy(incident => incident.OccurredAtUtc)
            .Select(incident => new IncidentListItemResponse(
                incident.Id,
                incident.Title,
                incident.Severity.ToString(),
                incident.Status.ToString(),
                incident.OccurredAtUtc,
                incident.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Деталі одного інциденту за id. Повертає <c>null</c>, якщо ресурсу немає —
    /// саме цей результат endpoint перетворює на 404. Проєкція навмисно не
    /// містить OwnerUserId, email власника та внутрішніх коментарів.
    /// </summary>
    public async Task<IncidentDetailsResponse?> GetDetailsAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await dbContext.Incidents
            .AsNoTracking()
            .Where(incident => incident.Id == id)
            .Select(incident => new IncidentDetailsResponse(
                incident.Id,
                incident.Title,
                incident.Description,
                incident.Severity.ToString(),
                incident.Status.ToString(),
                incident.OccurredAtUtc,
                incident.CreatedAtUtc,
                incident.Owner.DisplayName,
                incident.Comments
                    .Where(comment => !comment.IsInternal)
                    .OrderBy(comment => comment.CreatedAtUtc)
                    .Select(comment => new IncidentCommentResponse(
                        comment.AuthorDisplayName,
                        comment.Body,
                        comment.CreatedAtUtc))
                    .ToList()))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
