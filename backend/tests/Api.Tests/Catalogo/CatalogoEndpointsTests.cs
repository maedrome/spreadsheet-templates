using System.Net;
using System.Net.Http.Json;
using Api.Catalogo;
using Api.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.Catalogo;

public class CatalogoEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task Seed(Action<AppDbContext> arrange)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        arrange(db);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Catalog_lists_only_published_products_ordered_by_position()
    {
        await Seed(db =>
        {
            var second = TestData.PublishedTemplate("cat-second"); second.Position = 2;
            var first = TestData.PublishedTemplate("cat-first"); first.Position = 1;
            var draft = TestData.PublishableTemplate("cat-draft"); draft.Position = 0;
            var bundle = TestData.PublishableBundle("cat-bundle", first, second);
            bundle.Publish(TestData.Now);
            var draftBundle = TestData.PublishableBundle("cat-draft-bundle");
            db.AddRange(second, first, draft, bundle, draftBundle);
        });

        var catalog = await factory.CreateClient().GetFromJsonAsync<CatalogoDto>("/api/catalogo");

        Assert.NotNull(catalog);
        var slugs = catalog.Templates.Select(t => t.Slug).Where(s => s is "cat-first" or "cat-second").ToList();
        Assert.Equal(["cat-first", "cat-second"], slugs);
        Assert.DoesNotContain(catalog.Templates, t => t.Slug == "cat-draft");
        Assert.DoesNotContain(catalog.Bundles, b => b.Slug == "cat-draft-bundle");

        var bundleCard = Assert.Single(catalog.Bundles, b => b.Slug == "cat-bundle");
        Assert.Equal(300, bundleCard.SavingsCents);
        Assert.Equal(["cat-first", "cat-second"], bundleCard.Templates.Select(t => t.Slug));
        var variant = Assert.Single(bundleCard.Templates[0].MainPreview!.Variants);
        Assert.Equal("/api/previews/cat-first-640.webp", variant.Url);
        Assert.Equal((640, 480), (variant.Width, variant.Height));
    }

    [Fact]
    public async Task Template_detail_returns_all_previews_in_order()
    {
        await Seed(db =>
        {
            var template = TestData.PublishedTemplate("det-template");
            template.AddPreview().Variants.Add(new PreviewVariant { Width = 320, Height = 200, FileName = "det-template-2.webp" });
            db.Add(template);
        });

        var detail = await factory.CreateClient().GetFromJsonAsync<TemplateDetailDto>("/api/plantillas/det-template");

        Assert.NotNull(detail);
        Assert.Equal(2, detail.Previews.Count);
        Assert.Equal("/api/previews/det-template-640.webp", detail.Previews[0].Variants[0].Url);
    }

    [Fact]
    public async Task Bundle_detail_includes_savings_and_template_previews()
    {
        await Seed(db =>
        {
            var bundle = TestData.PublishableBundle("det-bundle");
            bundle.Publish(TestData.Now);
            db.Add(bundle);
        });

        var detail = await factory.CreateClient().GetFromJsonAsync<BundleDetailDto>("/api/paquetes/det-bundle");

        Assert.NotNull(detail);
        Assert.Equal(300, detail.SavingsCents);
        Assert.Equal(2, detail.Templates.Count);
        Assert.All(detail.Templates, t => Assert.NotNull(t.MainPreview));
    }

    [Theory]
    [InlineData("/api/plantillas/missing")]
    [InlineData("/api/plantillas/det-unpublished")]
    [InlineData("/api/paquetes/missing")]
    [InlineData("/api/paquetes/det-unpublished-bundle")]
    public async Task Detail_returns_404_when_missing_or_unpublished(string url)
    {
        await Seed(db =>
        {
            if (!db.Templates.Any(t => t.Slug == "det-unpublished"))
                db.Add(TestData.PublishableTemplate("det-unpublished"));
            if (!db.Bundles.Any(b => b.Slug == "det-unpublished-bundle"))
                db.Add(TestData.PublishableBundle("det-unpublished-bundle"));
        });

        var response = await factory.CreateClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Preview_is_served_with_immutable_cache()
    {
        var client = factory.CreateClient();
        var path = Path.Combine(factory.StorageRoot, "previews", "served-1.webp");
        await File.WriteAllBytesAsync(path, [1, 2, 3]);

        var response = await client.GetAsync("/api/previews/served-1.webp");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/webp", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("public, max-age=31536000, immutable", response.Headers.CacheControl?.ToString());
        Assert.Equal([1, 2, 3], await response.Content.ReadAsByteArrayAsync());
    }

    [Theory]
    [InlineData("..%2f..%2fsecret.webp")]
    [InlineData("%2e%2e%2fsecret.webp")]
    [InlineData("..%5csecret.webp")]
    [InlineData("secret.txt")]
    [InlineData("SECRET.webp")]
    [InlineData("secret.webp.png")]
    [InlineData("missing.webp")]
    public async Task Preview_rejects_unsafe_or_unknown_names(string fileName)
    {
        var client = factory.CreateClient();
        await File.WriteAllBytesAsync(Path.Combine(factory.StorageRoot, "secret.webp"), [9]);
        await File.WriteAllTextAsync(Path.Combine(factory.StorageRoot, "previews", "secret.txt"), "x");

        var response = await client.GetAsync($"/api/previews/{fileName}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
