using System.Globalization;
using InvestindoEmNegocio.Application.DTOs;
using Microsoft.AspNetCore.Http;

namespace InvestindoEmNegocio.Infrastructure.Api;

public static class ListQueryHelper
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 200;

    public static (IReadOnlyList<T> Items, int Total, int Page, int PageSize, bool IsPaged) Apply<T>(
        IReadOnlyList<T> data,
        ListQuery? query,
        IReadOnlyDictionary<string, Func<T, object?>> sortSelectors)
    {
        var items = data.AsEnumerable();
        var total = data.Count;
        var isPaged = query?.Page is not null || query?.PageSize is not null;

        if (!string.IsNullOrWhiteSpace(query?.SortBy) && sortSelectors.Count > 0)
        {
            if (sortSelectors.TryGetValue(query.SortBy, out var selector))
            {
                items = IsDescending(query.SortDir)
                    ? items.OrderByDescending(selector)
                    : items.OrderBy(selector);
            }
        }

        if (!isPaged)
        {
            return (items.ToList(), total, 1, total, false);
        }

        var page = Math.Max(1, query?.Page ?? 1);
        var pageSize = Math.Clamp(query?.PageSize ?? DefaultPageSize, 1, MaxPageSize);
        items = items.Skip((page - 1) * pageSize).Take(pageSize);

        return (items.ToList(), total, page, pageSize, true);
    }

    public static void WritePaginationHeaders(HttpResponse response, int total, int page, int pageSize)
    {
        response.Headers["X-Total-Count"] = total.ToString(CultureInfo.InvariantCulture);
        response.Headers["X-Page"] = page.ToString(CultureInfo.InvariantCulture);
        response.Headers["X-Page-Size"] = pageSize.ToString(CultureInfo.InvariantCulture);
    }

    private static bool IsDescending(string? sortDir)
    {
        return string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase)
               || string.Equals(sortDir, "descending", StringComparison.OrdinalIgnoreCase);
    }
}
