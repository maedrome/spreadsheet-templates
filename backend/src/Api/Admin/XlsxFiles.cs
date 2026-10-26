using Api.Common;
using Microsoft.Extensions.Options;

namespace Api.Admin;

/// <summary>Archivos .xlsx privados en <see cref="StorageOptions.TemplatesPath"/>. <c>Template.XlsxPath</c> es relativo a esa carpeta.</summary>
public class XlsxFiles(IOptions<StorageOptions> storage)
{
    public const long MaxBytes = 20 * 1024 * 1024;

    /// <summary>Un .xlsx es un ZIP: empieza con la firma de un local file header.</summary>
    private static readonly byte[] ZipSignature = "PK\x03\x04"u8.ToArray();

    /// <summary>Valida y guarda el archivo con un nombre generado. Devuelve ese nombre.</summary>
    /// <exception cref="DomainException">Si no tiene extensión .xlsx, no es un ZIP o pesa demasiado.</exception>
    public async Task<string> SaveAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new DomainException("El archivo debe tener la extensión .xlsx.");
        if (file.Length > MaxBytes)
            throw new DomainException("El archivo .xlsx pesa más de 20 MB.");

        await using var input = file.OpenReadStream();
        var header = new byte[ZipSignature.Length];
        if (await input.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken) < header.Length
            || !header.AsSpan().SequenceEqual(ZipSignature))
            throw new DomainException("El archivo no es un .xlsx válido.");

        var fileName = $"{Guid.NewGuid():N}.xlsx";
        await using (var output = File.Create(PathOf(fileName)))
        {
            await output.WriteAsync(header, cancellationToken);
            await input.CopyToAsync(output, cancellationToken);
        }
        return fileName;
    }

    public void Delete(string? fileName)
    {
        if (!string.IsNullOrEmpty(fileName))
            File.Delete(PathOf(fileName));
    }

    private string PathOf(string fileName) => Path.Combine(storage.Value.TemplatesPath, fileName);
}
