// selection.js
import { getServerData } from './serverData.js';
import { formatCurrency } from './utils.js';

let tableRef = null;

/* -------------------------
   Inicialización
------------------------- */
export function initSelection(table) {
    tableRef = table;
    if (!tableRef) return;

    attachRowCheckboxHandlers();
    attachSelectAllHandler();

    // checkbox para mostrar/ocultar desmarcadas
    const toggle = document.getElementById('show-unchecked');
    if (toggle) toggle.addEventListener('change', updateUncheckedVisibility);

    updateSelectedSummary();
    updateUncheckedVisibility();
}

/* -------------------------
   Resumen de selección
------------------------- */
export function updateSelectedSummary() {
    if (!tableRef) return;

    const checks = [...tableRef.querySelectorAll('.row-check')];
    const checked = checks.filter(c => c.checked);

    // sumar montos de las filas marcadas
    const sum = checked.reduce((acc, c) => {
        const row = c.closest('tr');
        const raw = (row?.dataset.monto || '0').replace(',', '.');
        const n = parseFloat(raw);
        return acc + (isNaN(n) ? 0 : n);
    }, 0);

    // actualizar contador y suma en DOM
    const countEl = document.getElementById('selected-count');
    const sumEl = document.getElementById('selected-sum');
    if (countEl) countEl.textContent = checked.length;
    if (sumEl) sumEl.textContent = formatCurrency(sum);

    // saldo anterior y restante según servidor
    const sd = getServerData();
    if (sd.interest != null) {
        const prev = parseFloat(String(sd.interest).replace(',', '.'));
        const prevEl = document.getElementById('previous-value');
        const remEl = document.getElementById('remaining-value');

        if (prevEl) prevEl.textContent = formatCurrency(prev);
        if (remEl) remEl.textContent = formatCurrency(prev + sum);
    }

    updateSelectAllState();
}

/* -------------------------
   Mostrar / ocultar filas desmarcadas
------------------------- */
export function updateUncheckedVisibility() {
    if (!tableRef) return;

    const showUnchecked = document.getElementById('show-unchecked')?.checked ?? false;

    tableRef.querySelectorAll('tbody tr').forEach(row => {
        const chk = row.querySelector('.row-check');
        if (!chk) return;

        row.style.display = (chk.checked || showUnchecked) ? '' : 'none';
    });
}

/* -------------------------
   Estado de select all
------------------------- */
function updateSelectAllState() {
    const selectAll = document.getElementById('select-all');
    if (!selectAll || !tableRef) return;

    const checks = [...tableRef.querySelectorAll('.row-check')];
    const checkedCount = checks.filter(c => c.checked).length;

    if (checkedCount === 0) {
        selectAll.checked = false;
        selectAll.indeterminate = false;
    } else if (checkedCount === checks.length) {
        selectAll.checked = true;
        selectAll.indeterminate = false;
    } else {
        selectAll.checked = false;
        selectAll.indeterminate = true;
    }
}

/* -------------------------
   Handler select all
------------------------- */
function attachSelectAllHandler() {
    const selectAll = document.getElementById('select-all');
    if (!selectAll || selectAll._attached) return;

    selectAll.addEventListener('change', () => {
        tableRef.querySelectorAll('.row-check').forEach(c => {
            if (!c.disabled) c.checked = selectAll.checked;
        });
        updateUncheckedVisibility();
        updateSelectedSummary();
    });

    selectAll._attached = true;
}

/* -------------------------
   Handlers para checkboxes por fila
------------------------- */
function attachRowCheckboxHandlers() {
    tableRef.querySelectorAll('.row-check').forEach(chk => {
        if (chk._attached) return;

        chk.addEventListener('change', () => {
            updateSelectedSummary();
            updateUncheckedVisibility();
        });

        // clic en la fila para marcar/desmarcar
        const row = chk.closest('tr');
        if (row && !row._rowClickAttached) {
            row.addEventListener('click', e => {
                const tag = e.target?.tagName?.toLowerCase();
                if (['input', 'button', 'select', 'a', 'textarea', 'label'].includes(tag)) return;

                chk.checked = !chk.checked;
                chk.dispatchEvent(new Event('change', { bubbles: true }));
            });
            row._rowClickAttached = true;
        }

        chk._attached = true;
    });
}
