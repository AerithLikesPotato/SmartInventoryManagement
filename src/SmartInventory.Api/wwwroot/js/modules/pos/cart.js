/**
 * Cart state, with no knowledge of the DOM: it only stores productId -> quantity.
 * Names and prices are looked up by the UI; the server re-prices every line at checkout anyway.
 *
 * The cart survives a page refresh (sessionStorage), so a cashier never loses a half-built sale.
 */
export function createCart(storageKey) {
  /** @type {Map<string, number>} */
  let lines = new Map();
  const listeners = new Set();

  try {
    const saved = JSON.parse(sessionStorage.getItem(storageKey) ?? '[]');
    if (Array.isArray(saved)) {
      lines = new Map(saved.filter(([id, qty]) => typeof id === 'string' && Number.isInteger(qty) && qty > 0));
    }
  } catch {
    /* corrupt or blocked storage: start empty */
  }

  const emit = () => {
    try {
      sessionStorage.setItem(storageKey, JSON.stringify([...lines]));
    } catch {
      /* ignore */
    }
    listeners.forEach((fn) => fn());
  };

  const qty = (id) => lines.get(id) ?? 0;

  /** Sets a quantity, never above `max` (units in stock). 0 or less removes the line. */
  const set = (id, wanted, max) => {
    const target = Math.floor(wanted);
    const next = Math.min(target, max);
    if (next > 0) lines.set(id, next);
    else lines.delete(id);
    emit();
    return { qty: Math.max(0, next), capped: target > max };
  };

  return {
    qty,
    set,
    add: (id, max) => set(id, qty(id) + 1, max),
    remove(id) {
      if (lines.delete(id)) emit();
    },
    clear() {
      if (lines.size === 0) return;
      lines.clear();
      emit();
    },
    get size() {
      return lines.size;
    },
    entries: () => [...lines],
    onChange: (fn) => listeners.add(fn),

    /** After stock changed: drop lines that are gone or sold out, clamp the rest. maxFor(id) -> units available. */
    reconcile(maxFor) {
      let changed = false;
      for (const [id, q] of lines) {
        const max = maxFor(id);
        if (max <= 0) {
          lines.delete(id);
          changed = true;
        } else if (q > max) {
          lines.set(id, max);
          changed = true;
        }
      }
      if (changed) emit();
      return changed;
    },
  };
}
