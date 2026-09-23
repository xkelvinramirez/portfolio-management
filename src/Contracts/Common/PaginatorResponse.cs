
namespace Contracts.Common;

public class PaginatorResponse<T>
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
    public int TotalPages { get; set; }
    public IEnumerable<T> Data { get; set; } = [];
}

