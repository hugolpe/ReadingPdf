/* PSEUDOCODE / PLAN (detailed)
1. On DOMContentLoaded:
   - Locate the account <select> element by id "CuentaQB" or by name "CuentaQB".
   - If not present, do nothing.

2. Read existing cookie "qb_selected_account":
   - If cookie exists and an option for that value is present in the <select>, set the select's value to the cookie value.
   - If cookie exists but no matching option, leave the select as-is.

3. Attach change event listener to the <select>:
   - On change, get the selected value.
   - Send a POST to "/api/quickbooks/select-account" with JSON body: { AccountFullName: "<selected value>" }.
   - Use fetch with credentials: 'same-origin' to allow the server to set the cookie in the response.
   - Handle success and failure:
     - On success (2xx): update client-side cookie immediately for instant UI sync (expiry 30 days).
     - Optionally dispatch a custom event 'qb:accountSelected' with detail { account: value, ok: true }.
     - On failure: dispatch 'qb:accountSelected' with ok: false and log error.

4. Provide small utility functions:
   - getCookie(name)
   - setCookie(name, value, days)
   - safeSetSelectValue(select, value) to only set if option exists.

5. Expose a helper on window (window.quickbooksSelectAccount) to programmatically trigger selection and registration.

This file is safe to include via a <script src="/js/quickbooks-account.js" defer></script> in the Razor page.
*/

/* Implementation */
(function () {
  const COOKIE_NAME = 'qb_selected_account';
  const API_ENDPOINT = '/api/quickbooks/select-account';

  function getCookie(name) {
    const pairs = document.cookie ? document.cookie.split('; ') : [];
    for (let i = 0; i < pairs.length; i++) {
      const idx = pairs[i].indexOf('=');
      if (idx === -1) continue;
      const key = decodeURIComponent(pairs[i].substring(0, idx));
      const val = decodeURIComponent(pairs[i].substring(idx + 1));
      if (key === name) return val;
    }
    return null;
  }

  function setCookie(name, value, days) {
    const expires = days ? '; expires=' + new Date(Date.now() + days * 864e5).toUTCString() : '';
    // Path=/ so it's available site-wide; HttpOnly cannot be set from JS (server may set it)
    document.cookie = encodeURIComponent(name) + '=' + encodeURIComponent(value) + expires + '; path=/';
  }

  function safeSetSelectValue(select, value) {
    if (!select) return false;
    for (let i = 0; i < select.options.length; i++) {
      if (select.options[i].value === value) {
        select.value = value;
        return true;
      }
    }
    return false;
  }

  async function postSelectedAccount(accountFullName) {
    if (!accountFullName) return { ok: false, message: 'empty account' };

    try {
      const res = await fetch(API_ENDPOINT, {
        method: 'POST',
        credentials: 'same-origin', // allow receiving cookies from same origin
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ AccountFullName: accountFullName })
      });

      const payloadText = await res.text();
      let payload = null;
      try { payload = payloadText ? JSON.parse(payloadText) : null; } catch { payload = payloadText; }

      if (res.ok) {
        // Mirror cookie locally for immediate UI consistency (server also sets cookie)
        setCookie(COOKIE_NAME, accountFullName, 30);
        return { ok: true, payload };
      } else {
        return { ok: false, status: res.status, payload };
      }
    } catch (err) {
      return { ok: false, error: err.message || String(err) };
    }
  }

  function dispatchEvent(detail) {
    try {
      const ev = new CustomEvent('qb:accountSelected', { detail });
      window.dispatchEvent(ev);
    } catch (e) {
      // ignore if CustomEvent not supported (very old browsers)
    }
  }

  function init() {
    const select = document.getElementById('CuentaQB') || document.querySelector('select[name="CuentaQB"]');
    if (!select) return;

    // Initialize from cookie if matches an option
    const cookieVal = getCookie(COOKIE_NAME);
    if (cookieVal) {
      safeSetSelectValue(select, cookieVal);
    }

    // Listen for user changes
    select.addEventListener('change', async function (e) {
      const val = (e.target && e.target.value) ? e.target.value : '';
      if (!val) {
        dispatchEvent({ account: val, ok: false, reason: 'empty-selection' });
        return;
      }

      // Post to API and update cookie/UI
      const result = await postSelectedAccount(val);
      if (result.ok) {
        dispatchEvent({ account: val, ok: true, response: result.payload });
      } else {
        dispatchEvent({ account: val, ok: false, error: result });
        console.error('Failed to save QuickBooks selected account', result);
      }
    });
  }

  // Expose helper to programmatically select an account and persist it
  window.quickbooksSelectAccount = async function (accountFullName) {
    const select = document.getElementById('CuentaQB') || document.querySelector('select[name="CuentaQB"]');
    if (select) {
      const applied = safeSetSelectValue(select, accountFullName);
      // if option not found, still attempt to persist the raw value
      const result = await postSelectedAccount(accountFullName);
      dispatchEvent({ account: accountFullName, ok: !!result.ok, response: result });
      return result;
    } else {
      return { ok: false, reason: 'no-select-element' };
    }
  };

  // bootstrap
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();