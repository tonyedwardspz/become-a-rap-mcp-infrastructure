using System.Text.Json;
using Microsoft.AspNetCore.Components;

namespace BecomeARapMcp.Web.Components;

public abstract class CatalogPageBase : ComponentBase
{
    [Inject] private ILogger<CatalogPageBase> Logger { get; set; } = default!;
    [CascadingParameter] protected HttpContext? HttpContext { get; set; }

    protected CancellationToken RequestCancellation => HttpContext?.RequestAborted ?? CancellationToken.None;
    protected string? ErrorMessage { get; private set; }

    protected async Task LoadAsync(Func<Task> load)
    {
        ErrorMessage = null;
        try
        {
            await load();
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidDataException
            || exception is OperationCanceledException && !RequestCancellation.IsCancellationRequested)
        {
            Logger.LogError(exception, "Could not load catalog page {Path}.", HttpContext?.Request.Path);
            ErrorMessage = "The catalog is unavailable right now. Please retry.";
            if (HttpContext is not null && !HttpContext.Response.HasStarted)
            {
                HttpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            }
        }
    }

    protected void MarkNotFound()
    {
        if (HttpContext is not null && !HttpContext.Response.HasStarted)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
        }
    }
}
