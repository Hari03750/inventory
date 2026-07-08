namespace InventoryHub.Api.Models;

/// <summary>
/// The shape returned to the front end for a single item. Deliberately a
/// separate type from the storage model so we control exactly what crosses
/// the wire (smaller payload, camelCase JSON, no accidental leakage of
/// internal-only fields if the storage model grows later).
/// </summary>
public record InventoryItemDto(
    int Id,
    string Sku,
    string Name,
    string Category,
    int QuantityOnHand,
    decimal UnitPrice,
    DateTime LastRestockedUtc)
{
    public static InventoryItemDto FromModel(InventoryItem item) => new(
        item.Id, item.Sku, item.Name, item.Category,
        item.QuantityOnHand, item.UnitPrice, item.LastRestockedUtc);
}

/// <summary>
/// A consistent "envelope" shape used by every list endpoint: the data
/// array plus a metadata block describing pagination. Keeping this
/// structure uniform across endpoints is what let the front end write one
/// generic fetch/render helper instead of one-off parsing per endpoint.
/// </summary>
public record PagedResponse<T>(
    IReadOnlyList<T> Data,
    PageMeta Meta);

public record PageMeta(
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

/// <summary>
/// Uniform error shape so the front end can branch on `error.code` instead
/// of parsing free-text messages.
/// </summary>
public record ApiError(string Code, string Message);
