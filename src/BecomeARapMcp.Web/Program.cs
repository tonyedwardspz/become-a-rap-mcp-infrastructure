using BecomeARapMcp.Web.Components;
using BecomeARapMcp.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddRazorComponents();
builder.Services.AddHttpClient<CatalogClient>(client => client.BaseAddress = new Uri("https+http://api"));
builder.Services.AddScoped<CatalogSearch>();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ArtworkService>();
#pragma warning disable EXTEXP0001
// Optional artwork should fail quickly, not inherit the catalog's retry policy.
builder.Services.AddHttpClient("artwork", client =>
{
    client.BaseAddress = new Uri("https://www.theaudiodb.com/api/v1/json/123/");
    client.Timeout = TimeSpan.FromSeconds(2);
}).RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001

var app = builder.Build();

app.UseExceptionHandler("/Error", createScopeForErrors: true);
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>();
app.MapDefaultEndpoints();

app.Run();

public partial class Program;
