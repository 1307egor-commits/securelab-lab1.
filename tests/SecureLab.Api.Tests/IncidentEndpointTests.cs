using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SecureLab.Api.Data;
using Xunit;

namespace SecureLab.Api.Tests;

/// <summary>
/// Baseline-набір інтеграційних та security-regression тестів. Після виконання
/// етапу 3 усі ці тести мають лишатися зеленими.
/// </summary>
public sealed class IncidentEndpointTests : IClassFixture<SecureLabApiFactory>
{
    private readonly SecureLabApiFactory _factory;

    public IncidentEndpointTests(SecureLabApiFactory factory) => _factory = factory;

    [Fact]
    public async Task GetIncidents_ReturnsSeededIncidents()
    {
        var client = _factory.CreateClient();

        var incidents = await client.GetFromJsonAsync<List<JsonElement>>("/api/incidents");

        Assert.NotNull(incidents);
        Assert.True(incidents!.Count >= 2, "У seed має бути принаймні два інциденти.");

        var ids = incidents.Select(i => i.GetProperty("id").GetGuid()).ToHashSet();
        Assert.Contains(DbSeeder.IncidentPhishingId, ids);
        Assert.Contains(DbSeeder.IncidentPortScanId, ids);
    }

    [Fact]
    public async Task GetIncidentDetails_UnknownId_ReturnsProblemDetails404()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/incidents/99999999-9999-9999-9999-999999999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetIncidentDetails_DoesNotExposeOwnerUserIdOrEmail()
    {
        var client = _factory.CreateClient();

        var raw = await client.GetStringAsync($"/api/incidents/{DbSeeder.IncidentClassLogId}");

        Assert.DoesNotContain("ownerUserId", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email", raw, StringComparison.OrdinalIgnoreCase);
        // Внутрішній коментар не потрапляє у відповідь.
        Assert.DoesNotContain("Внутрішня примітка", raw, StringComparison.Ordinal);
    }

    [Fact]
    public void ClientScript_DoesNotUseDangerousInnerHtmlSink()
    {
        var appJs = File.ReadAllText(ClientAssets.AppJsPath());

        // Небезпечний DOM sink може перетворити недовірені дані на HTML.
        // Забороняємо його навіть у коментарях клієнтського коду.
        Assert.DoesNotContain("innerHTML", appJs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("outerHTML", appJs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("insertAdjacentHTML", appJs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("document.write", appJs, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>Пошук клієнтських файлів від каталогу збірки вгору до кореня репозиторію.</summary>
internal static class ClientAssets
{
    public static string AppJsPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "SecureLab.Api", "Client", "app.js");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Не знайдено src/SecureLab.Api/Client/app.js від каталогу тестів.");
    }
}
