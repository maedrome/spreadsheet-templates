using System.Security.Cryptography;
using Api.Catalogo;
using Api.Common;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace Api.Admin;

/// <summary>Convierte una imagen subida en las variantes WebP de una Preview, guardadas en <see cref="StorageOptions.PreviewsPath"/>.</summary>
public class PreviewImages(IOptions<StorageOptions> storage)
{
    public const long MaxBytes = 10 * 1024 * 1024;
    private const long MaxPixels = 50_000_000;
    private const int WebpQuality = 82;

    public static int MinWidth => PreviewVariant.StandardWidths.Max();

    /// <summary>Genera un WebP por cada ancho de <see cref="PreviewVariant.StandardWidths"/>, llamado <c>{hash}-{ancho}.webp</c>.</summary>
    /// <exception cref="DomainException">Si el contenido no es una imagen PNG, JPEG o WebP utilizable.</exception>
    public List<PreviewVariant> Save(byte[] content)
    {
        if (content.Length > MaxBytes)
            throw new DomainException("La imagen pesa más de 10 MB.");

        using var data = SKData.CreateCopy(content);
        using var codec = SKCodec.Create(data) ?? throw new DomainException("El archivo no es una imagen válida.");
        if (codec.EncodedFormat is not (SKEncodedImageFormat.Png or SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Webp))
            throw new DomainException("Formato de imagen no admitido: usa PNG, JPEG o WebP.");
        if ((long)codec.Info.Width * codec.Info.Height > MaxPixels)
            throw new DomainException("La imagen tiene demasiados píxeles.");

        using var decoded = SKBitmap.Decode(codec) ?? throw new DomainException("El archivo no es una imagen válida.");
        using var oriented = ApplyOrigin(decoded, codec.EncodedOrigin);
        var source = oriented ?? decoded;
        if (source.Width < MinWidth)
            throw new DomainException($"La imagen debe tener al menos {MinWidth} px de ancho; esta tiene {source.Width} px.");

        var hash = Convert.ToHexStringLower(SHA256.HashData(content))[..32];
        var variants = new List<PreviewVariant>();
        foreach (var width in PreviewVariant.StandardWidths)
        {
            var height = Math.Max(1, (int)Math.Round(source.Height * (double)width / source.Width));
            using var resized = source.Resize(new SKImageInfo(width, height), new SKSamplingOptions(SKCubicResampler.Mitchell))
                ?? throw new DomainException("No se pudo procesar la imagen.");
            using var image = SKImage.FromBitmap(resized);
            using var webp = image.Encode(SKEncodedImageFormat.Webp, WebpQuality);

            var fileName = $"{hash}-{width}.webp";
            using (var file = File.Create(PathOf(fileName)))
                webp.SaveTo(file);
            variants.Add(new PreviewVariant { Width = width, Height = height, FileName = fileName });
        }
        return variants;
    }

    public string PathOf(string fileName) => Path.Combine(storage.Value.PreviewsPath, fileName);

    public void Delete(IEnumerable<string> fileNames)
    {
        foreach (var fileName in fileNames)
            File.Delete(PathOf(fileName));
    }

    /// <summary>Aplica la rotación EXIF de las fotos de teléfono. Devuelve <c>null</c> si no hace falta rotar.</summary>
    private static SKBitmap? ApplyOrigin(SKBitmap bitmap, SKEncodedOrigin origin)
    {
        var degrees = origin switch
        {
            SKEncodedOrigin.BottomRight => 180,
            SKEncodedOrigin.RightTop => 90,
            SKEncodedOrigin.LeftBottom => 270,
            _ => 0,
        };
        if (degrees == 0)
            return null;

        var swap = degrees != 180;
        var rotated = new SKBitmap(swap ? bitmap.Height : bitmap.Width, swap ? bitmap.Width : bitmap.Height);
        using var canvas = new SKCanvas(rotated);
        canvas.Translate(rotated.Width / 2f, rotated.Height / 2f);
        canvas.RotateDegrees(degrees);
        canvas.Translate(-bitmap.Width / 2f, -bitmap.Height / 2f);
        canvas.DrawBitmap(bitmap, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));
        return rotated;
    }
}
