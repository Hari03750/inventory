/**
 * Thin API client for the InventoryHub backend.
 *
 * Integration issues this module specifically fixes (found while debugging
 * with Copilot — see INTEGRATION_SUMMARY.md):
 *
 *  1. Race condition on fast typing: every keystroke fired a fetch, and
 *     slower earlier responses could arrive AFTER faster later ones,
 *     overwriting fresh results with stale ones. Fixed with AbortController
 *     — each new call cancels the previous in-flight request.
 *  2. Silent failures on non-2xx responses: fetch() does not reject on
 *     HTTP error status codes, so a 404/500 was being rendered as if it
 *     were a successful empty response. Fixed by explicitly checking
 *     response.ok and throwing a normalized ApiClientError.
 *  3. Trailing-slash / base-URL mismatches between environments. Fixed by
 *     centralizing BASE_URL in one place instead of hardcoding it per call.
 */

const BASE_URL = window.INVENTORYHUB_API_BASE_URL || "https://localhost:5001";

class ApiClientError extends Error {
  constructor(message, status, code) {
    super(message);
    this.status = status;
    this.code = code;
  }
}

let itemsAbortController = null;

/**
 * Fetch a page of inventory items. Cancels any previous in-flight
 * request for this same call site so stale responses can never overwrite
 * fresher ones (fixes the race condition described above).
 */
async function fetchItems(params) {
  if (itemsAbortController) {
    itemsAbortController.abort();
  }
  itemsAbortController = new AbortController();

  const query = new URLSearchParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== "") {
      query.set(key, value);
    }
  });

  const url = `${BASE_URL}/api/items?${query.toString()}`;

  let response;
  try {
    response = await fetch(url, { signal: itemsAbortController.signal });
  } catch (err) {
    if (err.name === "AbortError") {
      // Expected when a newer request superseded this one; not a real error.
      throw err;
    }
    throw new ApiClientError("Network error contacting InventoryHub API.", 0, "NETWORK_ERROR");
  }

  return parseJsonOrThrow(response);
}

async function fetchCategories() {
  const response = await fetch(`${BASE_URL}/api/items/categories`);
  return parseJsonOrThrow(response);
}

async function parseJsonOrThrow(response) {
  let body = null;
  try {
    body = await response.json();
  } catch {
    // Body wasn't JSON (e.g. an HTML error page slipped through) — surfaced
    // as a clear error instead of crashing the renderer downstream.
  }

  if (!response.ok) {
    const code = body?.code ?? "UNKNOWN_ERROR";
    const message = body?.message ?? `Request failed with status ${response.status}.`;
    throw new ApiClientError(message, response.status, code);
  }

  return body;
}

window.InventoryHubApi = { fetchItems, fetchCategories, ApiClientError };
