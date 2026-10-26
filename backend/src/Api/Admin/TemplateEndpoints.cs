using System.Text.RegularExpressions;
using Api.Catalogo;
using Api.Common;
using Api.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Admin;

public record TemplateInput(string Slug, string Name, string Description, int PriceCents, int? Position, string? GoogleSheetsCopyUrl);

public record TemplateListItem(
    int Id, string Slug, string Name, int PriceCents, int Position, bool IsPublished,
    string? MainPreviewUrl, int PreviewCount, bool HasXlsx, bool HasGoogleSheets);

public record TemplateDetail(
    int Id, string Slug, string Name, string Description, int PriceCents, int Position,
    bool IsPublished, bool SlugLocked, bool HasXlsx, string? GoogleSheetsCopyUrl,
    List<PreviewDto> Previews, List<NamedRef> Bundles, List<string> MissingForPublication);

public record PreviewDto(int Id, int Position, List<PreviewVariantDto> Variants);
public record PreviewVariantDto(int Width, int Height, string Url);
public record NamedRef(int Id, string Name, bool IsPublished);
public record PreviewOrderInput(List<int> PreviewIds);

public static partial class TemplateEndpoints
{
    private const int MaxDescriptionLength = 10_000;

    public static void MapAdminTemplates(this RouteGroupBuilder group)
    {
        group.MapGet("", List);
        group.MapGet("/{id:int}", Get);
        group.MapPost("", Create);
        group.MapPut("/{id:int}", Update);
        group.MapDelete("/{id:int}", Delete);
        group.MapPost("/{id:int}/publicar", Publish);
        group.MapPost("/{id:int}/despublicar", Unpublish);

        group.MapPost("/{id:int}/previews", UploadPreview)
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(PreviewImages.MaxBytes + 1024 * 1024));
        group.MapPut("/{id:int}/previews/orden", ReorderPreviews);
        group.MapDelete("/{id:int}/previews/{previewId:int}", DeletePreview);

