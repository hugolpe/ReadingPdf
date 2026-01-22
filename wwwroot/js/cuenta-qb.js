// Persist CuentaQB, restore on load, sync hidden AccountFullName and show saved value in the UI.
document.addEventListener('DOMContentLoaded', () => {
  const STORAGE_KEY = 'CuentaQB_selected';
  const select = document.getElementById('CuentaQB');
  if (!select) return;

  // Restore saved value if any
  const saved = localStorage.getItem(STORAGE_KEY);
  if (saved) {
    const optionExists = Array.from(select.options).some(o => o.value === saved);
    if (optionExists) select.value = saved;
  }

  // Ensure hidden input for server/API consumption exists
  function ensureHidden() {
    const form = select.closest('form') ?? document.querySelector('form');
    let hidden = (form ? form : document).querySelector('input[name="AccountFullName"]');
    if (!hidden) {
      hidden = document.createElement('input');
      hidden.type = 'hidden';
      hidden.name = 'AccountFullName';
      hidden.id = 'AccountFullName';
      if (form) form.appendChild(hidden); else select.insertAdjacentElement('afterend', hidden);
    }
    return hidden;
  }

  const hidden = ensureHidden();
  hidden.value = select.value ?? '';

  function showSavedBanner(val) {
    const disp = document.getElementById('savedCuentaDisplay');
    const container = document.getElementById('qb-saved');
    if (disp) disp.textContent = val ?? '';
    if (container) {
      if (val) container.classList.remove('d-none');
      else container.classList.add('d-none');
    }
  }

  const saveAndSync = () => {
    const val = select.value ?? '';
    localStorage.setItem(STORAGE_KEY, val);
    hidden.value = val;
    select.dispatchEvent(new CustomEvent('CuentaQBChanged', { detail: { value: val } }));
    // update any result displays and show banner
    document.querySelectorAll('.result-cuenta').forEach(el => { el.textContent = val; });
    document.querySelectorAll('[data-cuenta-target]').forEach(el => { el.textContent = val; });
    showSavedBanner(val);
  };

  select.addEventListener('change', saveAndSync);

  // Ensure correct value before any form submit
  const form = select.closest('form');
  if (form) form.addEventListener('submit', () => { hidden.value = select.value ?? ''; });

  window.getSavedCuentaQB = () => localStorage.getItem(STORAGE_KEY) ?? '';
  window.clearSavedCuentaQB = () => { localStorage.removeItem(STORAGE_KEY); showSavedBanner(''); };

  // Apply saved value on load to result elements and banner
  const current = window.getSavedCuentaQB();
  if (current) {
    document.querySelectorAll('.result-cuenta').forEach(el => { el.textContent = current; });
    document.querySelectorAll('[data-cuenta-target]').forEach(el => { el.textContent = current; });
    showSavedBanner(current);
  }
});