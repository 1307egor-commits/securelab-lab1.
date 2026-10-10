using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SecureLab.Api.Data;

namespace SecureLab.Api.Tests;

/// <summary>
/// GET /api/incidents/search: позитивна регресія (T-04) і security regression
/// (S-01 до виправлення / S-02 після). Тести перевіряють множину результатів,
/// а не лише відсутність 500.
/// </summary>
public sealed class SearchSecurityTests(SecureLabApiFactory factory) : IClassFixture<SecureLabApiFactory>
{
    /// <summary>Lab02Seed: "Перевірка USB навчальної мережі".</summary>
    private static readonly Guid UsbIncidentId = Guid.Parse("20000000-0000-0000-0000-000000000005");

    /// <summary>Lab02Seed: "O'Brien classroom report".</summary>
    private static readonly Guid OBrienIncidentId = Guid.Parse("20000000-0000-0000-0000-000000000004");

    /// <summary>Єдиний дозволений контрольний read-only ввід із методики.</summary>
    internal const string ControlInput = "zz-no-match' OR TRUE -- ";

    private async Task<(HttpStatusCode Status, List<JsonElement> Rows)> SearchAsync(string query)
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(query);
        var rows = response.StatusCode == HttpStatusCode.OK
            ? (await response.Content.ReadFromJsonAsync<List<JsonElement>>())!
            : [];
        return (response.StatusCode, rows);
    }

    private static HashSet<Guid> Ids(IEnumerable<JsonElement> rows) =>
        rows.Select(row => row.GetProperty("id").GetGuid()).ToHashSet();

    // ---------------------------------------------------------------- T-04

    [Fact]
    public async Task T04_NormalSearch_Usb_ReturnsOnlyMatchingRecord()
    {
        var (status, rows) = await SearchAsync("/api/incidents/search?q=USB");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(new HashSet<Guid> { UsbIncidentId }, Ids(rows));
    }

    [Fact]
    public async Task T04_LegitimateApostrophe_FindsComputerClassIncident()
    {
        // Апостроф U+0027 — саме той символ, що в seed; не нормалізуємо його.
        const string word = "комп'ютерного";
        Assert.Contains((char)0x27, word);

        var (status, rows) = await SearchAsync($"/api/incidents/search?q={Uri.EscapeDataString(word)}");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains(DbSeeder.IncidentClassLogId, Ids(rows));
        Assert.All(rows, row => Assert.True(
            row.GetProperty("title").GetString()!.Contains(word, StringComparison.OrdinalIgnoreCase)
            || row.GetProperty("description").GetString()!.Contains(word, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task T04_LegitimateApostrophe_OBrien()
    {
        var (status, rows) = await SearchAsync("/api/incidents/search?q=O%27Brien");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(new HashSet<Guid> { OBrienIncidentId }, Ids(rows));
    }

    // ---------------------------------------------------------- S-01 / S-02

    [Fact]
    public async Task S02_ControlInput_DoesNotWidenResults()
    {
        // Еталон: той самий текст без SQL-метасимволів нічого не знаходить.
        var (baselineStatus, baselineRows) = await SearchAsync("/api/incidents/search?q=zz-no-match");
        Assert.Equal(HttpStatusCode.OK, baselineStatus);
        Assert.Empty(baselineRows);

        var (status, rows) = await SearchAsync(
            $"/api/incidents/search?q={Uri.EscapeDataString(ControlInput)}");

        // Ввід має лишатися даними: 200 і та сама (порожня) множина, жодного
        // зайвого чи чужого запису.
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Empty(rows);
        Assert.Equal(Ids(baselineRows), Ids(rows));
    }
    // ------------------------------------------- ILIKE метасимволи як літерали

    [Fact]
    public async Task Search_LikeMetacharacters_AreMatchedLiterally()
    {
        // Власний запис із %, _ і \ у title; title генерується один раз.
        var title = $"Literal-{Guid.NewGuid():N} диск 100% заповнено_" + @"\journal";
        using var client = factory.CreateClient();
        using var created = await client.PostAsJsonAsync("/api/incidents", new
        {
            title,
            description = "Перевірка буквального пошуку метасимволів ILIKE.",
            severity = "Low",
            occurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5)
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var createdId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        foreach (var literal in new[] { "%", "_", "\\", "100%" })
        {
            var (status, rows) = await SearchAsync($"/api/incidents/search?q={Uri.EscapeDataString(literal)}");

            Assert.Equal(HttpStatusCode.OK, status);
            Assert.Contains(createdId, Ids(rows));
            // Жоден запис без цього символу не повертається: % не означає "будь-що".
            Assert.All(rows, row => Assert.True(
                row.GetProperty("title").GetString()!.Contains(literal, StringComparison.Ordinal)
                || row.GetProperty("description").GetString()!.Contains(literal, StringComparison.Ordinal),
                $"Запис {row.GetProperty("id")} не містить '{literal}'"));
        }

        // Seed-записи без % не потрапляють у результат q=%.
        var (_, percentRows) = await SearchAsync("/api/incidents/search?q=%25");
        Assert.DoesNotContain(UsbIncidentId, Ids(percentRows));
    }

    // ------------------------------------------------------------ сортування

    private static readonly string[] SeverityRank = ["Critical", "High", "Medium", "Low"];
    private static readonly string[] StatusRank = ["New", "Triaged", "InProgress", "Resolved", "Closed"];

    [Fact]
    public async Task Search_WithoutSortBy_DefaultsToCreatedAtDescending()
    {
        var (status, rows) = await SearchAsync("/api/incidents/search");

        Assert.Equal(HttpStatusCode.OK, status);
        var created = rows.Select(row => row.GetProperty("createdAtUtc").GetDateTimeOffset()).ToList();
        Assert.True(created.Count >= 5);
        Assert.Equal(created.OrderByDescending(value => value), created);

        var (emptyStatus, emptyRows) = await SearchAsync("/api/incidents/search?sortBy=");
        Assert.Equal(HttpStatusCode.OK, emptyStatus);
        Assert.Equal(Ids(rows), Ids(emptyRows));
    }

    [Fact]
    public async Task Search_SortBySeverity_UsesCriticalityRankNotAlphabet()
    {
        var (status, rows) = await SearchAsync("/api/incidents/search?sortBy=severity");

        Assert.Equal(HttpStatusCode.OK, status);
        var ranks = rows.Select(row => Array.IndexOf(SeverityRank, row.GetProperty("severity").GetString())).ToList();
        Assert.DoesNotContain(-1, ranks);
        Assert.Equal(ranks.Order(), ranks);
        // Лексикографічно було б High < Low < Medium; тут Medium має йти перед Low.
        Assert.True(ranks.IndexOf(2) < ranks.IndexOf(3));
    }

    [Fact]
    public async Task Search_SortByStatus_UsesWorkflowRank()
    {
        var (status, rows) = await SearchAsync("/api/incidents/search?sortBy=status");

        Assert.Equal(HttpStatusCode.OK, status);
        var ranks = rows.Select(row => Array.IndexOf(StatusRank, row.GetProperty("status").GetString())).ToList();
        Assert.DoesNotContain(-1, ranks);
        Assert.Equal(ranks.Order(), ranks);
    }

    // ---------------------------------------------------------------- T-05

    [Theory]
    [InlineData("title")]
    [InlineData("created_at_utc DESC")]
    [InlineData("Severity")]
    public async Task T05_UnknownSortBy_Returns400WithSortByKey(string sortBy)
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync($"/api/incidents/search?q=USB&sortBy={Uri.EscapeDataString(sortBy)}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(JsonDocument.Parse(body).RootElement.GetProperty("errors").TryGetProperty("sortBy", out _));
        SafeErrors.AssertNoInternalDetails(body);
    }

    [Fact]
    public async Task Search_TooLongQuery_Returns400WithQKey()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync($"/api/incidents/search?q={new string('x', 201)}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("q", out _));
    }

    // ---------------------------------------------------------------- A-02

    [Theory]
    [InlineData("2")]
    [InlineData("New, Triaged")]
    public async Task A02_ListStatusFilter_NumericOrCombinedAlias_Returns400(string status)
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync($"/api/incidents?status={Uri.EscapeDataString(status)}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        // Позитивний контроль: точна назва (без урахування регістру) працює.
        using var valid = await client.GetAsync("/api/incidents?status=triaged");
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
    }
}
