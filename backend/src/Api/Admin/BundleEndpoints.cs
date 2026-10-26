using Api.Catalogo;
using Api.Common;
using Api.Compras;
using Api.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Api.Admin;

public record BundleInput(string Slug, string Name, string Description, int PriceCents, int? Position);
public record BundleTemplateInput(int TemplateId);

public record BundleListItem(
    int Id, string Slug, string Name, int PriceCents, int Position, bool IsPublished, int TemplateCount, int SavingsCents);

public record BundleDetail(
    int Id, string Slug, string Name, string Description, int PriceCents, int Position,
    bool IsPublished, bool SlugLocked, List<BundleTemplateDto> Templates,
    int TemplatesTotalCents, int SavingsCents, List<string> PublicationProblems);

public record BundleTemplateDto(int Id, string Slug, string Name, int PriceCents, bool IsPublished);

public static class BundleEndpoints
{
    private const int MaxDescriptionLength = 10_000;

    public static void MapAdminBundles(this RouteGroupBuilder group)
    {
        group.MapGet("", List);
        group.MapGet("/{id:int}", Get);
        group.MapPost("", Create);
        group.MapPut("/{id:int}", Update);
        group.MapDelete("/{id:int}", Delete);
        group.MapPost("/{id:int}/publicar", Publish);
        group.MapPost("/{id:int}/despublicar", Unpublish);
        group.MapPost("/{id:int}/plantillas", AddTemplate);
        group.MapDelete("/{id:int}/plantillas/{templateId:int}", RemoveTemplate);
    }

    private static async Task<List<BundleListItem>> List(AppDbContext db)
    {
        var bundles = await db.Bundles.AsNoTracking()
            .Include(b => b.Templates).ThenInclude(bt => bt.Template)
            .OrderBy(b => b.Position).ThenBy(b => b.Name)
            .ToListAsync();

        return bundles.Select(b => new BundleListItem(
            b.Id, b.Slug, b.Name, b.PriceCents, b.Position, b.IsPublished, b.Templates.Count, b.SavingsCents)).ToList();
    }

    private static async Task<Results<Ok<BundleDetail>, NotFound>> Get(int id, AppDbContext db)
    {
        var bundle = await Load(db, id);
        return bundle is null ? TypedResults.NotFound() : TypedResults.Ok(ToDetail(bundle));
    }

    private static async Task<Results<Created<BundleDetail>, ValidationProblem>> Create(BundleInput input, AppDbContext db)
    {
        var errors = await Validate(input, db, id: null);
        if (errors.Count > 0)
            return TypedResults.ValidationProblem(errors);

        var position = input.Position ?? (await db.Bundles.MaxAsync(b => (int?)b.Position) ?? -1) + 1;
        var bundle = new Bundle(input.Slug, input.Name.Trim())
        {
            Description = input.Description.Trim(),
            PriceCents = input.PriceCents,
            Position = position,
        };
        db.Bundles.Add(bundle);
        await db.SaveChangesAsync();

        return TypedResults.Created($"/api/admin/paquetes/{bundle.Id}", ToDetail(bundle));
    }

