(function () {
  const { fetchItems, fetchCategories, ApiClientError } = window.InventoryHubApi;

  const searchInput = document.getElementById("searchInput");
  const categoryFilter = document.getElementById("categoryFilter");
  const sortBySelect = document.getElementById("sortBy");
  const sortDirSelect = document.getElementById("sortDir");
  const statusRegion = document.getElementById("statusRegion");
  const resultsGrid = document.getElementById("resultsGrid");
  const pagination = document.getElementById("pagination");

  const state = {
    search: "",
    category: "",
    sortBy: "name",
    sortDir: "asc",
    page: 1,
    pageSize: 20
  };

  // Debounce: avoids firing an API call on every single keystroke, which
  // both reduces load on the backend and avoids the flicker of many
  // rapid, superseded renders. 300ms is a common sweet spot for search-as-you-type.
  function debounce(fn, delayMs) {
    let timer;
    return (...args) => {
      clearTimeout(timer);
      timer = setTimeout(() => fn(...args), delayMs);
    };
  }

  function setStatus(message, isError) {
    statusRegion.textContent = message;
    statusRegion.classList.toggle("error", Boolean(isError));
  }

  function renderItems(items) {
    resultsGrid.innerHTML = "";

    if (items.length === 0) {
      resultsGrid.innerHTML = `<p>No items match your filters.</p>`;
      return;
    }

    const fragment = document.createDocumentFragment();

    for (const item of items) {
      const card = document.createElement("article");
      card.className = "item-card";

      const lowStock = item.quantityOnHand < 10;

      card.innerHTML = `
        <h3>${escapeHtml(item.name)}</h3>
        <div class="sku">${escapeHtml(item.sku)} &middot; ${escapeHtml(item.category)}</div>
        <div class="row"><span>Unit price</span><span>$${item.unitPrice.toFixed(2)}</span></div>
        <div class="row">
          <span>On hand</span>
          <span class="${lowStock ? "low-stock" : ""}">${item.quantityOnHand}${lowStock ? " (low)" : ""}</span>
        </div>
      `;
      fragment.appendChild(card);
    }

    resultsGrid.appendChild(fragment);
  }

  function renderPagination(meta) {
    pagination.innerHTML = "";

    const prevBtn = document.createElement("button");
    prevBtn.textContent = "Prev";
    prevBtn.disabled = meta.page <= 1;
    prevBtn.addEventListener("click", () => goToPage(meta.page - 1));
    pagination.appendChild(prevBtn);

    const label = document.createElement("span");
    label.textContent = ` Page ${meta.page} of ${Math.max(meta.totalPages, 1)} (${meta.totalCount} items) `;
    label.style.alignSelf = "center";
    pagination.appendChild(label);

    const nextBtn = document.createElement("button");
    nextBtn.textContent = "Next";
    nextBtn.disabled = meta.page >= meta.totalPages;
    nextBtn.addEventListener("click", () => goToPage(meta.page + 1));
    pagination.appendChild(nextBtn);
  }

  function goToPage(page) {
    state.page = page;
    loadItems();
  }

  function escapeHtml(value) {
    const div = document.createElement("div");
    div.textContent = value;
    return div.innerHTML;
  }

  async function loadItems() {
    setStatus("Loading...", false);

    try {
      const response = await fetchItems({
        search: state.search,
        category: state.category,
        sortBy: state.sortBy,
        sortDir: state.sortDir,
        page: state.page,
        pageSize: state.pageSize
      });

      renderItems(response.data);
      renderPagination(response.meta);
      setStatus(`${response.meta.totalCount} item(s) found.`, false);
    } catch (err) {
      if (err.name === "AbortError") {
        // A newer request is already in flight; nothing to render or report.
        return;
      }

      if (err instanceof ApiClientError) {
        setStatus(`Error (${err.code}): ${err.message}`, true);
      } else {
        setStatus("Unexpected error loading inventory.", true);
      }
      resultsGrid.innerHTML = "";
      pagination.innerHTML = "";
    }
  }

  async function loadCategories() {
    try {
      const categories = await fetchCategories();
      for (const category of categories) {
        const option = document.createElement("option");
        option.value = category;
        option.textContent = category;
        categoryFilter.appendChild(option);
      }
    } catch {
      // Non-fatal: the item list still works without the category filter populated.
    }
  }

  const debouncedSearch = debounce(() => {
    state.search = searchInput.value.trim();
    state.page = 1;
    loadItems();
  }, 300);

  searchInput.addEventListener("input", debouncedSearch);

  categoryFilter.addEventListener("change", () => {
    state.category = categoryFilter.value;
    state.page = 1;
    loadItems();
  });

  sortBySelect.addEventListener("change", () => {
    state.sortBy = sortBySelect.value;
    loadItems();
  });

  sortDirSelect.addEventListener("change", () => {
    state.sortDir = sortDirSelect.value;
    loadItems();
  });

  loadCategories();
  loadItems();
})();
