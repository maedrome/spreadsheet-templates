using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Api.Compras;

namespace Api.Tests;

/// <summary>La API de Polar: productos con un precio fijo y sesiones de checkout.</summary>
public sealed class FakePolar : HttpMessageHandler
{
    /// <summary>Un secreto con el formato de Polar desde septiembre de 2026: <c>whsec_</c> y la clave en base64.</summary>
    public static readonly string WebhookSecret = "whsec_" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private readonly ConcurrentDictionary<string, int> _prices = new();
    private int _sequence;

    public ConcurrentQueue<(HttpMethod Method, string Path, JsonObject? Body)> Requests { get; } = new();

    /// <summary>Si se fija, Polar responde con este código a todo.</summary>
    public HttpStatusCode? FailWith { get; set; }

    public List<(HttpMethod Method, string Path, JsonObject? Body)> RequestsTo(string pathPrefix) =>
        Requests.Where(r => r.Path.StartsWith(pathPrefix, StringComparison.Ordinal)).ToList();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Assert.Equal("Bearer polar_oat_test", request.Headers.Authorization?.ToString());
        Assert.Equal("sandbox-api.polar.sh", request.RequestUri!.Host);

        var path = request.RequestUri.AbsolutePath;
        var body = request.Content is null ? null : JsonNode.Parse(await request.Content.ReadAsStringAsync(cancellationToken))!.AsObject();
        Requests.Enqueue((request.Method, path, body));

        if (FailWith is { } status)
            return new HttpResponseMessage(status) { Content = new StringContent("""{"detail":"falla simulada"}""") };

        if (request.Method == HttpMethod.Post && path == "/v1/checkouts/")
        {
            var id = Guid.NewGuid().ToString();
            return Json(new { id, url = $"https://sandbox.polar.sh/checkout/polar_c_{id}" });
        }
        if (request.Method == HttpMethod.Post && path == "/v1/products/")
        {
            var id = $"prod-{Interlocked.Increment(ref _sequence)}";
            _prices[id] = body!["prices"]![0]!["price_amount"]!.GetValue<int>();
            return Product(id);
        }
        if (path.StartsWith("/v1/products/", StringComparison.Ordinal))
        {
            var id = path["/v1/products/".Length..];
            if (request.Method == HttpMethod.Patch && body!["prices"]![0]!["price_amount"] is { } amount)
                _prices[id] = amount.GetValue<int>();
            return Product(id);
        }
        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private HttpResponseMessage Product(string id) => Json(new
    {
        id,
        prices = new[]
        {
            new { id = $"{id}-price-{_prices[id]}", amount_type = "fixed", price_amount = _prices[id], price_currency = "usd", is_archived = false },
        },
    });

    private static HttpResponseMessage Json(object value) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };

    /// <summary>Un <c>order.paid</c> o <c>order.refunded</c> como los que envía Polar.</summary>
    public static string OrderEvent(
        string type, string orderId, string checkoutId, string tipo, int productId, string email = "Cliente@Example.com",
        int netAmount = 500, string status = "paid") =>
        JsonSerializer.Serialize(new
        {
            type,
            timestamp = "2026-10-25T12:00:00Z",
            data = new
            {
                id = orderId,
                created_at = "2026-10-25T12:00:00Z",
                status,
                paid = true,
                subtotal_amount = netAmount,
                discount_amount = 0,
                net_amount = netAmount,
                tax_amount = 60,
                total_amount = netAmount + 60,
                refunded_amount = status == "paid" ? 0 : netAmount + 60,
                currency = "usd",
                billing_reason = "purchase",
                checkout_id = checkoutId,
                product_id = "prod-x",
                metadata = new Dictionary<string, string> { ["tipo"] = tipo, ["producto_id"] = productId.ToString() },
                customer = new { id = Guid.NewGuid(), email },
            },
        });

    /// <summary>La petición firmada como la firma Polar (Standard Webhooks).</summary>
    public static HttpRequestMessage SignedWebhook(string body, DateTimeOffset sentAt, string? secret = null, Func<string, string>? tamper = null)
    {
        var id = $"msg_{Guid.NewGuid():N}";
        var timestamp = sentAt.ToUnixTimeSeconds().ToString();
        var key = Convert.FromBase64String((secret ?? WebhookSecret)["whsec_".Length..]);
        var signature = Convert.ToBase64String(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes($"{id}.{timestamp}.{body}")));

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/polar")
        {
            Content = new StringContent(tamper?.Invoke(body) ?? body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add(StandardWebhook.IdHeader, id);
        request.Headers.Add(StandardWebhook.TimestampHeader, timestamp);
        request.Headers.Add(StandardWebhook.SignatureHeader, $"v1,{signature}");
        return request;
    }
}

public sealed class FakeEmailSender : IEmailSender
{
    public ConcurrentQueue<EmailMessage> Sent { get; } = new();

    public bool Fail { get; set; }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (Fail)
            throw new HttpRequestException("Resend no responde (simulado).");
        Sent.Enqueue(message);
        return Task.CompletedTask;
    }

    /// <summary>El token del Enlace de compra que va en el email.</summary>
    public static string TokenIn(EmailMessage message)
    {
        const string prefix = "https://tienda.test/compra/";
        var start = message.Text.IndexOf(prefix, StringComparison.Ordinal) + prefix.Length;
        var end = message.Text.IndexOfAny(['\r', '\n'], start);
        return message.Text[start..(end < 0 ? message.Text.Length : end)];
    }
}
