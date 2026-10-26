using System.Net;
using System.Net.Http.Json;
using System.Web;
using Api.Admin;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Admin;

public class AuthTests(AdminApiFactory factory) : IClassFixture<AdminApiFactory>
{
    private const string Email = "duena@example.com";
    private const string Password = "Otra-Contraseña-456";

    [Theory]
    [InlineData("/api/admin/plantillas")]
    [InlineData("/api/admin/paquetes")]
    [InlineData("/api/admin/compras")]
    [InlineData("/api/admin/auth/2fa/setup")]
    public async Task Admin_endpoints_return_401_without_session(string url)
    {
        var response = await factory.CreateClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task First_login_forces_enrollment_and_later_logins_require_the_totp_code()
    {
        await CreateUserWithoutTwoFactorAsync();
        var client = factory.CreateXsrfClient();
        Assert.Equal(SessionStatus.Anonymous, await StatusAsync(client));

        var wrong = await client.PostAsJsonAsync("/api/admin/auth/login", new LoginRequest(Email, "incorrecta"));
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);

        // Primer login: solo contraseña, el panel sigue cerrado hasta enrolarse.
        var login = await client.PostAsJsonAsync("/api/admin/auth/login", new LoginRequest(Email, Password));
        Assert.Equal(SessionStatus.RequiresEnrollment, (await login.Content.ReadFromJsonAsync<SessionResponse>())!.Status);
        Assert.Equal(SessionStatus.RequiresEnrollment, await StatusAsync(client));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/plantillas")).StatusCode);

        var setup = (await client.GetFromJsonAsync<EnrollmentResponse>("/api/admin/auth/2fa/setup"))!;
        Assert.StartsWith("otpauth://totp/", setup.OtpauthUri);
        Assert.StartsWith("data:image/png;base64,", setup.QrCodeDataUrl);
        var key = HttpUtility.ParseQueryString(new Uri(setup.OtpauthUri).Query)["secret"]!;

        var badCode = await client.PostAsJsonAsync("/api/admin/auth/2fa/enable", new TwoFactorRequest("000000"));
        Assert.Equal(HttpStatusCode.BadRequest, badCode.StatusCode);

        var enable = await client.PostAsJsonAsync("/api/admin/auth/2fa/enable", new TwoFactorRequest(Totp.Compute(key)));
        enable.EnsureSuccessStatusCode();
        var recoveryCodes = (await enable.Content.ReadFromJsonAsync<EnableTwoFactorResponse>())!.RecoveryCodes;
        Assert.Equal(10, recoveryCodes.Count);
        Assert.Equal(SessionStatus.Authenticated, await StatusAsync(client));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/plantillas")).StatusCode);

        await LogoutAsync(client);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/plantillas")).StatusCode);

        // Logins siguientes: la contraseña sola deja la sesión a medias.
        login = await client.PostAsJsonAsync("/api/admin/auth/login", new LoginRequest(Email, Password));
        Assert.Equal(SessionStatus.RequiresTwoFactor, (await login.Content.ReadFromJsonAsync<SessionResponse>())!.Status);
        Assert.Equal(SessionStatus.RequiresTwoFactor, await StatusAsync(client));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/plantillas")).StatusCode);

        var wrongCode = await client.PostAsJsonAsync("/api/admin/auth/2fa/verify", new TwoFactorRequest("000000"));
        Assert.Equal(HttpStatusCode.Unauthorized, wrongCode.StatusCode);

        var verify = await client.PostAsJsonAsync("/api/admin/auth/2fa/verify", new TwoFactorRequest(Totp.Compute(key)));
        verify.EnsureSuccessStatusCode();
        Assert.Equal(SessionStatus.Authenticated, await StatusAsync(client));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/plantillas")).StatusCode);

        // Un código de recuperación sirve una sola vez.
        await LogoutAsync(client);
        await client.PostAsJsonAsync("/api/admin/auth/login", new LoginRequest(Email, Password));
        var recovery = await client.PostAsJsonAsync("/api/admin/auth/2fa/verify", new TwoFactorRequest(recoveryCodes[0], IsRecoveryCode: true));
        recovery.EnsureSuccessStatusCode();
        Assert.Equal(SessionStatus.Authenticated, await StatusAsync(client));
        await LogoutAsync(client);
        await client.PostAsJsonAsync("/api/admin/auth/login", new LoginRequest(Email, Password));
        var reused = await client.PostAsJsonAsync("/api/admin/auth/2fa/verify", new TwoFactorRequest(recoveryCodes[0], IsRecoveryCode: true));
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);
    }

    [Fact]
    public async Task Mutations_without_the_xsrf_header_are_rejected()
    {
        var cookies = new CookieContainer();
        await factory.CreateAdminClientAsync(cookies);
        // Mismas cookies de sesión, pero sin el handler que copia la cookie XSRF-TOKEN al header.
        var client = factory.CreateDefaultClient(new CookieContainerHandler(cookies));

        var response = await client.PostAsJsonAsync("/api/admin/plantillas", new TemplateInput("sin-xsrf", "Sin XSRF", "", 100, null, null));
        var forged = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/admin/plantillas")
        {
            Content = JsonContent.Create(new TemplateInput("sin-xsrf", "Sin XSRF", "", 100, null, null)),
            Headers = { { AdminSetup.XsrfHeader, "token-falso" } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task CreateUserWithoutTwoFactorAsync()
    {
        factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        Assert.True((await users.CreateAsync(new IdentityUser { UserName = Email, Email = Email }, Password)).Succeeded);
    }

    private static async Task<string> StatusAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<SessionResponse>("/api/admin/auth/session"))!.Status;

    private static async Task LogoutAsync(HttpClient client)
    {
        (await client.PostAsync("/api/admin/auth/logout", null)).EnsureSuccessStatusCode();
        await StatusAsync(client);
    }
}

public class RateLimitTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Login_is_rate_limited_per_ip()
    {
        var client = factory.CreateXsrfClient();
        await client.GetAsync("/api/admin/auth/session");

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 6; i++)
            statuses.Add((await client.PostAsJsonAsync("/api/admin/auth/login", new LoginRequest("nadie@example.com", "x"))).StatusCode);

        Assert.All(statuses.Take(5), status => Assert.Equal(HttpStatusCode.Unauthorized, status));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[5]);
    }
}

