using Api.Catalogo;
using Api.Common;
using Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();

builder.Services.Configure<SiteOptions>(builder.Configuration.GetSection(SiteOptions.Section));
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.Section));
builder.Services.PostConfigure<StorageOptions>(options =>
    options.Root = Path.GetFullPath(options.Root, builder.Environment.ContentRootPath));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

var storage = app.Services.GetRequiredService<IOptions<StorageOptions>>().Value;
Directory.CreateDirectory(storage.PreviewsPath);
Directory.CreateDirectory(storage.TemplatesPath);

if (app.Environment.IsDevelopment())
    await DevSeed.RunAsync(app.Services);

app.MapHealthChecks("/api/health");
app.MapCatalogo();
app.MapSeo();

app.Run();
