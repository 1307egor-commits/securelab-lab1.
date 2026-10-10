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
}
