using System.IO.Compression;
using System.Xml.Linq;
using Api.Common;
using Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace Api.Catalogo;

/// <summary>
/// Catálogo de ejemplo para desarrollo. Idempotente: si el catálogo ya existe no lo toca,
/// pero igual vuelve a crear los .xlsx y las previews de ejemplo que falten en disco.
/// </summary>
public static class DevSeed
{
    private record SeedTemplate(string Slug, string Name, string Description, int PriceCents, string[] Headers);

    private static readonly SeedTemplate[] Templates =
    [
        new("presupuesto-mensual", "Presupuesto mensual",
            "Controla tus ingresos y gastos del mes en una sola hoja, con gráficos que se actualizan solos.",
            900, ["Categoría", "Presupuestado", "Real", "Diferencia"]),
        new("finanzas-familiares", "Finanzas familiares",
            "Un presupuesto pensado para hogares: gastos fijos, variables, ahorro y metas compartidas.",
            1200, ["Gasto", "Mes 1", "Mes 2", "Mes 3"]),
        new("control-de-deudas", "Control de deudas",
            "Registra tus deudas, compara estrategias de pago y mira cuánto falta para quedar libre.",
            1500, ["Deuda", "Saldo", "Cuota", "Tasa"]),
    ];

    private const string BundleSlug = "paquete-finanzas-completo";

    public static async Task RunAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IOptions<StorageOptions>>().Value;

        EnsureSampleXlsxFiles(storage);
        EnsureSamplePreviewFiles(storage);

        if (await db.Bundles.AnyAsync(b => b.Slug == BundleSlug, ct)
            || await db.Templates.AnyAsync(t => Templates.Select(s => s.Slug).Contains(t.Slug), ct))
            return;

        var now = DateTimeOffset.UtcNow;

        var templates = new List<Template>();
        for (var i = 0; i < Templates.Length; i++)
        {
            var seed = Templates[i];
            var template = new Template(seed.Slug, seed.Name)
            {
                Description = seed.Description,
                PriceCents = seed.PriceCents,
                Position = i,
                XlsxPath = XlsxFileName(seed),
                GoogleSheetsCopyUrl = $"https://docs.google.com/spreadsheets/d/{seed.Slug}/copy",
            };
            for (var p = 0; p < PreviewsPerTemplate; p++)
            {
                var preview = template.AddPreview();
                foreach (var width in PreviewVariant.StandardWidths)
                    preview.Variants.Add(new PreviewVariant { Width = width, Height = PreviewHeight(width), FileName = PreviewFileName(seed, p + 1, width) });
            }
            template.Publish(now);
            templates.Add(template);
        }

        var bundle = new Bundle(BundleSlug, "Paquete finanzas completo")
        {
            Description = "Las tres plantillas juntas: presupuesto, finanzas familiares y control de deudas, a un precio menor.",
            PriceCents = 2900,
            Position = 0,
        };
        foreach (var template in templates)
            bundle.AddTemplate(template);
        bundle.Publish(now);

