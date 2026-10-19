var builder = DistributedApplication.CreateBuilder(args);

var sqlite = builder.AddSqlite(
    "sqlite",
    databasePath: Path.Combine(builder.AppHostDirectory, ".data"),
    databaseFileName: "become-a-rap-mcp.db");

builder.AddProject<Projects.BecomeARapMcp_Api>("api", launchProfileName: "http")
    .WithReference(sqlite)
    .WaitFor(sqlite)
    .WithHttpHealthCheck("/health")
    .WithUrl("/swagger", "Swagger UI");

builder.AddProject<Projects.BecomeARapMcp_McpServer>("mcp", launchProfileName: "http")
    .WithHttpHealthCheck("/health")
    .WithUrl("/mcp", "MCP (Streamable HTTP)");

builder.Build().Run();
