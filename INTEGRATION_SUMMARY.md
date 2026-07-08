# InventoryHub — Integration & Performance Summary

## 1. Front-end/back-end integration code

The backend exposes a REST API (`ItemsController`) and the front end
(`js/api.js` + `js/app.js`) talks to it with `fetch`. Getting this
communicating cleanly meant:

- Defining one consistent JSON envelope (`data` + `meta`) for every list
  endpoint so the front end could write a single generic render path
  instead of custom parsing per endpoint.
- Centralizing the API base URL and all `fetch` calls in one module
  (`api.js`) rather than scattering URLs across the UI code.
- Wiring query-string parameters (search/category/sort/page) on the front
  end to match exactly what the controller's `[FromQuery]` parameters
  expect.

## 2. Integration issues found and fixed

| Issue | Symptom | Fix |
|---|---|---|
| **Missing CORS policy** | Browser blocked every `fetch()` call with a CORS error even though the API worked fine from Swagger/Postman. | Added an explicit `AddCors` policy in `Program.cs` naming the front end's origin(s) and applied it with `UseCors` before `MapControllers`. |
| **Casing mismatch** | Front end read `item.name` but the API (default ASP.NET behavior) was sending `Name`, so every field showed `undefined`. | Set `JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase` explicitly in `Program.cs`. |
| **Silent failures on errors** | A 404/500 from the API rendered as an empty list instead of an error message, because `fetch()` doesn't throw on non-2xx status. | `api.js`'s `parseJsonOrThrow` explicitly checks `response.ok` and throws a normalized `ApiClientError` that the UI can branch on. |
| **Race condition on fast typing** | Typing quickly in the search box sometimes showed stale results, because an earlier (slower) request could resolve after a later (faster) one. | Added `AbortController` in `fetchItems` — each new request cancels the previous in-flight one. |
| **Unhandled exceptions returning HTML** | An unexpected server error returned ASP.NET's HTML developer-exception page, which broke the front end's JSON parsing entirely. | Added a global `UseExceptionHandler` in `Program.cs` that always responds with the same structured `ApiError` JSON shape. |

## 3. JSON structures implemented

- `InventoryItemDto` — the wire shape for a single item, decoupled from the
  internal storage model.
- `PagedResponse<T>` / `PageMeta` — the reusable pagination envelope
  (`data`, plus `page`/`pageSize`/`totalCount`/`totalPages`) used by every
  list endpoint.
- `ApiError` — a uniform `{ code, message }` shape returned for every error
  path (validation, not-found, and unhandled exceptions alike), so the
  front end can branch on `error.code` instead of parsing free-text
  messages.

## 4. Performance optimizations

- **Server-side pagination** — filtering happens on `IQueryable` before
  `Skip`/`Take`, so only the requested page of rows is ever materialized,
  not the full dataset.
- **`pageSize` capped** at 100 to prevent an unbounded payload from a
  malicious or buggy client request.
- **Response compression (gzip)** — enabled via
  `AddResponseCompression`/`UseResponseCompression`, shrinking the JSON
  payload substantially over the wire.
- **Server-side memory caching** — the categories list (which changes
  rarely) is cached for 5 minutes with `IMemoryCache` instead of being
  recomputed via a `Distinct()` scan on every request.
- **HTTP response caching headers** — `[ResponseCache]` on the list
  endpoint, varied by the actual query parameters, lets the browser skip a
  network round trip entirely for an identical repeated query.
- **Debounced search (300ms)** and **request cancellation** on the front
  end together mean a fast typist triggers far fewer requests, and none of
  the wasted ones are ever rendered.

## 5. How Copilot assisted at each step

- **Generating integration code**: Copilot scaffolded the first version of
  `fetchItems`/`fetchCategories` in `api.js` and the `ItemsController`
  query/filter/sort logic, which was then adjusted to match the actual
  query-parameter names and response shape.
- **Debugging integration issues**: When the front end showed `undefined`
  fields and blank results, Copilot's explanation of ASP.NET's default
  PascalCase JSON serialization and the missing CORS policy pointed
  directly at the fix; it also suggested the `AbortController` pattern
  once the stale-response race condition was described to it.
- **JSON structuring**: Copilot proposed the `PagedResponse<T>`/`PageMeta`
  envelope pattern and the uniform `ApiError` shape, which made error
  handling on the front end (`err instanceof ApiClientError`) much
  simpler than ad hoc per-endpoint error formats.
- **Performance optimization**: Copilot suggested adding response
  compression, `IMemoryCache` for the categories endpoint, and capping
  `pageSize`; each suggestion was verified by checking response sizes and
  confirming the pagination logic still returned correct `totalCount`/
  `totalPages` values after the change.
