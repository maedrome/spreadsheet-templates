using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Api.Admin;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Admin;

/// <summary>Una API con un administrador ya enrolado en 2FA y un rate limit que no estorba entre tests.</summary>
public class AdminApiFactory : ApiFactory
{
    public const string Email = "admin@example.com";
    public const string Password = "Contraseña-Segura-123";

    private readonly SemaphoreSlim _adminLock = new(1, 1);
    private string? _authenticatorKey;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("AdminAuth:PermitLimit", "1000");
    }

    /// <summary>Un cliente con sesión completa (contraseña + TOTP) y el token XSRF vigente.</summary>
    /// <param name="cookies">Para compartir las cookies de la sesión con otro cliente.</param>
    public async Task<HttpClient> CreateAdminClientAsync(CookieContainer? cookies = null)
    {
        var key = await EnsureEnrolledAdminAsync();
        var client = this.CreateXsrfClient(cookies);
        await client.GetAsync("/api/admin/auth/session");
        (await client.PostAsJsonAsync("/api/admin/auth/login", new LoginRequest(Email, Password))).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/admin/auth/2fa/verify", new TwoFactorRequest(Totp.Compute(key)))).EnsureSuccessStatusCode();
        await client.GetAsync("/api/admin/auth/session");
        return client;
    }

    private async Task<string> EnsureEnrolledAdminAsync()
    {
        await _adminLock.WaitAsync();
        try
        {
            if (_authenticatorKey is not null)
                return _authenticatorKey;

            CreateClient();
            using var scope = Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var user = new IdentityUser { UserName = Email, Email = Email, EmailConfirmed = true };
            Assert.True((await users.CreateAsync(user, Password)).Succeeded);
            await users.ResetAuthenticatorKeyAsync(user);
            await users.SetTwoFactorEnabledAsync(user, true);
            return _authenticatorKey = (await users.GetAuthenticatorKeyAsync(user))!;
        }
        finally
        {
            _adminLock.Release();
        }
    }
}

internal static class AdminClients
{
    /// <summary>Un cliente con cookies que, como Angular, copia la cookie XSRF-TOKEN al header X-XSRF-TOKEN.</summary>
    public static HttpClient CreateXsrfClient(this ApiFactory factory, CookieContainer? cookies = null)
    {
        cookies ??= new CookieContainer();
        return factory.CreateDefaultClient(factory.ClientOptions.BaseAddress, new XsrfHandler(cookies), new CookieContainerHandler(cookies));
    }

    private sealed class XsrfHandler(CookieContainer cookies) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method != HttpMethod.Get && cookies.GetCookies(request.RequestUri!)[AdminSetup.XsrfCookie] is { } token)
                request.Headers.Add(AdminSetup.XsrfHeader, token.Value);
            return base.SendAsync(request, cancellationToken);
        }
    }
}

/// <summary>TOTP de RFC 6238 (HMAC-SHA1, 30 s, 6 dígitos), como el de una app de autenticación.</summary>
internal static class Totp
{
    public static string Compute(string base32Key)
    {
        var step = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        var counter = new byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, step);
        var hash = HMACSHA1.HashData(Base32Decode(base32Key), counter);
        var offset = hash[^1] & 0x0f;
        var code = (BinaryPrimitives.ReadInt32BigEndian(hash.AsSpan(offset)) & 0x7fffffff) % 1_000_000;
        return code.ToString("D6");
    }

    private static byte[] Base32Decode(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bytes = new List<byte>();
        int buffer = 0, bits = 0;
        foreach (var c in input.ToUpperInvariant().Where(c => c != ' ' && c != '='))
        {
            buffer = (buffer << 5) | alphabet.IndexOf(c);
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                bytes.Add((byte)(buffer >> bits));
            }
        }
        return bytes.ToArray();
    }
}
