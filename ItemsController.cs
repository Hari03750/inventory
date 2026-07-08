using InventoryHub.Api.Data;
using InventoryHub.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace InventoryHub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ItemsController : ControllerBase
{
    private readonly InventoryStore _store;
    private readonly IMemoryCache _cache;
    private const int MaxPageSize = 100;

    public ItemsController(InventoryStore store, IMemoryCache cache)
    {
        _store = store;
        _cache = cache;
    }

    /// <summary>
    /// Paged, filterable, sortable item list. Query params:
    /// search, category, sortBy (name|price|quantity), sortDir (asc|desc), page, pageSize.
    ///
    /// PERFORMANCE NOTES (this is the piece Copilot helped optimize):
    ///  - Filtering happens on IQueryable before pagination, so we only ever
    ///    materialize the one page of rows actually being returned, not the
    ///    whole 250+ item table.
    ///  - pageSize is capped at MaxPageSize to prevent a client from
    ///    requesting an unbounded payload.
    ///  - Response caching headers let the browser/CDN skip a round trip
    ///    entirely for identical repeated requests within the cache window.
    /// </summary>
    [HttpGet]
    [ResponseCache(Duration = 30, Location = ResponseCacheLocation.Client, VaryByQueryKeys = new[] { "search", "category", "sortBy", "sortDir", "page", "pageSize" })]
    public ActionResult<PagedResponse<InventoryItemDto>> GetItems(
        [FromQuery] string? search,
        [FromQuery] string? category,
        [FromQuery] string sortBy = "name",
        [FromQuery] string sortDir = "asc",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = _store.Query();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(i =>
                i.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                i.Sku.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(i => i.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        }

        query = (sortBy.ToLowerInvariant(), sortDir.ToLowerInvariant()) switch
        {
            ("price", "desc") => query.OrderByDescending(i => i.UnitPrice),
            ("price", _) => query.OrderBy(i => i.UnitPrice),
            ("quantity", "desc") => query.OrderByDescending(i => i.QuantityOnHand),
            ("quantity", _) => query.OrderBy(i => i.QuantityOnHand),
            (_, "desc") => query.OrderByDescending(i => i.Name),
            _ => query.OrderBy(i => i.Name)
        };

        var totalCount = query.Count();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var pageItems = query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(InventoryItemDto.FromModel)
            .ToList();

        var response = new PagedResponse<InventoryItemDto>(
            pageItems,
            new PageMeta(page, pageSize, totalCount, totalPages));

        return Ok(response);
    }

    [HttpGet("{id:int}")]
    public ActionResult<InventoryItemDto> GetById(int id)
    {
        var item = _store.GetById(id);
        if (item is null)
        {
            return NotFound(new ApiError("ITEM_NOT_FOUND", $"No item with id {id}."));
        }

        return Ok(InventoryItemDto.FromModel(item));
    }

    /// <summary>
    /// Categories change rarely, so they're cached server-side for 5 minutes
    /// instead of being recomputed (a Distinct() scan) on every request.
    /// </summary>
    [HttpGet("categories")]
    public ActionResult<IReadOnlyCollection<string>> GetCategories()
    {
        var categories = _cache.GetOrCreate("categories", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            return _store.GetCategories();
        });

        return Ok(categories);
    }
}
