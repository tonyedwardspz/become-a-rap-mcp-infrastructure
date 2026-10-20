# Become a Rap MCP

A .NET 10 / Aspire solution with a Blazor catalog browser, a Minimal API, an empty MCP server, and a
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
The dashboard shows `web`, `api`, `mcp`, and `sqlite`; all applications should become
healthy. AppHost launches the apps with their Development HTTP profiles and
provides endpoint allocation, telemetry configuration, and the API's SQLite
connection string. There are no hardcoded inter-service URLs.

## API

Swagger UI is available at `/swagger` on the API and documents every read
endpoint. Use the addresses shown in the dashboard; with the default launch
profiles these are `http://localhost:5005` (`api`), `http://localhost:5086`
(`mcp`), `http://localhost:5203` (`web`), and `http://localhost:15037` (dashboard).

## Catalog browser

Open the **Music catalog** link on the `web` resource in the Aspire dashboard.
The read-only Blazor Web App uses static server rendering, ordinary links, a
submitted search form, and lightweight responsive CSS. It has no playback,
editing, login, WebAssembly download, or interactive server circuit.

- The homepage lists all albums grouped by album artist.
- Album pages show release information, ordered tracks, durations, genres,
  explicit flags, and linked primary/featured artist credits.
- Artist pages show available biography information, albums, and all primary
  and featured track appearances. Artists without their own albums are supported.
- Track pages show metadata and links back to the album and credited artists.
- Every page has a search box: press Enter or choose **Search**.

Search results are grouped into **Albums**, **Album artists**, **Track artists**,
and **Tracks**. Albums match title or album-artist name; artist groups match names;
tracks match title, album title, or credited artist name. A person can appear in
both artist groups. Results are case-insensitive, the query is retained in the
URL, and all API pages are read so groups/results are not silently truncated.
The web application filters album summaries because the API has no album-search
parameter. This intentionally simple approach suits the current small catalog;
a much larger catalog would benefit from API-side album search.

Album covers and artist photos are loaded on detail pages using
[TheAudioDB's free API](https://www.theaudiodb.com/free_music_api), with its
documented shared free key `123`. No account, paid subscription, or private
secret is needed. Images are displayed directly from external HTTPS URLs;
neither images nor lookup responses are written to the project or database.
Only lookup URLs/misses are cached temporarily in memory. The application
limits itself to 30 lookups/minute, uses a two-second lookup timeout without
retries, and shows a placeholder when a match is absent, the service is offline
or rate-limited, or the image itself fails to load. Coverage is not guaranteed.
Provider attribution is shown with returned artwork; images remain subject to
their source/provider terms. Normal browser/CDN caching can still occur.

The `web` application references `api` through Aspire service discovery, waits
for API readiness, and exports its health status, logs, metrics, and traces to
the dashboard. Catalog request failures display an explicit error and retry
link rather than an empty catalog.

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

Run the web client, search, artwork, and server-rendered page tests with:

```sh
dotnet test src/BecomeARapMcp.Web.Tests
```

Web tests use fake HTTP responses and do not require the external artwork
provider or a running Aspire host.
