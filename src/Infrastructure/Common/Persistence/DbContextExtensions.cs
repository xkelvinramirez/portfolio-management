
using Contracts.Common;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Common.Persistence;

public static class DbContextExtensions
{
    public static async Task<PaginatorResponse<T>> PaginateAsync<T>(this IQueryable<T> query, int page = 1, int limit = 10, CancellationToken cancellationToken = default) where T : class
    {
        var offset = (page - 1) * limit;
        var paginated = new PaginatorResponse<T>
        {
            Page = page,
            PageSize = limit,
            Total = await query.CountAsync(cancellationToken),
            Data = await query.Skip(offset).Take(limit).ToListAsync(cancellationToken)
        };

        paginated.TotalPages = (int)Math.Ceiling((decimal)paginated.Total / paginated.PageSize);

        return paginated;
    }
}
