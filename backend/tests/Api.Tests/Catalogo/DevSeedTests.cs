using System.IO.Compression;
using System.Xml.Linq;
using Api.Catalogo;
using Api.Common;
using Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace Api.Tests.Catalogo;

// Fixture propia: el seed publica un catálogo que no debe mezclarse con los datos de otros tests.
public class DevSeedTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Seed_writes_a_valid_sample_xlsx_for_every_template_and_restores_missing_ones()
    {
        factory.CreateClient();
        await DevSeed.RunAsync(factory.Services);

        var templatesPath = factory.Services.GetRequiredService<IOptions<StorageOptions>>().Value.TemplatesPath;
        var seeded = await QueryAsync(db => db.Templates.Select(t => new { t.Slug, t.XlsxPath }).ToListAsync());
        Assert.Equal(3, seeded.Count);

        foreach (var template in seeded)
        {
            var path = Path.Combine(templatesPath, template.XlsxPath!);
            Assert.True(File.Exists(path), $"Falta el .xlsx de {template.Slug}.");
            AssertIsWorkbookWithHeaders(path);
        }

        var monthly = seeded.Single(t => t.Slug == "presupuesto-mensual");
        Assert.Contains("Categoría", SheetTexts(Path.Combine(templatesPath, monthly.XlsxPath!)));

        // Con el catálogo ya creado, el seed no vuelve a insertar nada, pero recupera el archivo que falte.
        var missing = Path.Combine(templatesPath, monthly.XlsxPath!);
        File.Delete(missing);
        await DevSeed.RunAsync(factory.Services);

        Assert.True(File.Exists(missing));
        Assert.Equal(3, await QueryAsync(db => db.Templates.CountAsync()));
    }

    [Fact]
    public async Task Seed_writes_every_preview_variant_at_its_declared_size_and_restores_missing_ones()
    {
        factory.CreateClient();
        await DevSeed.RunAsync(factory.Services);

        var previewsPath = factory.Services.GetRequiredService<IOptions<StorageOptions>>().Value.PreviewsPath;
        var variants = await QueryAsync(db => db.Templates.AsNoTracking().SelectMany(t => t.Previews).SelectMany(p => p.Variants).ToListAsync());
        Assert.Equal(3 * 2 * PreviewVariant.StandardWidths.Length, variants.Count);

        foreach (var variant in variants)
        {
            var path = Path.Combine(previewsPath, variant.FileName);
            using var codec = SKCodec.Create(path);
            Assert.NotNull(codec);
            Assert.Equal(SKEncodedImageFormat.Webp, codec.EncodedFormat);
            Assert.Equal((variant.Width, variant.Height), (codec.Info.Width, codec.Info.Height));
        }

        var missing = Path.Combine(previewsPath, variants[0].FileName);
        File.Delete(missing);
        await DevSeed.RunAsync(factory.Services);

        Assert.True(File.Exists(missing));
    }

    private static void AssertIsWorkbookWithHeaders(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        var entries = zip.Entries.Select(e => e.FullName).ToHashSet();
        Assert.Contains("[Content_Types].xml", entries);
        Assert.Contains("_rels/.rels", entries);
        Assert.Contains("xl/workbook.xml", entries);
        Assert.Contains("xl/_rels/workbook.xml.rels", entries);
        Assert.Contains("xl/worksheets/sheet1.xml", entries);
        Assert.Equal(4 + 9, SheetTexts(path).Count); // 4 encabezados y una etiqueta "Concepto N" por cada una de las 9 filas.
    }

    private static List<string> SheetTexts(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        using var sheet = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        XNamespace main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        return XDocument.Load(sheet).Descendants(main + "t").Select(t => t.Value).ToList();
    }

    private async Task<T> QueryAsync<T>(Func<AppDbContext, Task<T>> query)
    {
        using var scope = factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}
