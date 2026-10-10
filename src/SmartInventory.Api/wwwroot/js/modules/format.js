/** Money helpers. All arithmetic is done in integer cents to avoid floating-point drift (0.1 + 0.2). */

export const toCents = (price) => Math.round(Number(price) * 100);

/** @returns {(cents: number) => string} formatter for an ISO currency code, falling back to plain 2-decimals. */
export function createMoney(currency) {
  let formatter;
  try {
    formatter = new Intl.NumberFormat(undefined, { style: 'currency', currency });
  } catch {
    formatter = new Intl.NumberFormat(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  }
  return (cents) => formatter.format(cents / 100);
}
