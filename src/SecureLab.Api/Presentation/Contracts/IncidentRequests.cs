namespace SecureLab.Api.Presentation.Contracts;

/// <summary>
/// Зовнішній контракт POST /api/incidents — лише поля, які дозволено надсилати
/// клієнту. Id, OwnerUserId, Status, CreatedAtUtc та UpdatedAtUtc сюди навмисно
/// не входять: їх визначає сервер, тому JSON-поле "ownerUserId" чи "status"
/// просто не має куди потрапити (захист від overposting / mass assignment).
/// Властивості nullable чесно відображають, що HTTP-клієнт може поле не надіслати;
/// обов'язковість забезпечує серверна перевірка, а не annotation.
/// </summary>
public sealed record CreateIncidentRequest(
    string? Title,
    string? Description,
    string? Severity,
    DateTimeOffset? OccurredAtUtc);
