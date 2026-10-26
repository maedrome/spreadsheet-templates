using Api.Compras;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Api.Tests;

public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();

    // HTTPS because outside Development the admin cookies are Secure-only.
    public ApiFactory() => ClientOptions.BaseAddress = new Uri("https://localhost");

    public string StorageRoot { get; } = Path.Combine(Path.GetTempPath(), "spreadsheets-tests", Guid.NewGuid().ToString("N"));

    /// <summary>La API de Polar simulada: registra lo que se le pide y responde como Polar.</summary>
    public FakePolar Polar { get; } = new();

    public FakeEmailSender Emails { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test"); // sin el seed de desarrollo
        builder.UseSetting("ConnectionStrings:Default", _postgres.GetConnectionString());
        builder.UseSetting("Storage:Root", StorageRoot);
        builder.UseSetting("Site:BaseUrl", "https://tienda.test");
        builder.UseSetting("Polar:AccessToken", "polar_oat_test");
        builder.UseSetting("Polar:WebhookSecret", FakePolar.WebhookSecret);
        builder.UseSetting("Polar:Environment", "sandbox");
        builder.UseSetting("Resend:ApiKey", "re_test");
        builder.UseSetting("Email:From", "Tienda <compras@tienda.test>");
        builder.UseSetting("Checkout:PermitLimit", "1000");
        builder.UseSetting("Checkout:StatusPermitLimit", "1000");
        builder.UseSetting("Recovery:IpPermitLimit", "1000");
        builder.UseSetting("Recovery:EmailPermitLimit", "3");
        builder.ConfigureTestServices(services =>
        {
            services.AddHttpClient<PolarPaymentProvider>().ConfigurePrimaryHttpMessageHandler(() => Polar);
            services.AddSingleton<IEmailSender>(Emails);
        });
    }

    public Task InitializeAsync() => _postgres.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
        if (Directory.Exists(StorageRoot))
            Directory.Delete(StorageRoot, recursive: true);
    }
}
