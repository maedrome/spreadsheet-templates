using System.Threading.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Api.Compras;

/// <summary>Checkout con Polar, webhooks, Enlaces de compra y emails.</summary>
public static class ComprasSetup
{
    public const string CheckoutRateLimit = "checkout";
    public const string StatusRateLimit = "checkout-estado";
    public const string RecoveryRateLimit = "recuperar";

    public static void AddCompras(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        var development = builder.Environment.IsDevelopment();
        services.TryAddSingleton(TimeProvider.System);

        // Fuera de desarrollo, sin estos secretos la API no arranca. En desarrollo van en dotnet user-secrets.
        services.AddOptions<PolarOptions>().BindConfiguration(PolarOptions.Section)
            .Validate(o => development || (o.IsConfigured && !string.IsNullOrWhiteSpace(o.WebhookSecret)),
                "Faltan Polar:AccessToken y Polar:WebhookSecret.")
            .Validate(o => o.IsSandbox || o.Environment.Equals("production", StringComparison.OrdinalIgnoreCase),
                "Polar:Environment tiene que ser sandbox o production.")
            .ValidateOnStart();
        services.AddOptions<ResendOptions>().BindConfiguration(ResendOptions.Section)
            .Validate(o => development || !string.IsNullOrWhiteSpace(o.ApiKey), "Falta Resend:ApiKey.")
            .ValidateOnStart();
        services.AddOptions<EmailOptions>().BindConfiguration(EmailOptions.Section)
            .Validate(o => !string.IsNullOrWhiteSpace(o.From), "Falta Email:From.")
            .ValidateOnStart();
        services.Configure<CheckoutOptions>(builder.Configuration.GetSection(CheckoutOptions.Section));
        services.Configure<RecoveryOptions>(builder.Configuration.GetSection(RecoveryOptions.Section));
        services.AddSingleton<RecoveryQueue>();
        services.AddSingleton<RecoveryEmailLimiter>();
        services.AddHostedService<RecoveryWorker>();

        services.AddHttpClient<PolarPaymentProvider>((sp, client) =>
        {
            var polar = sp.GetRequiredService<IOptions<PolarOptions>>().Value;
            client.BaseAddress = polar.ApiBaseUrl;
            client.DefaultRequestHeaders.Authorization = new("Bearer", polar.AccessToken);
            client.DefaultRequestHeaders.Add("Polar-Version", PolarOptions.ApiVersion);
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        services.AddTransient<IPaymentProvider>(sp => sp.GetRequiredService<IOptions<PolarOptions>>().Value.IsConfigured
            ? sp.GetRequiredService<PolarPaymentProvider>()
            : ActivatorUtilities.CreateInstance<NotConfiguredPaymentProvider>(sp));

        services.AddHttpClient<ResendEmailSender>(client =>
        {
            client.BaseAddress = ResendEmailSender.BaseAddress;
            // Polar espera la respuesta del webhook en menos de 10 s, y el email se envía dentro de él.
            client.Timeout = TimeSpan.FromSeconds(5);
        });
        services.AddTransient<IEmailSender>(sp => string.IsNullOrWhiteSpace(sp.GetRequiredService<IOptions<ResendOptions>>().Value.ApiKey)
            ? ActivatorUtilities.CreateInstance<LoggingEmailSender>(sp)
            : sp.GetRequiredService<ResendEmailSender>());
        services.AddScoped<PurchaseEmails>();

        services.AddSingleton(sp =>
        {
            var polar = sp.GetRequiredService<IOptions<PolarOptions>>().Value;
            return new StandardWebhook(polar.WebhookSecret, sp.GetRequiredService<TimeProvider>(), polar.WebhookTolerance);
        });
        services.AddScoped<PolarWebhookHandler>();

        services.AddRateLimiter(options =>
        {
            options.AddPolicy(CheckoutRateLimit, context => PerIp(context, o => o.PermitLimit));
            options.AddPolicy(StatusRateLimit, context => PerIp(context, o => o.StatusPermitLimit));
            options.AddPolicy(RecoveryRateLimit, context =>
            {
                var recovery = context.RequestServices.GetRequiredService<IOptions<RecoveryOptions>>().Value;
                return RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = recovery.IpPermitLimit, Window = recovery.Window });
            });
        });
    }

    private static RateLimitPartition<string> PerIp(HttpContext context, Func<CheckoutOptions, int> limit)
    {
        var options = context.RequestServices.GetRequiredService<IOptions<CheckoutOptions>>().Value;
        return RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = limit(options), Window = options.Window });
    }

    public static void MapCompras(this WebApplication app)
    {
        app.MapGroup("/api/checkout").AddEndpointFilter<PaymentProviderExceptionFilter>().MapCheckout();
        app.MapGroup("/api/compras").MapPurchases();
        app.MapGroup("/api/recuperar").MapRecovery();
        app.MapPolarWebhook();
    }
}

/// <summary>Una falla de Polar se devuelve como 502 con un mensaje que se puede mostrar.</summary>
public sealed class PaymentProviderExceptionFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (PaymentProviderException exception)
        {
            return Results.Problem(
                detail: exception.Message,
                statusCode: StatusCodes.Status502BadGateway,
                title: "No se pudo completar la operación con Polar");
        }
    }
}
