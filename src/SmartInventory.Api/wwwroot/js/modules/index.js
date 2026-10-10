import { qs, qsa } from '../dom.js';
import { showToast } from '../toast.js';
import { createCart } from './cart.js';
import { createMoney, toCents } from './format.js';
import { postCheckout } from './checkout.js';

/**
 * Point-of-sale screen (Views/Sales/Index.cshtml).
 * Finds its markup only through data-pos-* hooks; does nothing on other pages.
 */
export function initPos() {
  const root = qs('[data-pos]');
  if (!root) return;

  const money = createMoney(root.dataset.currency);
  const cart = createCart(root.dataset.storageKey);
  const $ = (name) => qs(`[data-pos-${name}]`, root);

  const el = {
    search: $('search'),
    grid: $('grid'),
    empty: $('empty'),
    count: $('count'),
    form: $('checkout'),
    lines: $('lines'),
    cartEmpty: $('cart-empty'),
    total: $('total'),
    submit: $('submit'),
    clear: $('clear'),
    errors: $('errors'),
    errorList: $('error-list'),
    status: $('status'),
    bar: $('bar'),
    barCount: $('bar-count'),
    barTotal: $('bar-total'),
    receipt: $('receipt'),
    receiptTitle: $('receipt-title'),
    receiptNumber: $('receipt-number'),
    receiptLines: $('receipt-lines'),
    receiptTotal: $('receipt-total'),
    newSale: $('new-sale'),
  };
  const lineTemplate = qs('[data-pos-line-template]');
  const receiptLineTemplate = qs('[data-pos-receipt-line-template]');
  const submitLabel = el.submit.textContent.trim();

  const tiles = new Map(qsa('[data-pos-product]', root).map((tile) => [tile.dataset.id, tile]));
  const lineEls = new Map();
  let pendingSaleId = null; // idempotency key; survives retries after a network failure
  let busy = false;

  // ---------------------------------------------------------------- helpers
  const stockOf = (id) => Number(tiles.get(id)?.dataset.stock ?? 0);
  const priceOf = (id) => toCents(tiles.get(id)?.dataset.price ?? 0);
  const announce = (message) => {
    el.status.textContent = '';
    // Re-set on the next frame so screen readers announce repeated identical messages.
    requestAnimationFrame(() => (el.status.textContent = message));
  };

  function paintStock(tile, stock, low = qs('[data-pos-stock-badge]', tile).classList.contains('badge--warning')) {
    const badge = qs('[data-pos-stock-badge]', tile);
    tile.dataset.stock = String(stock);
    tile.disabled = stock <= 0;
    badge.className = stock <= 0 ? 'badge badge--danger' : low ? 'badge badge--warning' : 'badge';
    badge.textContent = stock <= 0 ? 'Out of stock' : `${stock.toLocaleString()} left`;
  }

  // ---------------------------------------------------------------- catalogue
  qsa('[data-pos-price]', root).forEach((node) => {
    node.textContent = money(toCents(node.closest('[data-pos-product]').dataset.price));
  });

  function filterTiles() {
    const term = el.search.value.trim().toLowerCase();
    let visible = 0;
    for (const tile of tiles.values()) {
      const match = !term || tile.dataset.search.includes(term);
      tile.hidden = !match;
      if (match) visible += 1;
    }
    el.empty.hidden = visible > 0 || tiles.size === 0;
    el.count.textContent = term
      ? `${visible.toLocaleString()} of ${tiles.size.toLocaleString()} products`
      : `${tiles.size.toLocaleString()} products`;
  }

  function addToCart(tile) {
    const id = tile.dataset.id;
    const stock = stockOf(id);
    const { qty, capped } = cart.add(id, stock);
    tile.classList.remove('is-added');
    void tile.offsetWidth; // restart the CSS animation
    tile.classList.add('is-added');
    if (capped) {
      announce(`Only ${stock} of ${tile.dataset.name} in stock.`);
      showToast(`Only ${stock} of ${tile.dataset.name} in stock.`, 'error');
    } else {
      announce(`${tile.dataset.name} added. Quantity ${qty}.`);
    }
  }

  // ---------------------------------------------------------------- cart rendering
  function buildLine(id) {
    const li = lineTemplate.content.firstElementChild.cloneNode(true);
    li.dataset.id = id;
    qs('[data-line-name]', li).textContent = tiles.get(id).dataset.name;
    qs('[data-line-unit]', li).textContent = money(priceOf(id));
    return li;
  }

  function paintLine(li, id, qty) {
    const input = qs('[data-line-qty]', li);
    const stock = stockOf(id);
    if (document.activeElement !== input) input.value = String(qty);
    input.max = String(stock);
    qs('[data-line-total]', li).textContent = money(priceOf(id) * qty);
    qs('[data-action="inc"]', li).disabled = qty >= stock;
  }

  function render() {
    const entries = cart.entries().filter(([id]) => tiles.has(id));
    const seen = new Set();

    for (const [id, qty] of entries) {
      seen.add(id);
      let li = lineEls.get(id);
      if (!li) {
        li = buildLine(id);
        lineEls.set(id, li);
        el.lines.append(li);
      }
      paintLine(li, id, qty);
    }
    for (const [id, li] of lineEls) {
      if (!seen.has(id)) {
        li.remove();
        lineEls.delete(id);
      }
    }

    const itemCount = entries.reduce((sum, [, qty]) => sum + qty, 0);
    const totalCents = entries.reduce((sum, [id, qty]) => sum + priceOf(id) * qty, 0);
    const hasItems = entries.length > 0;

    el.lines.hidden = !hasItems;
    el.cartEmpty.hidden = hasItems;
    el.total.textContent = hasItems ? money(totalCents) : '—';
    el.clear.disabled = !hasItems || busy;
    el.submit.disabled = !hasItems || busy;
    el.bar.hidden = !hasItems;
    el.barCount.textContent = String(itemCount);
    el.barTotal.textContent = money(totalCents);
  }

  // ---------------------------------------------------------------- checkout
  function setBusy(value) {
    busy = value;
    el.submit.classList.toggle('is-loading', value);
    el.submit.toggleAttribute('aria-busy', value);
    el.submit.textContent = value ? el.submit.dataset.loadingLabel : submitLabel;
    render();
  }

  function showErrors(messages) {
    el.errorList.replaceChildren(
      ...messages.map((message) => Object.assign(document.createElement('li'), { textContent: message })),
    );
    el.errors.hidden = messages.length === 0;
  }

  function showReceipt(data) {
    el.receiptNumber.textContent = data.receiptNumber;
    el.receiptTotal.textContent = money(toCents(data.grandTotal));
    el.receiptLines.replaceChildren(
      ...data.lines.map((line) => {
        const li = receiptLineTemplate.content.firstElementChild.cloneNode(true);
        qs('[data-line-name]', li).textContent = line.name;
        qs('[data-line-qty]', li).textContent = String(line.quantity);
        qs('[data-line-unit]', li).textContent = money(toCents(line.unitPrice));
        qs('[data-line-total]', li).textContent = money(toCents(line.lineTotal));
        return li;
      }),
    );
    el.form.hidden = true;
    el.receipt.hidden = false;
    el.receiptTitle.focus();
  }

  function completeSale(data) {
    for (const line of data.lines) {
      const tile = tiles.get(line.productId);
      if (tile) paintStock(tile, line.remainingStock, line.isLowStock);
    }
    cart.clear();
    el.search.value = '';
    filterTiles();
    showReceipt(data);
    announce(`Sale complete. Receipt ${data.receiptNumber}.`);
    showToast(`Sale ${data.receiptNumber} completed.`, 'success');
  }

  function failSale({ data, networkError, sessionExpired }) {
    const errors = data?.errors ?? [];

    // Already recorded (a retry of a sale whose reply we lost): don't double-sell, just refresh.
    if (errors.some((e) => e.code === 'duplicate')) {
      cart.clear();
      showToast(errors[0].message, 'success');
      setTimeout(() => window.location.reload(), 1200);
      return;
    }

    for (const error of errors) {
      const tile = error.productId ? tiles.get(error.productId) : null;
      if (!tile) continue;
      if (error.code === 'not_found') {
        cart.remove(error.productId);
        tiles.delete(error.productId);
        tile.remove();
      } else if (error.available != null) {
        paintStock(tile, error.available);
      }
    }
    cart.reconcile(stockOf); // clamp quantities to what is actually left

    const messages = errors.length
      ? errors.map((e) => e.message)
      : [
          sessionExpired
            ? 'Your session expired. Reload the page and sign in again — your cart is kept.'
            : networkError
              ? 'No connection. The sale was NOT completed — check the network and press Complete sale again.'
              : 'Something went wrong. The sale was not completed.',
        ];
    showErrors(messages);
    announce(`Sale not completed. ${messages.join(' ')}`);
    showToast('Sale not completed.', 'error');
  }

  el.form.addEventListener('submit', async (event) => {
    event.preventDefault();
    if (busy || cart.size === 0) return;

    showErrors([]);
    setBusy(true);

    // Reuse the same id for a retry after a lost reply; the server rejects a repeat of an id it already saved.
    pendingSaleId ??= crypto.randomUUID();
    const items = cart.entries().map(([productId, quantity]) => ({ productId, quantity }));
    const token = qs('input[name="__RequestVerificationToken"]', el.form)?.value ?? '';

    const result = await postCheckout(el.form.action, token, items, pendingSaleId);
    setBusy(false);

    if (result.ok && result.data?.success) {
      pendingSaleId = null;
      completeSale(result.data);
      return;
    }
    // A definite answer from the server (4xx) ends this attempt; a lost reply (network/5xx) keeps the id.
    if (result.status >= 400 && result.status < 500) pendingSaleId = null;
    failSale(result);
  });

  // ---------------------------------------------------------------- events
  el.grid.addEventListener('click', (event) => {
    const tile = event.target.closest('[data-pos-product]');
    if (tile && !tile.disabled) addToCart(tile);
  });

  el.search.addEventListener('input', filterTiles);
  el.search.addEventListener('keydown', (event) => {
    if (event.key === 'Escape') {
      el.search.value = '';
      filterTiles();
      return;
    }
    if (event.key !== 'Enter') return;
    event.preventDefault();

    // Barcode scanners "type" the code and press Enter: an exact SKU wins, else a single visible match.
    const term = el.search.value.trim().toLowerCase();
    if (!term) return;
    const available = [...tiles.values()].filter((tile) => !tile.hidden && !tile.disabled);
    const exact = [...tiles.values()].find((tile) => tile.dataset.sku.toLowerCase() === term);
    const chosen = exact && !exact.disabled ? exact : exact ? null : available.length === 1 ? available[0] : null;

    if (chosen) {
      addToCart(chosen);
      el.search.value = '';
      filterTiles();
    } else if (exact) {
      announce(`${exact.dataset.name} is out of stock.`);
      showToast(`${exact.dataset.name} is out of stock.`, 'error');
    } else if (available.length === 0) {
      announce(`No product found for ${el.search.value.trim()}.`);
      showToast(`No product found for “${el.search.value.trim()}”.`, 'error');
    } else {
      announce(`${available.length} matches. Refine the search or tap a product.`);
    }
  });

  el.lines.addEventListener('click', (event) => {
    const button = event.target.closest('[data-action]');
    const li = event.target.closest('[data-pos-line]');
    if (!button || !li) return;
    const id = li.dataset.id;

    if (button.dataset.action === 'remove') {
      cart.remove(id);
      announce('Item removed.');
      el.search.focus(); // the focused button just disappeared
      return;
    }
    const delta = button.dataset.action === 'inc' ? 1 : -1;
    const { qty, capped } = cart.set(id, cart.qty(id) + delta, stockOf(id));
    if (qty === 0) el.search.focus();
    if (capped) announce(`Only ${stockOf(id)} in stock.`);
  });

  el.lines.addEventListener('change', (event) => {
    const input = event.target.closest('[data-line-qty]');
    if (!input) return;
    const id = event.target.closest('[data-pos-line]').dataset.id;
    const value = Number(input.value);

    if (!Number.isFinite(value) || input.value.trim() === '') {
      render(); // not a number: restore the previous quantity
      return;
    }
    const { capped } = cart.set(id, value, stockOf(id));
    if (capped) announce(`Only ${stockOf(id)} in stock.`);
    render();
  });

  // Enter inside a quantity box must NOT submit the form (that would complete the sale by accident).
  el.lines.addEventListener('keydown', (event) => {
    if (event.key === 'Enter' && event.target.matches('[data-line-qty]')) {
      event.preventDefault();
      event.target.blur();
      el.search.focus();
    }
  });

  el.clear.addEventListener('click', () => {
    cart.clear();
    showErrors([]);
    announce('Sale cleared.');
    el.search.focus();
  });

  el.newSale.addEventListener('click', () => {
    el.receipt.hidden = true;
    el.form.hidden = false;
    showErrors([]);
    el.search.focus();
  });

  // ---------------------------------------------------------------- start
  cart.onChange(render);
  cart.reconcile(stockOf); // a saved cart may predate a stock change
  filterTiles();
  render();
}
