// Plan (pseudocode):
// 1. Wait for DOM ready.
// 2. Find the select with id `CuentaQB`.
// 3. Ensure a hidden input named `AccountFullName` exists inside the form that submits the charge.
//    - If no form is found, still create the hidden input near the select to be included if the page serializes all inputs.
// 4. Keep the hidden input value synced with `CuentaQB` on load, change and on form submit.
// 5. Provide optional helper `buildChargeDto()` for AJAX flows that returns a JSON payload including AccountFullName.

// Implementation:
document.addEventListener('DOMContentLoaded', () => {
  const select = document.getElementById('CuentaQB');
  if (!select) return;

  // Try to find a logical form for submission; fallback to closest form or the document body
  const form = select.closest('form') ?? document.querySelector('form') ?? null;

  // Ensure hidden input exists
  let hidden = form ? form.querySelector('input[name="AccountFullName"]') : document.querySelector('input[name="AccountFullName"]');
  if (!hidden) {
    hidden = document.createElement('input');
    hidden.type = 'hidden';
    hidden.name = 'AccountFullName';
    hidden.id = 'AccountFullName';
    if (form) form.appendChild(hidden); else select.insertAdjacentElement('afterend', hidden);
  }

  // Sync initial value and on change
  const sync = () => { hidden.value = select.value ?? ''; };
  sync();
  select.addEventListener('change', sync);

  // Ensure value is correct right before a normal form submit
  if (form) form.addEventListener('submit', () => { sync(); });

  // Helper for AJAX submissions: builds DTO including AccountFullName
  window.buildChargeDto = function () {
    // Map other fields as needed from your page (example placeholders)
    const dto = {
      TxnDate: (document.querySelector('input[name="TxnDate"]')?.value) || new Date().toISOString(),
      PayeeFullName: document.querySelector('input[name="PayeeFullName"]')?.value || '',
      AccountFullName: select.value || '',
      ExpenseAccountFullName: document.querySelector('input[name="ExpenseAccountFullName"]')?.value || select.value || '',
      Memo: document.querySelector('input[name="Memo"]')?.value || '',
      Amount: parseFloat(document.querySelector('input[name="Amount"]')?.value) || 0
    };
    return dto;
  };

  // Example AJAX POST (uncomment and adapt to your submit handler)
  // async function postCharge() {
  //   const dto = buildChargeDto();
  //   const res = await fetch('/api/quickbooks/charge', {
  //     method: 'POST',
  //     headers: { 'Content-Type': 'application/json' },
  //     body: JSON.stringify(dto)
  //   });
  //   return res.json();
  // }
});