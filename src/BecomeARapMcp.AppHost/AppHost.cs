var resetDatabase = args.Contains("--reset-database", StringComparer.Ordinal);
var appHostArguments = args.Where(argument => !string.Equals(argument, "--reset-database", StringComparison.Ordinal)).ToArray();
var builder = DistributedApplication.CreateBuilder(appHostArguments);

var sqlite = builder.AddSqlite(
    "sqlite",
    databasePath: Path.Combine(builder.AppHostDirectory, ".data"),
    databaseFileName: "become-a-rap-mcp.db");

var api = builder.AddProject<Projects.BecomeARapMcp_Api>("api", launchProfileName: "http")
    .WithReference(sqlite)
    .WaitFor(sqlite)
    .WithHttpHealthCheck("/health")
    .WithUrl("/swagger", "Swagger UI");

if (resetDatabase)
{
    api.WithEnvironment("CATALOGUE_RESET_DATABASE", "true");
}

builder.AddProject<Projects.BecomeARapMcp_McpServer>("mcp", launchProfileName: "http")
    .WithHttpHealthCheck("/health")
    .WithUrl("/mcp", "MCP (Streamable HTTP)");

builder.AddProject<Projects.BecomeARapMcp_Web>("web", launchProfileName: "http")
    .WithReference(api)
    .WaitFor(api)
    .WithHttpHealthCheck("/health")
    .WithUrl("/", "Music catalog");

builder.Build().Run();
