/**
 * POST the cart to /Sales/Checkout.
 *
 * Resolves (never throws) to:
 *   { ok, status, data, networkError, sessionExpired }
 * so the caller has a single place to branch on the outcome.
 */
export async function postCheckout(url, antiforgeryToken, items, saleId) {
  let response;
  try {
    response = await fetch(url, {
      method: 'POST',
      credentials: 'same-origin',
      headers: {
        'Content-Type': 'application/json',
        Accept: 'application/json',
        // ASP.NET Core's default antiforgery header name.
        RequestVerificationToken: antiforgeryToken,
      },
      body: JSON.stringify({ items, saleId }),
    });
  } catch {
    return { ok: false, status: 0, data: null, networkError: true, sessionExpired: false };
  }

  // An expired login cookie makes the server redirect to the sign-in page (HTML, status 200).
  const sessionExpired = response.redirected && /\/Account\/Login/i.test(response.url);

  let data = null;
  try {
    data = await response.json();
  } catch {
    /* body was not JSON */
  }

  return { ok: response.ok && !sessionExpired, status: response.status, data, networkError: false, sessionExpired };
}
