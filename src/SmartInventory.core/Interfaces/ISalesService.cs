using SmartInventory.Core.DTOs;

namespace SmartInventory.Core.Interfaces;

public interface ISalesService
{
    /// <summary>
    /// Sells the cart as ONE all-or-nothing unit: either every line is sold, stock is decremented,
    /// ledger rows are written and low-stock alerts are raised — or nothing changes at all.
    /// </summary>
    /// <param name="cashier">Display name recorded in the ledger notes (the ledger has no user column).</param>
    Task<CheckoutResultDto> CheckoutAsync(CheckoutRequestDto request, string cashier, CancellationToken ct = default);
}
