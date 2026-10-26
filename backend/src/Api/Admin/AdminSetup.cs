using System.Security.Claims;
using System.Threading.RateLimiting;
using Api.Common;
using Api.Compras;
using Api.Data;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Api.Admin;

public class AdminAuthOptions
{
    public const string Section = "AdminAuth";

    /// <summary>Intentos permitidos por IP y por ventana en el login y en la verificación de 2FA.</summary>
    public int PermitLimit { get; set; } = 5;
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Nombre con el que aparece la cuenta en la app de autenticación.</summary>
    public string TotpIssuer { get; set; } = "Plantillas de presupuesto";
}

/// <summary>Identity con un único administrador, 2FA TOTP obligatorio, antiforgery y rate limiting.</summary>
public static class AdminSetup
{
    /// <summary>Sesión completa: contraseña y segundo factor.</summary>
    public const string Policy = "Admin";

    /// <summary>Contraseña correcta, pero el 2FA todavía no está configurado: solo puede enrolarse.</summary>
    public const string EnrollmentPolicy = "AdminEnrollment";

    public const string LoginRateLimit = "admin-login";
    public const string TwoFactorRateLimit = "admin-2fa";

    /// <summary>Cookie legible desde JavaScript que Angular reenvía en el header <see cref="XsrfHeader"/>.</summary>
    public const string XsrfCookie = "XSRF-TOKEN";
    public const string XsrfHeader = "X-XSRF-TOKEN";

    /// <summary>Claim que Identity agrega al iniciar sesión: <c>pwd</c> solo con contraseña, <c>mfa</c> tras el segundo factor.</summary>
    public const string AmrClaim = "amr";
    public const string MfaAmr = "mfa";

    public static void AddAdmin(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        var securePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;

        services.Configure<AdminAuthOptions>(builder.Configuration.GetSection(AdminAuthOptions.Section));
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<PreviewImages>();
        services.AddSingleton<XlsxFiles>();

        services.AddIdentityCore<IdentityUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 12;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders()
            .AddErrorDescriber<SpanishIdentityErrorDescriber>();

        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "admin_session";
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;
            // Es una API: sin redirecciones a páginas de login.
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });
        // Aplica a todas las cookies de Identity, incluida la del paso intermedio de 2FA.
        services.PostConfigureAll<CookieAuthenticationOptions>(options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = securePolicy;
        });
        // La revalidación periódica del security stamp crea un principal nuevo: hay que conservar el amr=mfa.
        services.Configure<SecurityStampValidatorOptions>(options => options.OnRefreshingPrincipal = context =>
        {
            if (context.CurrentPrincipal?.FindFirst(AmrClaim) is { } amr && context.NewPrincipal?.Identity is ClaimsIdentity identity)
                identity.AddClaim(new Claim(AmrClaim, amr.Value));
            return Task.CompletedTask;
        });

        services.AddAuthorizationBuilder()
            .AddPolicy(Policy, policy => policy.RequireAuthenticatedUser().RequireClaim(AmrClaim, MfaAmr))
            .AddPolicy(EnrollmentPolicy, policy => policy.RequireAuthenticatedUser());

        services.AddAntiforgery(options =>
        {
            options.HeaderName = XsrfHeader;
            options.Cookie.Name = "admin_antiforgery";
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = securePolicy;
        });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) => new ValueTask(Results.Problem(
                detail: "Demasiados intentos. Espera un minuto y vuelve a probar.",
                statusCode: StatusCodes.Status429TooManyRequests).ExecuteAsync(context.HttpContext));
            options.AddPolicy(LoginRateLimit, PerIp);
            options.AddPolicy(TwoFactorRateLimit, PerIp);
        });
    }

    private static RateLimitPartition<string> PerIp(HttpContext context)
    {
        var options = context.RequestServices.GetRequiredService<IOptions<AdminAuthOptions>>().Value;
        return RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = options.PermitLimit, Window = options.Window });
    }

    public static void MapAdmin(this WebApplication app)
    {
        var admin = app.MapGroup("/api/admin")
            .AddEndpointFilter<AntiforgeryFilter>()
            .AddEndpointFilter<DomainExceptionFilter>()
            .AddEndpointFilter<PaymentProviderExceptionFilter>();

        admin.MapGroup("/auth").MapAdminAuth();

        var secured = admin.MapGroup("").RequireAuthorization(Policy);
        secured.MapGroup("/plantillas").MapAdminTemplates();
        secured.MapGroup("/paquetes").MapAdminBundles();
        secured.MapGroup("/compras").MapAdminOrders();
        secured.MapAdminPreviewFiles();
    }

    /// <summary>Emite el token que Angular reenvía en <see cref="XsrfHeader"/>. Depende del usuario, así que se renueva tras cada cambio de sesión.</summary>
    public static void IssueXsrfToken(HttpContext context)
    {
        var tokens = context.RequestServices.GetRequiredService<IAntiforgery>().GetAndStoreTokens(context);
        var development = context.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment();
        context.Response.Cookies.Append(XsrfCookie, tokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false,
            SameSite = SameSiteMode.Strict,
            Secure = !development || context.Request.IsHttps,
            Path = "/",
        });
    }
}

/// <summary>Valida el token antiforgery en toda petición que modifica datos, sea JSON o multipart.</summary>
internal sealed class AntiforgeryFilter(IAntiforgery antiforgery) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var method = context.HttpContext.Request.Method;
        var safe = HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method);
        if (!safe && !await antiforgery.IsRequestValidAsync(context.HttpContext))
            return Results.Problem(
                detail: "La sesión del formulario expiró. Recarga la página e inténtalo de nuevo.",
                statusCode: StatusCodes.Status400BadRequest);
        return await next(context);
    }
}

/// <summary>Una regla de dominio que impide la operación se devuelve como 400 con su mensaje.</summary>
internal sealed class DomainExceptionFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (DomainException exception)
        {
            return Results.Problem(
                detail: exception.Message,
                statusCode: StatusCodes.Status400BadRequest,
                title: "No se pudo completar la operación");
        }
    }
}
