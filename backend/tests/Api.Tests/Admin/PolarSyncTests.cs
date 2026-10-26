using System.Net;
using System.Net.Http.Json;
using Api.Admin;
using Api.Compras;
using Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Admin;

/// <summary>El producto de Polar de cada Producto publicado, y el reenvío del Enlace de compra.</summary>
public class PolarSyncTests(AdminApiFactory factory) : IClassFixture<AdminApiFactory>
{
    [Fact]
    public async Task Publishing_a_template_creates_its_polar_product_and_a_price_change_updates_it()
    {
        var client = await factory.CreateAdminClientAsync();
        var template = TestData.PublishableTemplate("sync-plantilla", priceCents: 500);
        await SaveAsync(db => db.Templates.Add(template));

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/admin/plantillas/{template.Id}/publicar", null)).StatusCode);

        var productId = await PolarProductIdAsync(template.Id);
        var created = factory.Polar.RequestsTo("/v1/products/").Last();
        Assert.Equal(HttpMethod.Post, created.Method);
        Assert.Equal(template.Name, created.Body!["name"]!.GetValue<string>());
        Assert.Equal(500, created.Body["prices"]![0]!["price_amount"]!.GetValue<int>());
        Assert.Equal("plantilla", created.Body["metadata"]!["tipo"]!.GetValue<string>());

        var input = new TemplateInput("sync-plantilla", template.Name, template.Description, 750, null, template.GoogleSheetsCopyUrl);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/admin/plantillas/{template.Id}", input)).StatusCode);

        var patch = factory.Polar.RequestsTo($"/v1/products/{productId}").Last();
        Assert.Equal(HttpMethod.Patch, patch.Method);
        Assert.Equal(750, patch.Body!["prices"]![0]!["price_amount"]!.GetValue<int>());
        Assert.Equal(productId, await PolarProductIdAsync(template.Id));

        // Sin cambios en lo que se cobra, no se llama a Polar.
        var before = factory.Polar.Requests.Count;
        await client.PutAsJsonAsync($"/api/admin/plantillas/{template.Id}", input with { Position = 3 });
        Assert.Equal(before, factory.Polar.Requests.Count);

        // Un cambio de nombre conserva el precio vigente por su id.
        await client.PutAsJsonAsync($"/api/admin/plantillas/{template.Id}", input with { Name = "Nombre nuevo" });
        var rename = factory.Polar.RequestsTo($"/v1/products/{productId}").Last();
        Assert.Equal($"{productId}-price-750", rename.Body!["prices"]![0]!["id"]!.GetValue<string>());
    }

    [Fact]
    public async Task Publishing_a_bundle_creates_its_polar_product()
    {
        var client = await factory.CreateAdminClientAsync();
        var bundle = TestData.PublishableBundle("sync-paquete");
        await SaveAsync(db => db.Bundles.Add(bundle));

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/admin/paquetes/{bundle.Id}/publicar", null)).StatusCode);

        var productId = await QueryAsync(db => db.Bundles.Where(b => b.Id == bundle.Id).Select(b => b.PolarProductId).SingleAsync());
        Assert.NotNull(productId);
        Assert.Contains(factory.Polar.RequestsTo("/v1/products/"),
            r => r.Body?["metadata"]?["tipo"]?.GetValue<string>() == "paquete" && r.Body["prices"]![0]!["price_amount"]!.GetValue<int>() == 700);
    }

    [Fact]
    public async Task If_polar_fails_the_template_is_not_published()
    {
        var client = await factory.CreateAdminClientAsync();
        var template = TestData.PublishableTemplate("sync-falla");
        await SaveAsync(db => db.Templates.Add(template));
        factory.Polar.FailWith = HttpStatusCode.UnprocessableEntity;
        HttpResponseMessage response;
        try
        {
            response = await client.PostAsync($"/api/admin/plantillas/{template.Id}/publicar", null);
        }
        finally
        {
            factory.Polar.FailWith = null;
        }

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Contains("Polar", (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail);
        Assert.False(await QueryAsync(db => db.Templates.Where(t => t.Id == template.Id).Select(t => t.IsPublished).SingleAsync()));
    }

    [Fact]
    public async Task Resending_issues_a_new_purchase_link_and_emails_it()
    {
        var client = await factory.CreateAdminClientAsync();
        var template = TestData.PublishedTemplate("reenviar");
        var order = Order.ForTemplate("reenvio@example.com", template, 500, "polar-reenviar", TestData.Now);
        var original = order.IssuePurchaseLink(TestData.Now);
        await SaveAsync(db => db.Orders.Add(order));

        var response = await client.PostAsync($"/api/admin/compras/{order.Id}/reenviar", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var email = Assert.Single(factory.Emails.Sent, m => m.To == "reenvio@example.com");
        var token = FakeEmailSender.TokenIn(email);
        Assert.NotEqual(original, token);
        var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync($"/api/compras/{token}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync($"/api/compras/{original}")).StatusCode);
    }

    [Fact]
    public async Task A_refunded_order_cannot_be_resent()
    {
        var client = await factory.CreateAdminClientAsync();
        var order = Order.ForTemplate("reembolsada@example.com", TestData.PublishedTemplate("reenviar-reembolsada"), 500, "polar-reenviar-r", TestData.Now);
        order.Refund(TestData.Now);
        await SaveAsync(db => db.Orders.Add(order));

        var response = await client.PostAsync($"/api/admin/compras/{order.Id}/reenviar", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain(factory.Emails.Sent, m => m.To == "reembolsada@example.com");
    }

    [Fact]
    public async Task Resending_requires_an_admin_session()
    {
        var response = await factory.CreateXsrfClient().PostAsync("/api/admin/compras/1/reenviar", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private Task<string?> PolarProductIdAsync(int templateId) =>
        QueryAsync(db => db.Templates.Where(t => t.Id == templateId).Select(t => t.PolarProductId).SingleAsync());

    private async Task SaveAsync(Action<AppDbContext> change)
    {
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
