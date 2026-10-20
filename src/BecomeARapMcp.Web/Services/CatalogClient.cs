using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BecomeARapMcp.Web.Models;

namespace BecomeARapMcp.Web.Services;

public sealed class CatalogClient(HttpClient http)
{
    public Task<IReadOnlyList<AlbumSummary>> GetAlbumsAsync(CancellationToken cancellationToken = default) =>
        GetPagesAsync<AlbumSummary>("albums", cancellationToken);

    public Task<IReadOnlyList<ArtistSummary>> SearchArtistsAsync(string query, CancellationToken cancellationToken = default) =>
        GetPagesAsync<ArtistSummary>($"artists?search={Uri.EscapeDataString(query)}", cancellationToken);

    public Task<IReadOnlyList<Track>> SearchTracksAsync(string query, CancellationToken cancellationToken = default) =>
        GetPagesAsync<Track>($"songs?search={Uri.EscapeDataString(query)}", cancellationToken);

    public Task<IReadOnlyList<Track>> GetAppearancesAsync(int artistId, CancellationToken cancellationToken = default) =>
        GetPagesAsync<Track>($"songs?artistId={artistId}", cancellationToken);

    public Task<AlbumDetail?> GetAlbumAsync(int id, CancellationToken cancellationToken = default) =>
        GetDetailAsync<AlbumDetail>($"albums/{id}", cancellationToken);

    public Task<ArtistDetail?> GetArtistAsync(int id, CancellationToken cancellationToken = default) =>
        GetDetailAsync<ArtistDetail>($"artists/{id}", cancellationToken);

    public Task<Track?> GetTrackAsync(int id, CancellationToken cancellationToken = default) =>
        GetDetailAsync<Track>($"songs/{id}", cancellationToken);

    private async Task<T?> GetDetailAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(path, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
            ?? throw new JsonException($"The catalog returned an empty response for {path}.");
    }

    private async Task<IReadOnlyList<T>> GetPagesAsync<T>(string path, CancellationToken cancellationToken)
    {
        var items = new List<T>();
        for (var pageNumber = 1; ; pageNumber++)
        {
            var separator = path.Contains('?') ? '&' : '?';
            var page = await http.GetFromJsonAsync<PagedResponse<T>>(
                $"{path}{separator}page={pageNumber}&pageSize=100", cancellationToken)
                ?? throw new JsonException($"The catalog returned an empty page for {path}.");

            if (page.Page != pageNumber || page.PageSize <= 0 || page.TotalCount < 0
                || page.Items is null || page.Items.Count > page.PageSize
                || (page.Items.Count == 0 && items.Count < page.TotalCount))
            {
                throw new InvalidDataException($"The catalog returned invalid pagination for {path}.");
            }

            items.AddRange(page.Items);
            if (items.Count >= page.TotalCount)
            {
                return items;
            }
        }
    }
}