        db.Templates.AddRange(templates);
        db.Bundles.Add(bundle);
        await db.SaveChangesAsync(ct);
    }

    private static string XlsxFileName(SeedTemplate seed) => $"{seed.Slug}.xlsx";

    private const int PreviewsPerTemplate = 2;

    private static string PreviewFileName(SeedTemplate seed, int number, int width) => $"{seed.Slug}-{number}-{width}.webp";

    private static int PreviewHeight(int width) => width * 5 / 8;

    private static void EnsureSamplePreviewFiles(StorageOptions storage)
    {
        Directory.CreateDirectory(storage.PreviewsPath);
        foreach (var seed in Templates)
            for (var number = 1; number <= PreviewsPerTemplate; number++)
                foreach (var width in PreviewVariant.StandardWidths)
                {
                    var path = Path.Combine(storage.PreviewsPath, PreviewFileName(seed, number, width));
                    if (!File.Exists(path))
                        File.WriteAllBytes(path, Render(seed.Slug, number, width, PreviewHeight(width)));
                }
    }

    private static void EnsureSampleXlsxFiles(StorageOptions storage)
    {
        Directory.CreateDirectory(storage.TemplatesPath);
        foreach (var seed in Templates)
        {
            var path = Path.Combine(storage.TemplatesPath, XlsxFileName(seed));
            if (!File.Exists(path))
                File.WriteAllBytes(path, BuildXlsx(seed));
        }
    }

    /// <summary>Un .xlsx mínimo pero válido (Open XML): una hoja con los encabezados de la Plantilla y filas de ejemplo.</summary>
    private static byte[] BuildXlsx(SeedTemplate seed)
    {
        XNamespace main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace officeRel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        XNamespace packageRel = "http://schemas.openxmlformats.org/package/2006/relationships";
        XNamespace contentTypes = "http://schemas.openxmlformats.org/package/2006/content-types";
        const string relType = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/";

        XElement Cell(int column, int row, object value)
        {
            var reference = new XAttribute("r", $"{(char)('A' + column)}{row}");
            return value is string text
                ? new XElement(main + "c", reference, new XAttribute("t", "inlineStr"), new XElement(main + "is", new XElement(main + "t", text)))
                : new XElement(main + "c", reference, new XElement(main + "v", value));
        }

        XElement Relationship(string type, string target) =>
            new(packageRel + "Relationships",
                new XElement(packageRel + "Relationship",
                    new XAttribute("Id", "rId1"), new XAttribute("Type", relType + type), new XAttribute("Target", target)));

        XElement ContentType(string kind, string key, string value) =>
            new(contentTypes + kind, new XAttribute(kind == "Default" ? "Extension" : "PartName", key), new XAttribute("ContentType", value));

        var rows = new List<XElement> { new(main + "row", new XAttribute("r", 1), seed.Headers.Select((h, c) => Cell(c, 1, h))) };
        for (var r = 2; r <= 10; r++)
        {
            var row = r;
            rows.Add(new XElement(main + "row", new XAttribute("r", row),
                seed.Headers.Select((_, c) => Cell(c, row, c == 0 ? $"Concepto {row - 1}" : (row * 37 + c * 91) % 900 + 100))));
        }

        var parts = new (string Name, XElement Xml)[]
        {
            ("[Content_Types].xml", new XElement(contentTypes + "Types",
                ContentType("Default", "rels", "application/vnd.openxmlformats-package.relationships+xml"),
                ContentType("Default", "xml", "application/xml"),
                ContentType("Override", "/xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"),
                ContentType("Override", "/xl/worksheets/sheet1.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"))),
            ("_rels/.rels", Relationship("officeDocument", "xl/workbook.xml")),
            ("xl/workbook.xml", new XElement(main + "workbook", new XAttribute(XNamespace.Xmlns + "r", officeRel),
                new XElement(main + "sheets",
                    new XElement(main + "sheet", new XAttribute("name", "Plantilla"), new XAttribute("sheetId", 1), new XAttribute(officeRel + "id", "rId1"))))),
            ("xl/_rels/workbook.xml.rels", Relationship("worksheet", "worksheets/sheet1.xml")),
            ("xl/worksheets/sheet1.xml", new XElement(main + "worksheet", new XElement(main + "sheetData", rows))),
        };

        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, xml) in parts)
            {
                using var entry = zip.CreateEntry(name).Open();
                new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), xml).Save(entry);
            }
        }
        return stream.ToArray();
    }

    /// <summary>
    /// Escala la imagen de ejemplo incrustada en <c>Catalogo/SeedPreviews</c> (capturas generadas con IA,
    /// 16:10) al ancho pedido.
    /// </summary>
    private static byte[] Render(string slug, int number, int width, int height)
    {
        var resource = $"{typeof(DevSeed).Namespace}.SeedPreviews.{slug}-{number}.webp";
        using var stream = typeof(DevSeed).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Falta la imagen de ejemplo {resource}.");
        using var source = SKBitmap.Decode(stream);
        using var resized = source.Resize(new SKImageInfo(width, height), new SKSamplingOptions(SKCubicResampler.Mitchell));
        using var image = SKImage.FromBitmap(resized);
        using var data = image.Encode(SKEncodedImageFormat.Webp, 85);
        return data.ToArray();
    }
}
