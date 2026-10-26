using Api.Common;
using Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace Api.Catalogo;

/// <summary>Catálogo de ejemplo para desarrollo. Idempotente: no hace nada si ya existe.</summary>
public static class DevSeed
{
    private record SeedTemplate(string Slug, string Name, string Description, int PriceCents, SKColor Accent, string[] Headers);

    private static readonly SeedTemplate[] Templates =
    [
        new("presupuesto-mensual", "Presupuesto mensual",
            "Controla tus ingresos y gastos del mes en una sola hoja, con gráficos que se actualizan solos.",
            900, new SKColor(0x16, 0xA3, 0x4A), ["Categoría", "Presupuestado", "Real", "Diferencia"]),
        new("finanzas-familiares", "Finanzas familiares",
            "Un presupuesto pensado para hogares: gastos fijos, variables, ahorro y metas compartidas.",
            1200, new SKColor(0x0E, 0x74, 0x90), ["Gasto", "Mes 1", "Mes 2", "Mes 3"]),
        new("control-de-deudas", "Control de deudas",
            "Registra tus deudas, compara estrategias de pago y mira cuánto falta para quedar libre.",
            1500, new SKColor(0xB4, 0x53, 0x09), ["Deuda", "Saldo", "Cuota", "Tasa"]),
    ];

    private const string BundleSlug = "paquete-finanzas-completo";

    public static async Task RunAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IOptions<StorageOptions>>().Value;

        if (await db.Bundles.AnyAsync(b => b.Slug == BundleSlug, ct)
            || await db.Templates.AnyAsync(t => Templates.Select(s => s.Slug).Contains(t.Slug), ct))
            return;

        Directory.CreateDirectory(storage.PreviewsPath);
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
                XlsxPath = $"{seed.Slug}.xlsx",
                GoogleSheetsCopyUrl = $"https://docs.google.com/spreadsheets/d/{seed.Slug}/copy",
            };
            for (var p = 0; p < 2; p++)
            {
                var preview = template.AddPreview();
                foreach (var width in PreviewVariant.StandardWidths)
                {
                    var height = width * 5 / 8;
                    var fileName = $"{seed.Slug}-{p + 1}-{width}.webp";
                    File.WriteAllBytes(Path.Combine(storage.PreviewsPath, fileName), Draw(seed, p, width, height));
                    preview.Variants.Add(new PreviewVariant { Width = width, Height = height, FileName = fileName });
                }
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

    /// <summary>Dibuja una hoja de cálculo ficticia: encabezado, filas y un gráfico de barras (variante 0) o un gráfico circular (variante 1).</summary>
    private static byte[] Draw(SeedTemplate seed, int variant, int width, int height)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        var canvas = surface.Canvas;
        var scale = width / 1600f;
        canvas.Scale(scale);
        canvas.Clear(SKColors.White);

        using var accent = new SKPaint { Color = seed.Accent, IsAntialias = true };
        using var line = new SKPaint { Color = new SKColor(0xE5, 0xE7, 0xEB), IsStroke = true, StrokeWidth = 2 };
        using var text = new SKPaint { Color = new SKColor(0x37, 0x41, 0x51), IsAntialias = true };
        using var white = new SKPaint { Color = SKColors.White, IsAntialias = true };
        using var font = new SKFont(SKTypeface.Default, 34);
        using var bold = new SKFont(SKTypeface.FromFamilyName(null, SKFontStyle.Bold), 34);
        using var title = new SKFont(SKTypeface.FromFamilyName(null, SKFontStyle.Bold), 52);

        canvas.DrawRect(0, 0, 1600, 120, accent);
        canvas.DrawText(seed.Name, 48, 78, SKTextAlign.Left, title, white);

        const float tableLeft = 48, tableTop = 170, colWidth = 190, rowHeight = 62;
        for (var c = 0; c < seed.Headers.Length; c++)
        {
            var x = tableLeft + c * colWidth;
            canvas.DrawRect(x, tableTop, colWidth, rowHeight, accent);
            canvas.DrawText(seed.Headers[c], x + 16, tableTop + 42, SKTextAlign.Left, bold, white);
        }
        for (var r = 1; r <= 9; r++)
        {
            for (var c = 0; c < seed.Headers.Length; c++)
            {
                var x = tableLeft + c * colWidth;
                var y = tableTop + r * rowHeight;
                canvas.DrawRect(x, y, colWidth, rowHeight, line);
                var value = c == 0 ? $"Concepto {r}" : $"${(r * 37 + c * 91 + variant * 53) % 900 + 100}";
                canvas.DrawText(value, x + 16, y + 42, SKTextAlign.Left, font, text);
            }
        }

        const float chartLeft = 960, chartTop = 190, chartSize = 560;
        canvas.DrawRect(chartLeft, chartTop, chartSize, chartSize, line);
        if (variant == 0)
        {
            int[] heights = [220, 340, 280, 420, 360, 470];
            for (var b = 0; b < heights.Length; b++)
            {
                var shade = (byte)(255 - 30 * (b % 3));
                using var bar = new SKPaint { Color = seed.Accent.WithAlpha(shade), IsAntialias = true };
                canvas.DrawRect(chartLeft + 36 + b * 84, chartTop + chartSize - 30 - heights[b], 60, heights[b], bar);
            }
        }
        else
        {
            var oval = new SKRect(chartLeft + 60, chartTop + 60, chartLeft + chartSize - 60, chartTop + chartSize - 60);
            float[] sweeps = [150, 100, 70, 40];
            var start = -90f;
            for (var s = 0; s < sweeps.Length; s++)
            {
                using var slice = new SKPaint { Color = seed.Accent.WithAlpha((byte)(255 - s * 55)), IsAntialias = true };
                canvas.DrawArc(oval, start, sweeps[s], true, slice);
                start += sweeps[s];
            }
        }

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Webp, 85);
        return data.ToArray();
    }
}
