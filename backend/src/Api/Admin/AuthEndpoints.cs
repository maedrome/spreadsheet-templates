using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using QRCoder;

namespace Api.Admin;

public static class SessionStatus
{
    public const string Anonymous = "anonymous";
    public const string RequiresTwoFactor = "requiresTwoFactor";
    public const string RequiresEnrollment = "requiresEnrollment";
    public const string Authenticated = "authenticated";
}

public record SessionResponse(string Status, string? Email);
public record LoginRequest(string Email, string Password);
public record TwoFactorRequest(string Code, bool IsRecoveryCode = false);
public record EnrollmentResponse(string SharedKey, string OtpauthUri, string QrCodeDataUrl);
public record EnableTwoFactorResponse(List<string> RecoveryCodes);

public static class AuthEndpoints
{
    private const int RecoveryCodeCount = 10;

    public static void MapAdminAuth(this RouteGroupBuilder group)
    {
        group.MapGet("/session", GetSession);
        group.MapPost("/login", Login).RequireRateLimiting(AdminSetup.LoginRateLimit);
        group.MapPost("/2fa/verify", VerifyTwoFactor).RequireRateLimiting(AdminSetup.TwoFactorRateLimit);
        group.MapGet("/2fa/setup", GetEnrollment).RequireAuthorization(AdminSetup.EnrollmentPolicy);
        group.MapPost("/2fa/enable", EnableTwoFactor)
            .RequireAuthorization(AdminSetup.EnrollmentPolicy)
            .RequireRateLimiting(AdminSetup.TwoFactorRateLimit);
        group.MapPost("/logout", Logout);
    }

    /// <summary>Estado de la sesión para el guard de Angular. También renueva la cookie XSRF-TOKEN.</summary>
    private static async Task<SessionResponse> GetSession(HttpContext context, SignInManager<IdentityUser> signIn)
    {
        AdminSetup.IssueXsrfToken(context);
        context.Response.Headers.CacheControl = "no-store";

        var user = context.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            var status = user.HasClaim(AdminSetup.AmrClaim, AdminSetup.MfaAmr) ? SessionStatus.Authenticated : SessionStatus.RequiresEnrollment;
            return new SessionResponse(status, user.Identity.Name);
        }
        if (await signIn.GetTwoFactorAuthenticationUserAsync() is not null)
            return new SessionResponse(SessionStatus.RequiresTwoFactor, null);
        return new SessionResponse(SessionStatus.Anonymous, null);
    }

    private static async Task<IResult> Login(LoginRequest request, SignInManager<IdentityUser> signIn)
    {
        var result = await signIn.PasswordSignInAsync(request.Email.Trim(), request.Password, isPersistent: false, lockoutOnFailure: true);

        if (result.RequiresTwoFactor)
            return Results.Ok(new SessionResponse(SessionStatus.RequiresTwoFactor, null));
        // Sin 2FA configurado la sesión queda con amr=pwd y solo sirve para enrolarse.
        if (result.Succeeded)
            return Results.Ok(new SessionResponse(SessionStatus.RequiresEnrollment, request.Email.Trim()));
        if (result.IsLockedOut)
            return LockedOut();
        return Results.Problem(detail: "Email o contraseña incorrectos.", statusCode: StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> VerifyTwoFactor(TwoFactorRequest request, SignInManager<IdentityUser> signIn)
    {
        if (await signIn.GetTwoFactorAuthenticationUserAsync() is null)
            return Results.Problem(
                detail: "La verificación expiró. Vuelve a iniciar sesión.",
                statusCode: StatusCodes.Status401Unauthorized);

        var result = request.IsRecoveryCode
            ? await signIn.TwoFactorRecoveryCodeSignInAsync(request.Code.Trim())
            : await signIn.TwoFactorAuthenticatorSignInAsync(NormalizeTotp(request.Code), isPersistent: false, rememberClient: false);

        if (result.Succeeded)
            return Results.Ok(new SessionResponse(SessionStatus.Authenticated, null));
        if (result.IsLockedOut)
            return LockedOut();
        return Results.Problem(detail: "El código no es correcto.", statusCode: StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> GetEnrollment(
        ClaimsPrincipal principal, UserManager<IdentityUser> users, IOptions<AdminAuthOptions> options)
    {
        var user = await users.GetUserAsync(principal);
        if (user is null)
            return Results.Unauthorized();
        if (user.TwoFactorEnabled)
            return AlreadyEnrolled();

        var key = await users.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            await users.ResetAuthenticatorKeyAsync(user);
            key = await users.GetAuthenticatorKeyAsync(user);
        }

        var issuer = Uri.EscapeDataString(options.Value.TotpIssuer);
        var uri = $"otpauth://totp/{issuer}:{Uri.EscapeDataString(user.Email!)}?secret={key}&issuer={issuer}&digits=6";
        var png = PngByteQRCodeHelper.GetQRCode(uri, QRCodeGenerator.ECCLevel.Q, 6);

        return Results.Ok(new EnrollmentResponse(
            FormatKey(key!),
            uri,
            $"data:image/png;base64,{Convert.ToBase64String(png)}"));
    }

    private static async Task<IResult> EnableTwoFactor(
        TwoFactorRequest request, ClaimsPrincipal principal, UserManager<IdentityUser> users, SignInManager<IdentityUser> signIn)
    {
        var user = await users.GetUserAsync(principal);
        if (user is null)
            return Results.Unauthorized();
        if (user.TwoFactorEnabled)
            return AlreadyEnrolled();

        var valid = await users.VerifyTwoFactorTokenAsync(user, users.Options.Tokens.AuthenticatorTokenProvider, NormalizeTotp(request.Code));
        if (!valid)
            return Results.Problem(
                detail: "El código no es correcto. Revisa que la hora del teléfono esté sincronizada.",
                statusCode: StatusCodes.Status400BadRequest);

        await users.SetTwoFactorEnabledAsync(user, true);
        var codes = await users.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount);
        await signIn.SignInWithClaimsAsync(user, isPersistent: false, [new Claim(AdminSetup.AmrClaim, AdminSetup.MfaAmr)]);

        return Results.Ok(new EnableTwoFactorResponse(codes!.ToList()));
    }

    private static async Task<IResult> Logout(SignInManager<IdentityUser> signIn)
    {
        await signIn.SignOutAsync();
        return Results.NoContent();
    }

    private static IResult LockedOut() => Results.Problem(
        detail: "La cuenta quedó bloqueada por demasiados intentos fallidos. Prueba de nuevo en 15 minutos.",
        statusCode: StatusCodes.Status429TooManyRequests);

    private static IResult AlreadyEnrolled() => Results.Problem(
        detail: "La verificación en dos pasos ya está configurada.",
        statusCode: StatusCodes.Status400BadRequest);

    private static string NormalizeTotp(string code) => code.Replace(" ", "").Replace("-", "");

    /// <summary>Agrupa la clave en bloques de 4 para escribirla a mano en la app de autenticación.</summary>
    private static string FormatKey(string key) =>
        string.Join(' ', key.ToLowerInvariant().Chunk(4).Select(chunk => new string(chunk)));
}
