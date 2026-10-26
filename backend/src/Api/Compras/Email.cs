using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.Catalogo;
using Microsoft.Extensions.Options;

namespace Api.Compras;

public record EmailMessage(string To, string Subject, string Html, string Text);

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

/// <summary>Envía con la API de Resend.</summary>
public sealed class ResendEmailSender(HttpClient http, IOptions<ResendOptions> resend, IOptions<EmailOptions> email) : IEmailSender
{
    public static readonly Uri BaseAddress = new("https://api.resend.com/");

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "emails")
        {
            Content = JsonContent.Create(new
            {
                from = email.Value.From,
                to = new[] { message.To },
                subject = message.Subject,
                html = message.Html,
                text = message.Text,
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", resend.Value.ApiKey);

        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"Resend respondió {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}",
                null, response.StatusCode);
    }
}

/// <summary>Solo en desarrollo, sin <c>Resend:ApiKey</c>: el email se escribe en el log, con su enlace.</summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        logger.LogInformation("Email (sin enviar) para {To}: {Subject}\n{Text}", message.To, message.Subject, message.Text);
        return Task.CompletedTask;
    }
}

/// <summary>El email con el Enlace de compra, en español.</summary>
public sealed class PurchaseEmails(IEmailSender sender, IOptions<SiteOptions> site)
{
    public Task SendPurchaseLinkAsync(Order order, string productName, string token, CancellationToken cancellationToken)
    {
        var link = PurchaseUrl(token);
        var subject = $"Tu compra: {productName}";
        var text =
            $"""
            ¡Gracias por tu compra!

            Ya puedes descargar {productName} desde este enlace:
            {link}

            Guarda este email: el enlace no vence y es tu acceso a las descargas. No lo compartas, porque cualquiera que lo tenga puede descargar tus plantillas.

            Polar, que procesa el pago, te envía el recibo por separado.
            """;
        var name = WebUtility.HtmlEncode(productName);
        var href = WebUtility.HtmlEncode(link);
        var html =
            $"""
            <!doctype html>
            <html lang="es">
            <body style="margin:0;padding:24px;background:#f8fafc;font-family:Arial,Helvetica,sans-serif;color:#0f172a">
              <div style="max-width:560px;margin:0 auto;background:#ffffff;border-radius:8px;padding:32px">
                <h1 style="margin:0 0 16px;font-size:22px">¡Gracias por tu compra!</h1>
                <p style="margin:0 0 24px;font-size:16px;line-height:1.5">Ya puedes descargar <strong>{name}</strong>.</p>
                <p style="margin:0 0 24px">
                  <a href="{href}" style="display:inline-block;background:#0f172a;color:#ffffff;text-decoration:none;padding:12px 20px;border-radius:6px;font-weight:bold">Ir a mis descargas</a>
                </p>
                <p style="margin:0 0 16px;font-size:14px;line-height:1.5;color:#475569">Si el botón no funciona, copia este enlace en tu navegador:<br><a href="{href}" style="color:#0f172a;word-break:break-all">{href}</a></p>
                <p style="margin:0 0 16px;font-size:14px;line-height:1.5;color:#475569">Guarda este email: el enlace no vence y es tu acceso a las descargas. No lo compartas, porque cualquiera que lo tenga puede descargar tus plantillas.</p>
                <p style="margin:0;font-size:14px;line-height:1.5;color:#475569">Polar, que procesa el pago, te envía el recibo por separado.</p>
              </div>
            </body>
            </html>
            """;

        return sender.SendAsync(new EmailMessage(order.Email, subject, html, text), cancellationToken);
    }

    public string PurchaseUrl(string token) => site.Value.AbsoluteUrl($"/compra/{token}");
}
