using System.Collections.Concurrent;
using InventoryHub.Api.Models;

namespace InventoryHub.Api.Data;

/// <summary>
/// Thread-safe in-memory store standing in for a database. A
/// ConcurrentDictionary is used (rather than a plain List) so concurrent
/// requests can read/write without an explicit lock, which matters once
/// the API is under real concurrent load.
/// </summary>
public class InventoryStore
{
    private readonly ConcurrentDictionary<int, InventoryItem> _items = new();
    private int _nextId = 1;

    public InventoryStore()
    {
        var categories = new[] { "Electronics", "Office Supplies", "Warehouse", "Furniture", "Tools" };
        var rand = new Random(42);

        for (int i = 0; i < 250; i++)
        {
            var id = _nextId++;
            _items[id] = new InventoryItem
            {
                Id = id,
                Sku = $"SKU-{id:D5}",
                Name = $"{categories[i % categories.Length]} Item {id}",
                Category = categories[i % categories.Length],
                QuantityOnHand = rand.Next(0, 500),
                UnitPrice = Math.Round((decimal)(rand.NextDouble() * 200 + 1), 2),
                LastRestockedUtc = DateTime.UtcNow.AddDays(-rand.Next(0, 90))
            };
        }
    }

    public IQueryable<InventoryItem> Query() => _items.Values.AsQueryable();

    public InventoryItem? GetById(int id) => _items.TryGetValue(id, out var item) ? item : null;

    public IReadOnlyCollection<string> GetCategories() =>
        _items.Values.Select(i => i.Category).Distinct().OrderBy(c => c).ToList();
}
