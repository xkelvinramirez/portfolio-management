using Contracts.Common;
using ErrorOr;

namespace Infrastructure.MafAgent.Tools;

/// <summary>Reads every page of an existing paginated query (the query validators allow at most 100 per page).</summary>
internal static class ToolPaging
{
    public const int PageLimit = 100;

    public static async Task<ErrorOr<List<TOut>>> ReadAllAsync<TIn, TOut>(
        Func<PaginatorRequest, Task<ErrorOr<PaginatorResponse<TIn>>>> readPage, Func<TIn, TOut> map)
    {
        List<TOut> all = [];
        int page = 1;
        int totalPages;
        do
        {
            ErrorOr<PaginatorResponse<TIn>> result = await readPage(new PaginatorRequest(page, PageLimit));
            if (result.IsError)
            {
                return result.Errors;
            }

            all.AddRange(result.Value.Data.Select(map));
            totalPages = result.Value.TotalPages;
            page++;
        }
        while (page <= totalPages);

        return all;
    }
}
