using System.Net;
using Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Catalogo;

public class SeoEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private HttpClient Client() =>
        factory.WithWebHostBuilder(b => b.UseSetting("Site:BaseUrl", "https://tienda.test/")).CreateClient();

    [Fact]
    public async Task Sitemap_lists_home_and_published_products_only()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var bundle = TestData.PublishableBundle("seo-bundle");
            bundle.Publish(TestData.Now);
            db.AddRange(bundle, TestData.PublishableTemplate("seo-draft"));
            await db.SaveChangesAsync();
        }

        var response = await Client().GetAsync("/sitemap.xml");
        var xml = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/xml", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("<loc>https://tienda.test/</loc>", xml);
        Assert.Contains("<loc>https://tienda.test/paquetes/seo-bundle</loc>", xml);
        Assert.Contains("<loc>https://tienda.test/plantillas/seo-bundle-a</loc>", xml);
        Assert.DoesNotContain("seo-draft", xml);
    }

    [Fact]
    public async Task Robots_disallows_private_areas_and_points_to_sitemap()
    {
        var response = await Client().GetAsync("/robots.txt");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Disallow: /admin", body);
        Assert.Contains("Disallow: /compra", body);
        Assert.Contains("Disallow: /api", body);
        Assert.Contains("Sitemap: https://tienda.test/sitemap.xml", body);
    }
}
