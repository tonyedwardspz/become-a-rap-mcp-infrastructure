# Become a Rap MCP

A minimal .NET 10 / Aspire solution with an API, an empty MCP server, and a
persistent SQLite resource. No Docker, database schema, or MCP tools are needed.

## Start

Install the .NET SDK specified in `global.json` (10.0.402 or a newer patch in that
feature band). From the solution directory:

```sh
dotnet restore
dotnet build --no-restore
dotnet run --project src/BecomeARapMcp.AppHost --launch-profile http
```

Open the dashboard login URL printed by AppHost, including its generated token.
The dashboard shows `api`, `mcp`, and `sqlite`; both applications should become
healthy. AppHost launches the apps with their Development HTTP profiles and
provides endpoint allocation, telemetry configuration, and the API's SQLite
connection string. There are no hardcoded inter-service URLs.

## Endpoints

Use the addresses shown in the dashboard; API and MCP are separate applications.
With the default launch profiles these are `http://localhost:5005` (`api`),
`http://localhost:5086` (`mcp`) and `http://localhost:15037` (dashboard).

| Resource | Path | Purpose |
| --- | --- | --- |
| `api` | `/swagger` | Swagger UI: expand `GET /hello`, select **Try it out**, then **Execute** |
| `api` | `/hello` | Returns HTTP 200 with `{"message":"Hello"}` |
| `mcp` | `/mcp` | MCP Streamable HTTP endpoint |
| Both apps | `/health` | Readiness; the API includes a SQLite connectivity query |
| Both apps | `/alive` | Process liveness only |

Swagger and health endpoints are enabled only in Development. Standard Aspire
logging, metrics, tracing, service discovery, and HTTP client resilience are
shared through ServiceDefaults. Telemetry is exported to the Aspire dashboard.

Configure an MCP client with the `mcp` address plus `/mcp` and Streamable HTTP
transport (for example, `http://localhost:<dashboard-shown-port>/mcp`). The server
supports initialization with stateless HTTP transport but advertises no tools.
An ordinary browser GET is not an MCP connection. Add tool registrations to the
MCP project's `AddMcpServer()` pipeline when needed.

## SQLite persistence

The Toolkit provisions `src/BecomeARapMcp.AppHost/.data/become-a-rap-mcp.db`,
independent of the shell's working directory. The file and any sidecars are
gitignored and survive AppHost restarts. The dashboard's `sqlite` resource is an
embedded file, not a separate server process; actual database connectivity is
checked by the API's built-in SQLite client health check. There is no SQLite
admin UI, EF Core model, or migration.

Stop AppHost with **Ctrl+C** to stop its managed applications. This scaffold is
for local development only: the application profiles use localhost HTTP with no
application authentication. Dashboard authentication remains enabled.

The AppHost's `http` profile explicitly permits local unencrypted transport.
Its `https` profile is also available if you prefer a TLS dashboard.
Direct references to Microsoft.Data.Sqlite 10.0.13 and MessagePack 2.5.307 keep
the integrations' transitive dependencies on patched stable versions.