        group.MapPut("/{id:int}/xlsx", UploadXlsx)
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(XlsxFiles.MaxBytes + 1024 * 1024));
    }

    private static async Task<List<TemplateListItem>> List(AppDbContext db)
    {
        var templates = await db.Templates.AsNoTracking()
            .Include(t => t.Previews)
            .OrderBy(t => t.Position).ThenBy(t => t.Name)
            .ToListAsync();

        return templates.Select(t => new TemplateListItem(
            t.Id, t.Slug, t.Name, t.PriceCents, t.Position, t.IsPublished,
            t.MainPreview?.Variants.OrderBy(v => v.Width).Select(v => PreviewFiles.Url(v.FileName)).FirstOrDefault(),
            t.Previews.Count, t.XlsxPath is not null, t.GoogleSheetsCopyUrl is not null)).ToList();
    }

    private static async Task<Results<Ok<TemplateDetail>, NotFound>> Get(int id, AppDbContext db)
    {
        var template = await Load(db, id);
        return template is null ? TypedResults.NotFound() : TypedResults.Ok(ToDetail(template));
    }

    private static async Task<Results<Created<TemplateDetail>, ValidationProblem>> Create(TemplateInput input, AppDbContext db)
    {
        var errors = await Validate(input, db, id: null);
        if (errors.Count > 0)
            return TypedResults.ValidationProblem(errors);

        var position = input.Position ?? (await db.Templates.MaxAsync(t => (int?)t.Position) ?? -1) + 1;
        var template = new Template(input.Slug, input.Name.Trim())
        {
            Description = input.Description.Trim(),
            PriceCents = input.PriceCents,
            Position = position,
            GoogleSheetsCopyUrl = NullIfBlank(input.GoogleSheetsCopyUrl),
        };
        db.Templates.Add(template);
        await db.SaveChangesAsync();

        return TypedResults.Created($"/api/admin/plantillas/{template.Id}", ToDetail(template));
    }

    private static async Task<Results<Ok<TemplateDetail>, NotFound, ValidationProblem>> Update(int id, TemplateInput input, AppDbContext db)
    {
        var template = await Load(db, id);
        if (template is null)
            return TypedResults.NotFound();
        var errors = await Validate(input, db, id);
        if (errors.Count > 0)
            return TypedResults.ValidationProblem(errors);

        template.ChangeSlug(input.Slug);
        template.Name = input.Name.Trim();
        template.Description = input.Description.Trim();
        template.PriceCents = input.PriceCents;
        template.Position = input.Position ?? template.Position;
        template.GoogleSheetsCopyUrl = NullIfBlank(input.GoogleSheetsCopyUrl);
        EnsureStillPublishable(template);

        await db.SaveChangesAsync();
        return TypedResults.Ok(ToDetail(template));
    }

    private static async Task<Results<NoContent, NotFound>> Delete(int id, AppDbContext db, PreviewImages images, XlsxFiles xlsx)
    {
        var template = await Load(db, id);
        if (template is null)
            return TypedResults.NotFound();
        if (template.IsPublished)
            throw new DomainException("Despublica la Plantilla antes de borrarla.");
        if (await db.Orders.AnyAsync(o => o.TemplateId == id) || await db.OrderTemplates.AnyAsync(ot => ot.TemplateId == id))
            throw new DomainException("No se puede borrar la Plantilla porque tiene Compras. Puedes dejarla sin publicar.");

        var previewFiles = template.Previews.SelectMany(p => p.Variants).Select(v => v.FileName).ToList();
        db.Templates.Remove(template);
        await db.SaveChangesAsync();

        images.Delete(await UnreferencedAsync(db, previewFiles));
        xlsx.Delete(template.XlsxPath);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<TemplateDetail>, NotFound>> Publish(int id, AppDbContext db, TimeProvider time)
    {
        var template = await Load(db, id);
        if (template is null)
            return TypedResults.NotFound();

        template.Publish(time.GetUtcNow());
        await db.SaveChangesAsync();
        return TypedResults.Ok(ToDetail(template));
    }

    private static async Task<Results<Ok<TemplateDetail>, NotFound>> Unpublish(int id, AppDbContext db)
    {
        var template = await Load(db, id);
        if (template is null)
            return TypedResults.NotFound();

        template.Unpublish();
        await db.SaveChangesAsync();
        return TypedResults.Ok(ToDetail(template));
    }

    private static async Task<Results<Ok<TemplateDetail>, NotFound, ValidationProblem>> UploadPreview(
        int id, IFormFile? file, AppDbContext db, PreviewImages images, CancellationToken cancellationToken)
    {
        var template = await Load(db, id);
        if (template is null)
            return TypedResults.NotFound();
        if (file is null || file.Length == 0)
            return FileMissing();
        if (file.Length > PreviewImages.MaxBytes)
            throw new DomainException("La imagen pesa más de 10 MB.");

        using var content = new MemoryStream((int)file.Length);
        await file.CopyToAsync(content, cancellationToken);
        var variants = images.Save(content.ToArray());

        template.AddPreview().Variants.AddRange(variants);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ToDetail(template));
    }

    private static async Task<Results<Ok<TemplateDetail>, NotFound>> ReorderPreviews(int id, PreviewOrderInput input, AppDbContext db)
    {
        var template = await Load(db, id);
        if (template is null)
            return TypedResults.NotFound();

        var current = template.Previews.Select(p => p.Id).Order();
        if (input.PreviewIds.Distinct().Count() != input.PreviewIds.Count || !current.SequenceEqual(input.PreviewIds.Order()))
            throw new DomainException("El nuevo orden tiene que incluir cada Preview de la Plantilla una sola vez.");

        foreach (var preview in template.Previews)
            preview.Position = input.PreviewIds.IndexOf(preview.Id);
        await db.SaveChangesAsync();
        return TypedResults.Ok(ToDetail(template));
    }

    private static async Task<Results<Ok<TemplateDetail>, NotFound>> DeletePreview(int id, int previewId, AppDbContext db, PreviewImages images)
    {
        var template = await Load(db, id);
        var preview = template?.Previews.SingleOrDefault(p => p.Id == previewId);
        if (template is null || preview is null)
            return TypedResults.NotFound();
        if (template.IsPublished && template.Previews.Count == 1)
            throw new DomainException("No se puede borrar la única Preview de una Plantilla publicada.");

        var files = preview.Variants.Select(v => v.FileName).ToList();
        template.Previews.Remove(preview);
        await db.SaveChangesAsync();

        images.Delete(await UnreferencedAsync(db, files));
        return TypedResults.Ok(ToDetail(template));
    }

    private static async Task<Results<Ok<TemplateDetail>, NotFound, ValidationProblem>> UploadXlsx(
        int id, IFormFile? file, AppDbContext db, XlsxFiles xlsx, CancellationToken cancellationToken)
    {
        var template = await Load(db, id);
        if (template is null)
            return TypedResults.NotFound();
        if (file is null || file.Length == 0)
            return FileMissing();

        var fileName = await xlsx.SaveAsync(file, cancellationToken);
        var previous = template.XlsxPath;
        template.XlsxPath = fileName;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            xlsx.Delete(fileName);
            throw;
        }

        xlsx.Delete(previous);
        return TypedResults.Ok(ToDetail(template));
    }

    private static Task<Template?> Load(AppDbContext db, int id) =>
        db.Templates
            .Include(t => t.Previews)
            .Include(t => t.BundleTemplates).ThenInclude(bt => bt.Bundle).ThenInclude(b => b.Templates).ThenInclude(bt => bt.Template)
            .AsSplitQuery()
            .SingleOrDefaultAsync(t => t.Id == id);

    /// <summary>
    /// Una Plantilla publicada no puede quedar incompleta, ni dejar sin Ahorro a un Paquete publicado al cambiar su precio.
    /// </summary>
    private static void EnsureStillPublishable(Template template)
    {
        if (!template.IsPublished)
            return;

        var missing = template.MissingForPublication();
        if (missing.Count > 0)
            throw new DomainException(
                $"La Plantilla está publicada y quedaría sin {string.Join(", ", missing)}. Despublícala antes de quitarlo.");

        foreach (var bundle in template.BundleTemplates.Select(bt => bt.Bundle).Where(b => b.IsPublished))
        {
            var problems = bundle.PublicationProblems();
            if (problems.Count > 0)
                throw new DomainException(
                    $"El Paquete publicado \"{bundle.Name}\" dejaría de cumplir las reglas: {string.Join("; ", problems)}.");
        }
    }

    private static async Task<Dictionary<string, string[]>> Validate(TemplateInput input, AppDbContext db, int? id)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(input.Name))
            errors["name"] = ["El nombre es obligatorio."];
        else if (input.Name.Trim().Length > 200)
            errors["name"] = ["El nombre no puede pasar de 200 caracteres."];
        if (input.Description.Length > MaxDescriptionLength)
            errors["description"] = [$"La descripción no puede pasar de {MaxDescriptionLength} caracteres."];
        if (input.PriceCents < 0)
            errors["priceCents"] = ["El precio no puede ser negativo."];
        if (input.Position < 0)
            errors["position"] = ["La posición no puede ser negativa."];
        if (!string.IsNullOrWhiteSpace(input.GoogleSheetsCopyUrl) && !GoogleSheetsCopyUrl().IsMatch(input.GoogleSheetsCopyUrl.Trim()))
            errors["googleSheetsCopyUrl"] = ["El link debe tener la forma https://docs.google.com/spreadsheets/d/{id}/copy."];

        if (!SlugFormat.IsValid(input.Slug))
            errors["slug"] = ["Usa solo minúsculas, números y guiones."];
        else if (await db.Templates.AnyAsync(t => t.Slug == input.Slug && t.Id != id))
            errors["slug"] = ["Ya existe una Plantilla con ese slug."];
        return errors;
    }

    private static ValidationProblem FileMissing() =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["file"] = ["Selecciona un archivo."] });

    private static async Task<List<string>> UnreferencedAsync(AppDbContext db, List<string> fileNames)
    {
        // La misma imagen subida dos veces comparte archivos: solo se borran los que ya nadie usa.
        var stillUsed = new HashSet<string>();
        foreach (var fileName in fileNames)
            if (await db.Previews.AnyAsync(p => p.Variants.Any(v => v.FileName == fileName)))
                stillUsed.Add(fileName);
        return fileNames.Where(f => !stillUsed.Contains(f)).ToList();
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static TemplateDetail ToDetail(Template t) => new(
        t.Id, t.Slug, t.Name, t.Description, t.PriceCents, t.Position,
        t.IsPublished, t.FirstPublishedAt is not null, t.XlsxPath is not null, t.GoogleSheetsCopyUrl,
        t.Previews.OrderBy(p => p.Position).Select(p => new PreviewDto(
            p.Id, p.Position,
            p.Variants.OrderBy(v => v.Width).Select(v => new PreviewVariantDto(v.Width, v.Height, PreviewFiles.Url(v.FileName))).ToList())).ToList(),
        t.BundleTemplates.Select(bt => new NamedRef(bt.Bundle.Id, bt.Bundle.Name, bt.Bundle.IsPublished)).ToList(),
        t.MissingForPublication());

    [GeneratedRegex("^https://docs\\.google\\.com/spreadsheets/d/[A-Za-z0-9_-]+/copy$")]
    private static partial Regex GoogleSheetsCopyUrl();
}

internal static class SlugFormat
{
    public static bool IsValid(string? slug)
    {
        if (string.IsNullOrEmpty(slug))
            return false;
        try
        {
            Slug.EnsureValid(slug);
            return true;
        }
        catch (DomainException)
        {
            return false;
        }
    }
}
