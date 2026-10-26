using System.Text;
using System.Xml.Linq;
using Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Catalogo;

public class SiteOptions
{
    public const string Section = "Site";

    public string BaseUrl { get; set; } = "http://localhost:4200";

    public string AbsoluteUrl(string path) => BaseUrl.TrimEnd('/') + path;
}

public static class SeoEndpoints
{
    public static IEndpointRouteBuilder MapSeo(this IEndpointRouteBuilder app)
    {
        app.MapGet("/sitemap.xml", GetSitemap);
        app.MapGet("/robots.txt", GetRobots);
        return app;
    }

    private static async Task<IResult> GetSitemap(AppDbContext db, IOptions<SiteOptions> site, CancellationToken ct)
    {
        var templates = await db.Templates.AsNoTracking().Where(t => t.IsPublished)
            .OrderBy(t => t.Position).ThenBy(t => t.Id).Select(t => t.Slug).ToListAsync(ct);
        var bundles = await db.Bundles.AsNoTracking().Where(b => b.IsPublished)
            .OrderBy(b => b.Position).ThenBy(b => b.Id).Select(b => b.Slug).ToListAsync(ct);

        var urls = new List<string> { site.Value.AbsoluteUrl("/") };
        urls.AddRange(bundles.Select(s => site.Value.AbsoluteUrl($"/paquetes/{s}")));
        urls.AddRange(templates.Select(s => site.Value.AbsoluteUrl($"/plantillas/{s}")));

        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(ns + "urlset", urls.Select(u => new XElement(ns + "url", new XElement(ns + "loc", u)))));

        return TypedResults.Text(document.Declaration + "\n" + document.Root, "application/xml", Encoding.UTF8);
    }

    private static IResult GetRobots(IOptions<SiteOptions> site)
    {
        var body = $"""
            User-agent: *
            Disallow: /admin
            Disallow: /compra
            Disallow: /api

            Sitemap: {site.Value.AbsoluteUrl("/sitemap.xml")}
            """;
        return TypedResults.Text(body, "text/plain", Encoding.UTF8);
    }
}
