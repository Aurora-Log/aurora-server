using Microsoft.EntityFrameworkCore;

namespace Shared.Pagination;

public static class PaginationExtensions
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>
    (this IQueryable<T> query, PagedRequest request, CancellationToken cancellationToken = default)
    {
        var page = request.Page > 0 ? request.Page : 1;
        var limit = request.Limit > 0 ? request.Limit : 10;

        var totalItems = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>(items, totalItems, page, limit);
    }
}
