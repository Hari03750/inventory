# InventoryHub

A small full-stack app built as the capstone for the *Full-Stack
Integration* course: an ASP.NET Core Web API backend and a vanilla
JS/HTML/CSS front end, focused on clean front-end/back-end communication,
well-structured JSON responses, resolved integration issues, and
performance optimizations.

## Structure

```
backend/
  Program.cs                CORS, response compression/caching, global JSON error handler
  Controllers/ItemsController.cs   Paginated/filterable/sortable items API + cached categories endpoint
  Models/Dtos.cs             InventoryItemDto, PagedResponse<T>, PageMeta, ApiError (structured JSON shapes)
  Models/InventoryItem.cs    Internal storage model
  Data/InventoryStore.cs     Thread-safe in-memory seeded data
frontend/
  index.html                 Search/filter/sort UI, results grid, pagination
  css/styles.css
  js/api.js                  Fetch wrapper: AbortController, error normalization
  js/app.js                  Debounced search, state management, rendering
INTEGRATION_SUMMARY.md       Reflective summary: how Copilot helped at each stage
```

## Running it locally

**Backend:**
```bash
cd backend
dotnet restore
dotnet run
```
The API listens on `https://localhost:5001` (adjust `frontend/js/api.js`'s
`BASE_URL` fallback, or set `window.INVENTORYHUB_API_BASE_URL`, if your port differs).

**Frontend:**
Serve the `frontend/` folder with any static file server, e.g.:
```bash
cd frontend
python3 -m http.server 5500
```
Then open `http://localhost:5500`. (`Program.cs`'s CORS policy already
allows `localhost:5500`/`127.0.0.1:5500`/`localhost:3000` — add your port
if you use a different one.)

## API shape

`GET /api/items?search=&category=&sortBy=name&sortDir=asc&page=1&pageSize=20`

```json
{
  "data": [
    {
      "id": 1,
      "sku": "SKU-00001",
      "name": "Electronics Item 1",
      "category": "Electronics",
      "quantityOnHand": 42,
      "unitPrice": 19.99,
      "lastRestockedUtc": "2026-06-01T00:00:00Z"
    }
  ],
  "meta": { "page": 1, "pageSize": 20, "totalCount": 250, "totalPages": 13 }
}
```

Errors are always `{ "code": "...", "message": "..." }`, whether they come
from a handled `NotFound()` or the global exception handler.

## Key integration & performance points

- **CORS** configured explicitly for the front end's origin(s) — the first
  integration bug hit and fixed (see `INTEGRATION_SUMMARY.md`).
- **camelCase JSON** on the wire, matching what the front end expects,
  instead of the ASP.NET default PascalCase.
- **Consistent envelope shape** (`data` + `meta`) across list endpoints so
  the front end has one generic parsing/rendering path.
- **AbortController-based cancellation** in `api.js` prevents a race
  condition where a slow, stale response could overwrite a fresher one.
- **Debounced search input** (300ms) cuts unnecessary API calls while typing.
- **Server-side**: pagination caps materialized rows per request, response
  compression (gzip) shrinks payload size, categories are cached in memory
  for 5 minutes, and `ResponseCache` headers let the browser skip repeat
  round trips for identical queries.
