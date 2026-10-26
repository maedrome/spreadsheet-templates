namespace Api.Common;

/// <summary>Raíz del volumen de archivos: Previews públicas en <c>previews/</c>, archivos .xlsx privados en <c>templates/</c>.</summary>
public class StorageOptions
{
    public const string Section = "Storage";

    public string Root { get; set; } = "/data/files";

    public string PreviewsPath => Path.Combine(Root, "previews");
    public string TemplatesPath => Path.Combine(Root, "templates");
}
