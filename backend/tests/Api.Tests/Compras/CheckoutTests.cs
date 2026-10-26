using System.Net;
using System.Net.Http.Json;
using Api.Catalogo;
using Api.Compras;
using Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Compras;

public class CheckoutTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Checkout_creates_the_polar_product_and_a_session_that_returns_to_the_confirmation_page()
    {
        var template = TestData.PublishedTemplate("checkout-ok", priceCents: 900);
        await SaveAsync(db => db.Templates.Add(template));
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/checkout", new CheckoutInput("plantilla", "checkout-ok"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("https://sandbox.polar.sh/checkout/", (await response.Content.ReadFromJsonAsync<CheckoutCreated>())!.Url);

        // Publicada antes de integrar Polar: el producto se crea en la primera compra y queda guardado.
        var product = factory.Polar.RequestsTo("/v1/products/").Single(r => r.Body?["metadata"]?["producto_id"]?.GetValue<int>() == template.Id);
        Assert.Equal(900, product.Body!["prices"]![0]!["price_amount"]!.GetValue<int>());
        var productId = await QueryAsync(db => db.Templates.Where(t => t.Id == template.Id).Select(t => t.PolarProductId).SingleAsync());
        Assert.NotNull(productId);

        var checkout = factory.Polar.RequestsTo("/v1/checkouts/").Last().Body!;
        Assert.Equal(productId, checkout["products"]![0]!.GetValue<string>());
        Assert.Equal("https://tienda.test/compra/confirmando?checkout={CHECKOUT_ID}", checkout["success_url"]!.GetValue<string>());
        Assert.Equal("https://tienda.test/plantillas/checkout-ok", checkout["return_url"]!.GetValue<string>());
        Assert.Equal("plantilla", checkout["metadata"]!["tipo"]!.GetValue<string>());
        Assert.Equal(template.Id.ToString(), checkout["metadata"]!["producto_id"]!.GetValue<string>());
    }

    [Fact]
    public async Task Checkout_rejects_products_that_are_not_published()
    {
        var draft = TestData.PublishableTemplate("checkout-borrador");
        var bundle = TestData.PublishableBundle("checkout-paquete-borrador");
        await SaveAsync(db =>
        {
            db.Templates.Add(draft);
            db.Bundles.Add(bundle);
        });
        var client = factory.CreateClient();
        var before = factory.Polar.Requests.Count;

        var template = await client.PostAsJsonAsync("/api/checkout", new CheckoutInput("plantilla", "checkout-borrador"));
        var unpublishedBundle = await client.PostAsJsonAsync("/api/checkout", new CheckoutInput("paquete", "checkout-paquete-borrador"));
        var missing = await client.PostAsJsonAsync("/api/checkout", new CheckoutInput("plantilla", "no-existe"));
        var badType = await client.PostAsJsonAsync("/api/checkout", new CheckoutInput("otro", "checkout-borrador"));

        Assert.Equal(HttpStatusCode.NotFound, template.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unpublishedBundle.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badType.StatusCode);
        Assert.Equal(before, factory.Polar.Requests.Count);
    }

    [Fact]
    public async Task Checkout_fails_with_502_when_polar_fails()
    {
        var template = TestData.PublishedTemplate("checkout-falla");
        await SaveAsync(db => db.Templates.Add(template));
        factory.Polar.FailWith = HttpStatusCode.InternalServerError;
        try
        {
            var response = await factory.CreateClient().PostAsJsonAsync("/api/checkout", new CheckoutInput("plantilla", "checkout-falla"));

            Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        }
        finally
        {
            factory.Polar.FailWith = null;
        }
    }

    [Fact]
    public async Task Polling_returns_pending_until_the_webhook_and_then_issues_the_token_only_once()
    {
        var bundle = TestData.PublishableBundle("polling");
        bundle.Publish(TestData.Now);
        await SaveAsync(db => db.Bundles.Add(bundle));
        var client = factory.CreateClient();
        var checkoutId = Guid.NewGuid().ToString();

        var pending = await client.GetFromJsonAsync<CheckoutStatus>($"/api/checkout/{checkoutId}/estado");
        Assert.Equal(new CheckoutStatus("pending", null), pending);

        var webhook = await client.SendAsync(FakePolar.SignedWebhook(
            FakePolar.OrderEvent("order.paid", "polar-order-polling", checkoutId, "paquete", bundle.Id, netAmount: 700), DateTimeOffset.UtcNow));
        Assert.Equal(HttpStatusCode.Accepted, webhook.StatusCode);

        var first = (await client.GetFromJsonAsync<CheckoutStatus>($"/api/checkout/{checkoutId}/estado"))!;
        var second = (await client.GetFromJsonAsync<CheckoutStatus>($"/api/checkout/{checkoutId}/estado"))!;
        Assert.Equal("paid", first.Status);
        Assert.NotNull(first.Token);
        Assert.Equal(new CheckoutStatus("paid", null), second);

        // El token de la confirmación y el del email son Enlaces distintos de la misma Compra.
        var purchase = (await client.GetFromJsonAsync<PurchaseDto>($"/api/compras/{first.Token}"))!;
        Assert.Equal("paquete", purchase.ProductType);
        Assert.Equal(bundle.Name, purchase.ProductName);
        Assert.Equal("cliente@example.com", purchase.Email);
        Assert.Equal(700, purchase.PricePaidCents);
        Assert.False(purchase.IsRefunded);
        Assert.Equal(bundle.Templates.Select(bt => bt.Template.Name).Order(), purchase.Templates.Select(t => t.Name));

        var emailToken = FakeEmailSender.TokenIn(factory.Emails.Sent.Single(m => m.Subject == $"Tu compra: {bundle.Name}"));
        Assert.NotEqual(first.Token, emailToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/compras/{emailToken}")).StatusCode);
    }

    [Fact]
    public async Task Concurrent_polls_issue_a_single_token()
    {
        var template = TestData.PublishedTemplate("polling-concurrente");
        await SaveAsync(db => db.Templates.Add(template));
        var client = factory.CreateClient();
        var checkoutId = Guid.NewGuid().ToString();
        await client.SendAsync(FakePolar.SignedWebhook(
            FakePolar.OrderEvent("order.paid", "polar-order-concurrente", checkoutId, "plantilla", template.Id), DateTimeOffset.UtcNow));

        var responses = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => client.GetFromJsonAsync<CheckoutStatus>($"/api/checkout/{checkoutId}/estado")));

        Assert.Single(responses, r => r!.Token is not null);
        Assert.All(responses, r => Assert.Equal("paid", r!.Status));
    }

    [Fact]
    public async Task Unknown_or_malformed_ids_are_not_found()
    {
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/checkout/no-es-un-uuid/estado")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/compras/token-que-no-existe")).StatusCode);
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
