using System.Net;
using System.Net.Http.Json;
using Api.Catalogo;
using Api.Compras;
using Api.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Compras;

public class DownloadAndRecoveryTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string LinkPrefix = "https://tienda.test/compra/";

    [Fact]
    public async Task Download_returns_the_current_file_with_private_headers()
    {
        var (_, token, template) = await PaidOrderAsync("dl-ok", "dl-ok@example.com", "contenido-v1");
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/compras/{token}/plantillas/{template.Id}/xlsx");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("contenido-v1", await response.Content.ReadAsStringAsync());
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal("dl-ok.xlsx", response.Content.Headers.ContentDisposition.FileName?.Trim('"'));
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        Assert.Equal("noindex", response.Headers.GetValues("X-Robots-Tag").Single());

        // El archivo vigente: si la Plantilla se actualiza, la descarga entrega el nuevo.
        File.WriteAllText(XlsxFile("dl-ok"), "contenido-v2");
        Assert.Equal("contenido-v2", await client.GetStringAsync($"/api/compras/{token}/plantillas/{template.Id}/xlsx"));
    }

    [Fact]
    public async Task Download_is_not_found_for_unknown_tokens_and_templates_outside_the_snapshot()
    {
        var (_, token, template) = await PaidOrderAsync("dl-404", "dl-404@example.com");
        var other = TestData.PublishedTemplate("dl-404-otra");
        await SaveAsync(db => db.Templates.Add(other));
        File.WriteAllText(XlsxFile("dl-404-otra"), "otra");
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/compras/token-falso/plantillas/{template.Id}/xlsx")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/compras/{token}/plantillas/{other.Id}/xlsx")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/compras/{token}/plantillas/999999/xlsx")).StatusCode);
    }

    [Fact]
    public async Task Download_is_gone_and_the_summary_hides_downloads_when_the_order_is_refunded()
    {
        var (order, token, template) = await PaidOrderAsync("dl-410", "dl-410@example.com");
        var client = factory.CreateClient();
        var paid = await client.GetFromJsonAsync<PurchaseDto>($"/api/compras/{token}");
        Assert.Equal($"/api/compras/{token}/plantillas/{template.Id}/xlsx", paid!.Templates.Single().DownloadUrl);
        Assert.Equal(template.GoogleSheetsCopyUrl, paid.Templates.Single().GoogleSheetsCopyUrl);

        await SaveAsync(db => db.Orders.Single(o => o.Id == order.Id).Refund(TestData.Now));

        Assert.Equal(HttpStatusCode.Gone, (await client.GetAsync($"/api/compras/{token}/plantillas/{template.Id}/xlsx")).StatusCode);
        var refunded = await client.GetFromJsonAsync<PurchaseDto>($"/api/compras/{token}");
        Assert.True(refunded!.IsRefunded);
        Assert.All(refunded.Templates, t =>
        {
            Assert.Null(t.DownloadUrl);
            Assert.Null(t.GoogleSheetsCopyUrl);
        });
    }

    [Fact]
    public async Task Recovery_sends_one_email_with_a_new_link_per_order_and_the_old_link_keeps_working()
    {
        var (_, oldToken, _) = await PaidOrderAsync("rec-a", "rec@example.com");
        await PaidOrderAsync("rec-b", "REC@example.com");
        var (refundedOrder, _, _) = await PaidOrderAsync("rec-c", "rec@example.com");
        await SaveAsync(db => db.Orders.Single(o => o.Id == refundedOrder.Id).Refund(TestData.Now));
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/recuperar", new RecoveryInput(" Rec@Example.com "));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var message = await WaitForEmailAsync("rec@example.com");
        var links = LinksIn(message).ToList();
        Assert.Equal(2, links.Count);
        Assert.DoesNotContain("rec-c", message.Text);
        foreach (var link in links)
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/compras/{link}")).StatusCode);
        Assert.DoesNotContain(oldToken, links);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/compras/{oldToken}")).StatusCode);
        Assert.Single(factory.Emails.Sent, m => m.To == "rec@example.com");
    }

    [Fact]
    public async Task Recovery_answers_the_same_with_or_without_orders_and_sends_nothing_without_them()
    {
        await PaidOrderAsync("rec-same", "rec-same@example.com");
        var client = factory.CreateClient();

        var known = await client.PostAsJsonAsync("/api/recuperar", new RecoveryInput("rec-same@example.com"));
        var unknown = await client.PostAsJsonAsync("/api/recuperar", new RecoveryInput("nadie-nunca@example.com"));

        Assert.Equal(known.StatusCode, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, unknown.StatusCode);
        Assert.Equal(await known.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());
        await WaitForEmailAsync("rec-same@example.com");
        await Task.Delay(300);
        Assert.DoesNotContain(factory.Emails.Sent, m => m.To == "nadie-nunca@example.com");
    }

    [Fact]
    public async Task Recovery_rejects_invalid_emails()
    {
        var client = factory.CreateClient();

        foreach (var email in new[] { "", "no-es-un-email", "a@b", "a@@b.com" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/recuperar", new RecoveryInput(email))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/recuperar", new RecoveryInput(null))).StatusCode);
    }

    [Fact]
    public async Task Recovery_limits_each_email_without_changing_the_response()
    {
        await PaidOrderAsync("rec-limit", "rec-limit@example.com");
        var client = factory.CreateClient();

        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/recuperar", new RecoveryInput("rec-limit@example.com"))).StatusCode);

        await WaitForEmailAsync("rec-limit@example.com");
        await Task.Delay(500);
        Assert.Equal(3, factory.Emails.Sent.Count(m => m.To == "rec-limit@example.com"));
    }

    [Fact]
    public async Task Recovery_limits_each_ip()
    {
        using var limited = factory.WithWebHostBuilder(builder => builder.UseSetting("Recovery:IpPermitLimit", "2"));
        var client = limited.CreateClient();

        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/recuperar", new RecoveryInput("ip-1@example.com"))).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/recuperar", new RecoveryInput("ip-2@example.com"))).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/recuperar", new RecoveryInput("ip-3@example.com"))).StatusCode);
    }

    private string XlsxFile(string slug) => Path.Combine(factory.StorageRoot, "templates", $"{slug}.xlsx");

    private async Task<(Order Order, string Token, Template Template)> PaidOrderAsync(string slug, string email, string content = "xlsx")
    {
        var template = TestData.PublishedTemplate(slug);
        var order = Order.ForTemplate(email, template, 500, $"polar-{slug}", TestData.Now);
        var token = order.IssuePurchaseLink(TestData.Now);
        await SaveAsync(db => db.Orders.Add(order));
        File.WriteAllText(XlsxFile(slug), content);
        return (order, token, template);
    }

    private static IEnumerable<string> LinksIn(EmailMessage message) =>
        message.Text.Split('\n').Select(l => l.Trim()).Where(l => l.StartsWith(LinkPrefix)).Select(l => l[LinkPrefix.Length..]);

    private async Task<EmailMessage> WaitForEmailAsync(string to)
    {
        for (var i = 0; i < 100; i++)
        {
            if (factory.Emails.Sent.FirstOrDefault(m => m.To == to) is { } message)
                return message;
            await Task.Delay(50);
        }
        throw new TimeoutException($"No llegó ningún email a {to}.");
    }

    private async Task SaveAsync(Action<AppDbContext> change)
    {
        factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        change(db);
        await db.SaveChangesAsync();
    }
}
