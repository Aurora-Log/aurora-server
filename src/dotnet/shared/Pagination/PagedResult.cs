namespace Shared.Pagination;

public class PagedResult<T>(List<T> items, int totalItems, int page, int limit)
{
    public List<T> Items { get; set; } = items;
    public int Page { get; set; } = page;
    public int Limit { get; set; } = limit;
    public int TotalPages { get; set; } = (int)Math.Ceiling(totalItems / (double)limit);
    public int TotalItems { get; set; } = totalItems;

    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}
