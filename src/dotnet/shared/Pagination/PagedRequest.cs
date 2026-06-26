namespace Shared.Pagination;

public class PagedRequest
{
    public int Page { get; set; } = 1;
    public int Limit { get; set; } = 10;
}
