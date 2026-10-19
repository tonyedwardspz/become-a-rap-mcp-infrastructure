# Become a Rap MCP

A .NET 10 / Aspire solution with a Minimal API, an empty MCP server, and a
persistent SQLite resource. The API exposes a read-only music catalogue backed
by Entity Framework Core.

## Start

Install the .NET SDK specified in `global.json` (10.0.402 or a newer patch in that
feature band). From the solution directory:

```sh
dotnet restore
dotnet build
dotnet run --project src/BecomeARapMcp.AppHost --launch-profile http
```

Open the dashboard login URL printed by AppHost, including its generated token.
The dashboard shows `api`, `mcp`, and `sqlite`; both applications should become
healthy. AppHost launches the apps with their Development HTTP profiles and
provides endpoint allocation, telemetry configuration, and the API's SQLite
connection string. There are no hardcoded inter-service URLs.

## API

Swagger UI is available at `/swagger` on the API and documents every read
endpoint. Use the addresses shown in the dashboard; with the default launch
profiles these are `http://localhost:5005` (`api`), `http://localhost:5086`
(`mcp`), and `http://localhost:15037` (dashboard).

| Method | Path | Purpose |
| --- | --- | --- |
| `GET` | `/artists` | Paginated artist list; optional `search` |
| `GET` | `/artists/{id}` | Artist details and albums |
| `GET` | `/albums` | Paginated album list |
| `GET` | `/albums/{id}` | Album details and tracks |
| `GET` | `/songs` | Paginated song list; optional `search`, `genreId`, and `artistId` |
| `GET` | `/songs/{id}` | Song details with album, genre, and primary/featured artists |
| `GET` | `/genres` | Paginated genre list with song counts |
| `GET` | `/hello` | Returns HTTP 200 with `{"message":"Hello"}` |
| `GET` | `/mcp` | MCP Streamable HTTP endpoint, separate `mcp` application |

Collection endpoints accept `page` (default `1`) and `pageSize` (default `50`,
capped at `100`) and return `page`, `pageSize`, `totalCount`, and `items`.
Song filter parameters can be combined. Responses use DTOs rather than EF
entities. Swagger and health endpoints are enabled only in Development.

Configure an MCP client with the `mcp` address plus `/mcp` and Streamable HTTP
transport. The server supports initialization with stateless HTTP transport but
advertises no tools. An ordinary browser GET is not an MCP connection.

## SQLite database

The Toolkit provisions
`src/BecomeARapMcp.AppHost/.data/become-a-rap-mcp.db`, independent of the
shell's working directory. The file and any sidecars are gitignored and survive
AppHost restarts. The Aspire `sqlite` resource is an embedded file, not a
separate server process.

The API applies EF Core migrations and loads deterministic seed data at
Development startup. Seed data is maintained in
[music-catalogue.json](./src/BecomeARapMcp.Api/Data/Seed/music-catalogue.json).
It includes complete album track listings for Eminem's *The Marshall Mathers
LP* and *The Slim Shady LP*, Dr. Dre's *2001*, Xzibit's *Restless*, and Sapient's
*Letterhead*. Public release and track metadata references are recorded in the
JSON (MusicBrainz for release and recording credits, Deezer for track explicit
flags). Sapient's release date is left null because MusicBrainz and Apple Music
records differ. No lyrics or audio are stored. Startup adds only missing seed
rows and does not overwrite existing records.

To reset and reseed the catalogue in local Development, run:

```sh
dotnet run --project src/BecomeARapMcp.AppHost --launch-profile http -- --reset-database
```

The AppHost forwards this switch to the API, which migrates first, clears the
catalogue tables, and reseeds the existing persistent SQLite file. The reset
switch is rejected outside Development. Stop AppHost with **Ctrl+C** when
finished. Normal restarts preserve the catalogue.

Production does not automatically migrate or seed the database; deploy
migrations through the production database deployment process.

Standard Aspire logging, metrics, tracing, service discovery, and HTTP client
resilience are shared through ServiceDefaults. Telemetry is exported to the
Aspire dashboard. The AppHost's `http` profile explicitly permits local
unencrypted transport; its `https` profile is also available if you prefer a
TLS dashboard. Dashboard authentication remains enabled.

## Tests

Run the API integration tests with:

```sh
dotnet test src/BecomeARapMcp.Api.Tests
```
