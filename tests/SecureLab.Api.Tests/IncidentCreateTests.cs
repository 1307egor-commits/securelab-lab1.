using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;

namespace SecureLab.Api.Tests;

/// <summary>
/// Контракт POST /api/incidents (baseline 2-A): T-01 коректне створення,
/// T-02 некоректний DTO, T-03 предметний конфлікт, T-09 cross-field правило,
/// T-10 додаткове предметне правило, A-02 overposting / строгий enum.
/// </summary>
public sealed class IncidentCreateTests(SecureLabApiFactory factory) : IClassFixture<SecureLabApiFactory>
{
    private const string ValidDescription = "Навчальний опис інциденту для перевірки контракту створення.";

    private static string NewTitle() => $"Regression-{Guid.NewGuid():N}";

    private static string NowIso(TimeSpan offset = default) =>
        DateTimeOffset.UtcNow.Add(offset).ToString("O");

    private HttpClient Client() => factory.CreateClient();

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    private static object ValidBody(string title, string severity = "Medium", string? description = null) => new
    {
        title,
        description = description ?? ValidDescription,
        severity,
        occurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-30)
    };

    /// <summary>
    /// Спільна перевірка 400: статус, application/problem+json, помилка саме для
    /// очікуваного поля й відсутність внутрішніх деталей у тілі.
    /// </summary>
    private static async Task AssertValidationProblemAsync(HttpResponseMessage response, string expectedKey)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(body);
        var errors = document.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty(expectedKey, out var messages), $"Немає помилки для '{expectedKey}': {body}");
        Assert.True(messages.GetArrayLength() > 0);

        SafeErrors.AssertNoInternalDetails(body);
    }

    // ---------------------------------------------------------------- T-01

    [Fact]
    public async Task T01_ValidRequest_Returns201WithResponseDtoOnly()
    {
        var title = NewTitle();
        using var response = await Client().PostAsJsonAsync("/api/incidents", ValidBody($"  {title}  ", "high"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Лише поля response DTO: без ownerUserId, description, зв'язків.
        var names = created.EnumerateObject().Select(property => property.Name).Order().ToArray();
        Assert.Equal(
            new[] { "createdAtUtc", "id", "occurredAtUtc", "severity", "status", "title", "updatedAtUtc" },
            names);

        Assert.Equal(title, created.GetProperty("title").GetString()); // Trim() застосовано
        Assert.Equal("High", created.GetProperty("severity").GetString());
        Assert.Equal("New", created.GetProperty("status").GetString());
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal($"/api/incidents/{id}", response.Headers.Location?.OriginalString);

        // Створений ресурс доступний через details ЛР 1.
        using var details = await Client().GetAsync($"/api/incidents/{id}");
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
    }

    [Fact]
    public async Task T01_OccurredAtWithinFiveMinuteSkew_IsAccepted()
    {
        var body = $$"""
            {"title":"{{NewTitle()}}","description":"{{ValidDescription}}","severity":"Low","occurredAtUtc":"{{NowIso(TimeSpan.FromMinutes(4))}}"}
            """;
        using var response = await Client().PostAsync("/api/incidents", Json(body));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ---------------------------------------------------------------- T-02

    [Fact]
    public async Task T02_SeveritySeven_IsRejectedByIsDefined_Returns400()
    {
        // Enum.TryParse("7") успішно дає (IncidentSeverity)7, але Enum.IsDefined відхиляє.
        Assert.True(Enum.TryParse<IncidentSeverity>("7", true, out var parsed));
        Assert.False(Enum.IsDefined(parsed));

        using var response = await Client().PostAsJsonAsync("/api/incidents", ValidBody(NewTitle(), "7"));
        await AssertValidationProblemAsync(response, "severity");
    }

    [Theory]
    [InlineData("Extreme")]
    [InlineData("")]
    [InlineData("2")]          // числовий псевдонім High: TryParse + IsDefined його пропускають
    [InlineData("Low, High")]  // список прапорців: 0 | 2 = High, теж проходить IsDefined
    public async Task T02_InvalidSeverity_Returns400(string severity)
    {
        using var response = await Client().PostAsJsonAsync("/api/incidents", ValidBody(NewTitle(), severity));
        await AssertValidationProblemAsync(response, "severity");
    }

    [Fact]
    public async Task T02_WhitespaceTitle_Returns400()
    {
        using var response = await Client().PostAsJsonAsync("/api/incidents", ValidBody("   "));
        await AssertValidationProblemAsync(response, "title");
    }

    [Fact]
    public async Task T02_TitleLongerThan160_Returns400()
    {
        using var response = await Client().PostAsJsonAsync("/api/incidents", ValidBody(new string('T', 161)));
        await AssertValidationProblemAsync(response, "title");
    }

    [Fact]
    public async Task T02_DescriptionLongerThan4000_Returns400()
    {
        using var response = await Client().PostAsJsonAsync(
            "/api/incidents", ValidBody(NewTitle(), description: new string('d', 4001)));
        await AssertValidationProblemAsync(response, "description");
    }

    [Fact]
    public async Task T02_MissingFields_Returns400ForEachField()
    {
        using var response = await Client().PostAsync("/api/incidents", Json("{}"));
        await AssertValidationProblemAsync(response, "title");

        // Повторно читаємо тіло, щоб перевірити всі ключі одночасно.
        using var again = await Client().PostAsync("/api/incidents", Json("{}"));
        var errors = (await again.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        foreach (var key in new[] { "title", "description", "severity", "occurredAtUtc" })
        {
            Assert.True(errors.TryGetProperty(key, out _), $"Очікувалась помилка для {key}");
        }
    }

    [Fact]
    public async Task T02_MissingOccurredAtUtc_Returns400()
    {
        var body = $$"""
            {"title":"{{NewTitle()}}","description":"{{ValidDescription}}","severity":"Low"}
            """;
        using var response = await Client().PostAsync("/api/incidents", Json(body));
        await AssertValidationProblemAsync(response, "occurredAtUtc");
    }

    [Fact]
    public async Task T02_OccurredAtMoreThanFiveMinutesInFuture_Returns400()
    {
        var body = $$"""
            {"title":"{{NewTitle()}}","description":"{{ValidDescription}}","severity":"Low","occurredAtUtc":"{{NowIso(TimeSpan.FromMinutes(10))}}"}
            """;
        using var response = await Client().PostAsync("/api/incidents", Json(body));
        await AssertValidationProblemAsync(response, "occurredAtUtc");
    }

    [Fact]
    public async Task T02_MalformedJson_Returns400ProblemDetailsWithoutInternals()
    {
        using var response = await Client().PostAsync("/api/incidents", Json("{\"title\": "));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        SafeErrors.AssertNoInternalDetails(body);
    }

    // ---------------------------------------------------------------- T-03

    [Fact]
    public async Task T03_DuplicateActiveTitle_Returns409_ThenCaseAndClosedRulesHold()
    {
        // Title генерується один раз і використовується в усіх запитах сценарію.
        var title = $"Regression-{Guid.NewGuid():N}";

        using var first = await Client().PostAsJsonAsync("/api/incidents", ValidBody(title));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstId = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // Той самий title (навіть із пробілами навколо) при статусі New -> 409.
        using var duplicate = await Client().PostAsJsonAsync("/api/incidents", ValidBody($" {title} "));
        var duplicateBody = await duplicate.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("application/problem+json", duplicate.Content.Headers.ContentType?.MediaType);
        Assert.Equal(409, JsonDocument.Parse(duplicateBody).RootElement.GetProperty("status").GetInt32());
        SafeErrors.AssertNoInternalDetails(duplicateBody);

        // Порівняння чутливе до регістру: інший регістр — інший заголовок.
        using var otherCase = await Client().PostAsJsonAsync("/api/incidents", ValidBody(title.ToUpperInvariant()));
        Assert.Equal(HttpStatusCode.Created, otherCase.StatusCode);

        // Resolved усе ще блокує повторне створення.
        await SetStatusAsync(firstId, IncidentStatus.Resolved);
        using var whileResolved = await Client().PostAsJsonAsync("/api/incidents", ValidBody(title));
        Assert.Equal(HttpStatusCode.Conflict, whileResolved.StatusCode);

        // Closed повторне створення не блокує.
        await SetStatusAsync(firstId, IncidentStatus.Closed);
        using var afterClosed = await Client().PostAsJsonAsync("/api/incidents", ValidBody(title));
        Assert.Equal(HttpStatusCode.Created, afterClosed.StatusCode);
    }

    [Theory]
    [InlineData(IncidentStatus.Triaged)]
    [InlineData(IncidentStatus.InProgress)]
    public async Task T03_DuplicateOfTriagedOrInProgress_Returns409(IncidentStatus existingStatus)
    {
        var title = NewTitle();
        using var first = await Client().PostAsJsonAsync("/api/incidents", ValidBody(title));
        var id = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await SetStatusAsync(id, existingStatus);

        using var duplicate = await Client().PostAsJsonAsync("/api/incidents", ValidBody(title));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    // ---------------------------------------------------------------- T-09

    [Theory]
    [InlineData("High")]
    [InlineData("Critical")]
    public async Task T09_HighOrCritical_Description39Rejected_40Accepted(string severity)
    {
        // Пробіли навколо доводять, що довжина рахується після Trim().
        var description39 = "  " + new string('a', 39) + "  ";
        var description40 = "  " + new string('a', 40) + "  ";

        using var rejected = await Client().PostAsJsonAsync(
            "/api/incidents", ValidBody(NewTitle(), severity, description39));
        await AssertValidationProblemAsync(rejected, "description");

        using var accepted = await Client().PostAsJsonAsync(
            "/api/incidents", ValidBody(NewTitle(), severity, description40));
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
    }

    [Fact]
    public async Task T09_LowSeverity_ShortDescriptionIsAllowed()
    {
        using var response = await Client().PostAsJsonAsync(
            "/api/incidents", ValidBody(NewTitle(), "Low", "Коротко."));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ---------------------------------------------------------------- T-10

    [Fact]
    public async Task T10_DescriptionRepeatingTitle_Rejected_ExtendedDescriptionAccepted()
    {
        var title = NewTitle();

        using var rejected = await Client().PostAsJsonAsync(
            "/api/incidents", ValidBody(title, "Low", "  " + title.ToLowerInvariant() + " "));
        await AssertValidationProblemAsync(rejected, "description");

        using var accepted = await Client().PostAsJsonAsync(
            "/api/incidents", ValidBody(title, "Low", title + ": деталі спостереження в журналі."));
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
    }

    // ---------------------------------------------------------------- A-02

    [Fact]
    public async Task A02_ServerManagedFieldsInBody_AreIgnored()
    {
        var attackerChosenId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var body = $$"""
            {
              "id": "{{attackerChosenId}}",
              "ownerUserId": "{{DbSeeder.BobId}}",
              "status": "Closed",
              "createdAtUtc": "2000-01-01T00:00:00Z",
              "updatedAtUtc": "2000-01-01T00:00:00Z",
              "title": "{{NewTitle()}}",
              "description": "{{ValidDescription}}",
              "severity": "Low",
              "occurredAtUtc": "{{NowIso(TimeSpan.FromMinutes(-1))}}"
            }
            """;
        var before = DateTimeOffset.UtcNow.AddMinutes(-1);

        using var response = await Client().PostAsync("/api/incidents", Json(body));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        Assert.NotEqual(attackerChosenId, id);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecureLabDbContext>();
        var stored = await db.Incidents.AsNoTracking().SingleAsync(incident => incident.Id == id);

        Assert.Equal(DbSeeder.AliceId, stored.OwnerUserId);   // demo context, не body
        Assert.Equal(IncidentStatus.New, stored.Status);      // серверне правило
        Assert.True(stored.CreatedAtUtc >= before);           // серверний час
        Assert.Equal(stored.CreatedAtUtc, stored.UpdatedAtUtc);
        Assert.False(await db.Incidents.AnyAsync(incident => incident.Id == attackerChosenId));
    }

    private async Task SetStatusAsync(Guid id, IncidentStatus status)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecureLabDbContext>();
        var incident = await db.Incidents.SingleAsync(row => row.Id == id);
        incident.Status = status;
        await db.SaveChangesAsync();
    }
}

/// <summary>Перевірка, що тіло помилки не розкриває внутрішніх деталей.</summary>
internal static class SafeErrors
{
    private static readonly string[] Forbidden =
    [
        "Exception", "StackTrace", "   at ", "Npgsql", "SELECT", "Host=", "Password", "SecureLab.Api.", "incidents"
    ];

    public static void AssertNoInternalDetails(string body)
    {
        foreach (var marker in Forbidden)
        {
            Assert.DoesNotContain(marker, body, StringComparison.OrdinalIgnoreCase);
        }
    }
}