public class CreateAdminCommandTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Creates_the_single_admin_with_the_password_from_stdin()
    {
        factory.CreateClient();
        var output = new StringWriter();
        string[] args = [CreateAdminCommand.Name, "--email", "duena@example.com"];

        var weak = await CreateAdminCommand.RunAsync(factory.Services, args, output, _ => "corta", confirm: false);
        Assert.Equal(1, weak);
        Assert.Contains("al menos 12 caracteres", output.ToString());

        var created = await CreateAdminCommand.RunAsync(factory.Services, args, output, _ => "Contraseña-Segura-123", confirm: false);
        Assert.Equal(0, created);

        var second = await CreateAdminCommand.RunAsync(
            factory.Services, [CreateAdminCommand.Name, "--email=otra@example.com"], output, _ => "Contraseña-Segura-123", confirm: false);
        Assert.Equal(1, second);
        Assert.Contains("Ya existe un administrador", output.ToString());

        var client = factory.CreateXsrfClient();
        await client.GetAsync("/api/admin/auth/session");
        var login = await client.PostAsJsonAsync("/api/admin/auth/login", new LoginRequest("duena@example.com", "Contraseña-Segura-123"));
        Assert.Equal(SessionStatus.RequiresEnrollment, (await login.Content.ReadFromJsonAsync<SessionResponse>())!.Status);
    }

    [Fact]
    public async Task Without_email_prints_usage()
    {
        var output = new StringWriter();

        var exitCode = await CreateAdminCommand.RunAsync(factory.Services, [CreateAdminCommand.Name], output, _ => "x", confirm: false);

        Assert.Equal(1, exitCode);
        Assert.Contains("--email", output.ToString());
    }
}