    private static async Task<Results<Ok<BundleDetail>, NotFound, ValidationProblem>> Update(
        int id, BundleInput input, AppDbContext db, IPaymentProvider payments, CancellationToken ct)
    {
        var bundle = await Load(db, id);
        if (bundle is null)
            return TypedResults.NotFound();
        var errors = await Validate(input, db, id);
        if (errors.Count > 0)
            return TypedResults.ValidationProblem(errors);

        var charged = (bundle.Name, bundle.Description, bundle.PriceCents);
        bundle.ChangeSlug(input.Slug);
        bundle.Name = input.Name.Trim();
        bundle.Description = input.Description.Trim();
        bundle.PriceCents = input.PriceCents;
        bundle.Position = input.Position ?? bundle.Position;
        if (bundle.IsPublished && bundle.PublicationProblems() is { Count: > 0 } problems)
            throw new DomainException(
                $"El Paquete está publicado y dejaría de cumplir las reglas: {string.Join("; ", problems)}. Despublícalo antes.");
        // Polar cobra con su propio producto: primero se actualiza allá, y si falla no se guarda nada.
        if (bundle.IsPublished && charged != (bundle.Name, bundle.Description, bundle.PriceCents))
            await payments.SyncAsync(bundle, ct);

        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToDetail(bundle));
    }

    private static async Task<Results<NoContent, NotFound>> Delete(int id, AppDbContext db)
    {
        var bundle = await Load(db, id);
        if (bundle is null)
            return TypedResults.NotFound();
        if (bundle.IsPublished)
            throw new DomainException("Despublica el Paquete antes de borrarlo.");
        if (await db.Orders.AnyAsync(o => o.BundleId == id))
            throw new DomainException("No se puede borrar el Paquete porque tiene Compras. Puedes dejarlo sin publicar.");

        db.Bundles.Remove(bundle);
        await db.SaveChangesAsync();
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<BundleDetail>, NotFound>> Publish(
        int id, AppDbContext db, TimeProvider time, IPaymentProvider payments, CancellationToken ct)
    {
        var bundle = await Load(db, id);
        if (bundle is null)
            return TypedResults.NotFound();

        bundle.Publish(time.GetUtcNow());
        await payments.SyncAsync(bundle, ct);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToDetail(bundle));
    }

    private static async Task<Results<Ok<BundleDetail>, NotFound>> Unpublish(int id, AppDbContext db)
    {
        var bundle = await Load(db, id);
        if (bundle is null)
            return TypedResults.NotFound();

        bundle.Unpublish();
        await db.SaveChangesAsync();
        return TypedResults.Ok(ToDetail(bundle));
    }

    private static async Task<Results<Ok<BundleDetail>, NotFound>> AddTemplate(int id, BundleTemplateInput input, AppDbContext db)
    {
        var bundle = await Load(db, id);
        if (bundle is null)
            return TypedResults.NotFound();
        var template = await db.Templates.SingleOrDefaultAsync(t => t.Id == input.TemplateId)
            ?? throw new DomainException("La Plantilla no existe.");

        bundle.AddTemplate(template);
        await db.SaveChangesAsync();
        return TypedResults.Ok(ToDetail(bundle));
    }

    private static async Task<Results<Ok<BundleDetail>, NotFound>> RemoveTemplate(int id, int templateId, AppDbContext db)
    {
        var bundle = await Load(db, id);
        var template = bundle?.Templates.SingleOrDefault(bt => bt.TemplateId == templateId)?.Template;
        if (bundle is null || template is null)
            return TypedResults.NotFound();

        bundle.RemoveTemplate(template);
        await db.SaveChangesAsync();
        return TypedResults.Ok(ToDetail(bundle));
    }

    private static Task<Bundle?> Load(AppDbContext db, int id) =>
        db.Bundles
            .Include(b => b.Templates).ThenInclude(bt => bt.Template)
            .SingleOrDefaultAsync(b => b.Id == id);

    private static async Task<Dictionary<string, string[]>> Validate(BundleInput input, AppDbContext db, int? id)
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

        if (!SlugFormat.IsValid(input.Slug))
            errors["slug"] = ["Usa solo minúsculas, números y guiones."];
        else if (await db.Bundles.AnyAsync(b => b.Slug == input.Slug && b.Id != id))
            errors["slug"] = ["Ya existe un Paquete con ese slug."];
        return errors;
    }

    private static BundleDetail ToDetail(Bundle b) => new(
        b.Id, b.Slug, b.Name, b.Description, b.PriceCents, b.Position, b.IsPublished, b.FirstPublishedAt is not null,
        b.Templates.OrderBy(bt => bt.Position)
            .Select(bt => new BundleTemplateDto(bt.Template.Id, bt.Template.Slug, bt.Template.Name, bt.Template.PriceCents, bt.Template.IsPublished))
            .ToList(),
        b.Templates.Sum(bt => bt.Template.PriceCents),
        b.SavingsCents,
        b.PublicationProblems());
}
