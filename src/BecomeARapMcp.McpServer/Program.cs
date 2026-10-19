var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true);

var app = builder.Build();

app.MapMcp("/mcp");
app.MapDefaultEndpoints();
app.Run();
