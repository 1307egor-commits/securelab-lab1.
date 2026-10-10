using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SecureLab.Api.Data;
using Xunit;

namespace SecureLab.Api.Tests;

/// <summary>
/// Піднімає застосунок у середовищі Development, тому тести звертаються до тієї
/// самої локальної навчальної PostgreSQL, що й звичайний стенд. Ізоляцію
/// тестових даних забезпечує відтворюваний seed/reset, а не окрема база.
/// </summary>
public sealed class SecureLabApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        return base.CreateHost(builder);
    }

    public async Task InitializeAsync()
    {
        await DatabaseBootstrap.InitializeAsync(Services, reset: true);
    }

    public new Task DisposeAsync() => Task.CompletedTask;
}
