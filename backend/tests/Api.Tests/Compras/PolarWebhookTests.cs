using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Api.Compras;
using Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Compras;

public class PolarWebhookTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task A_valid_signature_is_accepted()
    {
        var response = await factory.CreateClient().SendAsync(FakePolar.SignedWebhook("""{"type":"checkout.created","data":{}}""", DateTimeOffset.UtcNow));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task A_tampered_body_is_rejected_and_creates_nothing()
    {
        var template = TestData.PublishedTemplate("webhook-alterado");
        await SaveAsync(db => db.Templates.Add(template));
        var body = FakePolar.OrderEvent("order.paid", "polar-alterado", Guid.NewGuid().ToString(), "plantilla", template.Id);

        var response = await factory.CreateClient().SendAsync(
            FakePolar.SignedWebhook(body, DateTimeOffset.UtcNow, tamper: b => b.Replace("cliente@example.com", "otro@example.com", StringComparison.OrdinalIgnoreCase)));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(await QueryAsync(db => db.Orders.AnyAsync(o => o.PolarOrderId == "polar-alterado")));
    }

    [Fact]
    public async Task A_signature_with_another_secret_is_rejected()
    {
        var otherSecret = "whsec_" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        var response = await factory.CreateClient().SendAsync(
            FakePolar.SignedWebhook("""{"type":"checkout.created","data":{}}""", DateTimeOffset.UtcNow, otherSecret));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task An_old_timestamp_is_rejected_even_with_a_valid_signature()
    {
        var response = await factory.CreateClient().SendAsync(
            FakePolar.SignedWebhook("""{"type":"checkout.created","data":{}}""", DateTimeOffset.UtcNow.AddMinutes(-10)));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Missing_signature_headers_are_rejected()
    {
        var response = await factory.CreateClient().PostAsync(
            "/api/webhooks/polar", new StringContent("""{"type":"order.paid","data":{}}""", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void Secrets_from_before_september_2026_use_the_utf8_bytes_of_the_whole_secret_as_key()
    {
        const string legacySecret = "whsec_secreto-viejo-de-polar";
        var webhook = new StandardWebhook(legacySecret, TimeProvider.System, TimeSpan.FromMinutes(5));
        var body = Encoding.UTF8.GetBytes("""{"type":"order.paid"}""");
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signature = Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(legacySecret), (byte[])[.. Encoding.UTF8.GetBytes($"msg_1.{timestamp}."), .. body]));

        Assert.True(webhook.IsValid("msg_1", timestamp, $"v1,otra-firma v1,{signature}", body));
        Assert.False(webhook.IsValid("msg_2", timestamp, $"v1,{signature}", body));
    }

    [Fact]
    public async Task The_same_order_paid_twice_creates_a_single_order_and_a_single_email()
    {
        var template = TestData.PublishedTemplate("webhook-repetido", priceCents: 800);
        await SaveAsync(db => db.Templates.Add(template));
        var client = factory.CreateClient();
        var body = FakePolar.OrderEvent("order.paid", "polar-repetido", Guid.NewGuid().ToString(), "plantilla", template.Id, netAmount: 640);

        var first = await client.SendAsync(FakePolar.SignedWebhook(body, DateTimeOffset.UtcNow));
        var second = await client.SendAsync(FakePolar.SignedWebhook(body, DateTimeOffset.UtcNow));

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
        var orders = await QueryAsync(db => db.Orders.Include(o => o.Templates).Include(o => o.PurchaseLinks)
            .Where(o => o.PolarOrderId == "polar-repetido").ToListAsync());
        var order = Assert.Single(orders);
        Assert.Equal("cliente@example.com", order.Email);
        Assert.Equal(640, order.PricePaidCents);
        Assert.Equal(template.Id, order.TemplateId);
        Assert.Equal(template.Id, Assert.Single(order.Templates).TemplateId);
        Assert.Single(order.PurchaseLinks);

        var email = Assert.Single(factory.Emails.Sent, m => m.Subject == $"Tu compra: {template.Name}");
        Assert.Equal("cliente@example.com", email.To);
        Assert.Contains("https://tienda.test/compra/", email.Html);
        Assert.Equal(PurchaseLink.HashToken(FakeEmailSender.TokenIn(email)), order.PurchaseLinks[0].TokenHash);
    }

    [Fact]
    public async Task Concurrent_deliveries_of_the_same_order_create_a_single_order()
    {
        var template = TestData.PublishedTemplate("webhook-concurrente");
        await SaveAsync(db => db.Templates.Add(template));
        var client = factory.CreateClient();
        var body = FakePolar.OrderEvent("order.paid", "polar-concurrente", Guid.NewGuid().ToString(), "plantilla", template.Id);

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => client.SendAsync(FakePolar.SignedWebhook(body, DateTimeOffset.UtcNow))));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Accepted, r.StatusCode));
        Assert.Equal(1, await QueryAsync(db => db.Orders.CountAsync(o => o.PolarOrderId == "polar-concurrente")));
    }

    [Fact]
    public async Task An_email_failure_does_not_lose_the_order()
    {
        var template = TestData.PublishedTemplate("webhook-sin-email");
        await SaveAsync(db => db.Templates.Add(template));
        var checkoutId = Guid.NewGuid().ToString();
        var client = factory.CreateClient();
        factory.Emails.Fail = true;
        try
        {
            var response = await client.SendAsync(FakePolar.SignedWebhook(
                FakePolar.OrderEvent("order.paid", "polar-sin-email", checkoutId, "plantilla", template.Id), DateTimeOffset.UtcNow));

            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        }
        finally
        {
            factory.Emails.Fail = false;
        }

        Assert.True(await QueryAsync(db => db.Orders.AnyAsync(o => o.PolarOrderId == "polar-sin-email")));
        // El cliente sigue llegando a su Compra desde la confirmación.
        var status = (await client.GetFromJsonAsync<CheckoutStatus>($"/api/checkout/{checkoutId}/estado"))!;
        Assert.NotNull(status.Token);
    }

    [Fact]
    public async Task Order_refunded_marks_the_order_as_refunded()
    {
        var template = TestData.PublishedTemplate("webhook-reembolso");
        await SaveAsync(db => db.Templates.Add(template));
        var checkoutId = Guid.NewGuid().ToString();
        var client = factory.CreateClient();
        await client.SendAsync(FakePolar.SignedWebhook(
            FakePolar.OrderEvent("order.paid", "polar-reembolso", checkoutId, "plantilla", template.Id), DateTimeOffset.UtcNow));
        var token = (await client.GetFromJsonAsync<CheckoutStatus>($"/api/checkout/{checkoutId}/estado"))!.Token;

        var partial = await client.SendAsync(FakePolar.SignedWebhook(
            FakePolar.OrderEvent("order.refunded", "polar-reembolso", checkoutId, "plantilla", template.Id, status: "partially_refunded"), DateTimeOffset.UtcNow));
        Assert.Equal(HttpStatusCode.Accepted, partial.StatusCode);
        Assert.False((await client.GetFromJsonAsync<PurchaseDto>($"/api/compras/{token}"))!.IsRefunded);

        var refund = await client.SendAsync(FakePolar.SignedWebhook(
            FakePolar.OrderEvent("order.refunded", "polar-reembolso", checkoutId, "plantilla", template.Id, status: "refunded"), DateTimeOffset.UtcNow));

        Assert.Equal(HttpStatusCode.Accepted, refund.StatusCode);
        var order = await QueryAsync(db => db.Orders.SingleAsync(o => o.PolarOrderId == "polar-reembolso"));
        Assert.Equal(OrderStatus.Refunded, order.Status);
        Assert.NotNull(order.RefundedAt);
        Assert.True((await client.GetFromJsonAsync<PurchaseDto>($"/api/compras/{token}"))!.IsRefunded);
    }

    [Fact]
    public async Task An_order_for_an_unknown_product_is_acknowledged_without_creating_anything()
    {
        var response = await factory.CreateClient().SendAsync(FakePolar.SignedWebhook(
            FakePolar.OrderEvent("order.paid", "polar-desconocido", Guid.NewGuid().ToString(), "plantilla", 999_999), DateTimeOffset.UtcNow));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.False(await QueryAsync(db => db.Orders.AnyAsync(o => o.PolarOrderId == "polar-desconocido")));
    }

    private async Task SaveAsync(Action<AppDbContext> change)
    {
        factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        change(db);
        await db.SaveChangesAsync();
    }

    private async Task<T> QueryAsync<T>(Func<AppDbContext, Task<T>> query)
    {
        using var scope = factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}
