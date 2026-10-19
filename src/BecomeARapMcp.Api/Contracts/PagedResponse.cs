namespace BecomeARapMcp.Api.Contracts;

public sealed record PagedResponse<T>(int Page, int PageSize, int TotalCount, IReadOnlyList<T> Items);
