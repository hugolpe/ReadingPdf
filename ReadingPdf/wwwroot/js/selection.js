// selection.js
let tableRef = null;

/* -------------------------
   Inicialización
------------------------- */
function initSelection() {
    console.log('📋 Inicializando selection.js...');

    tableRef = document.getElementById('resultados-table');
    if (!tableRef) {
        console.error('❌ No se encontró #resultados-table');
        return;
    }

    attachRowCheckboxHandlers();
    attachSelectAllHandler();

    // Auto-check rows that originally had an empresa (class has-empresa)
    autoCheckHasEmpresaRows();

    // checkbox para mostrar/ocultar desmarcadas
    const toggle = document.getElementById('show-unchecked');
    if (toggle) toggle.addEventListener('change', updateUncheckedVisibility);

    updateSelectedSummary();
    updateUncheckedVisibility();

    console.log('✅ Selection.js configurado');
}

/* -------------------------
   NOTE: getServerData is provided centrally by serverData.js
   Do NOT duplicate here to avoid inconsistent parsing/attributes.
------------------------- */

/* -------------------------
   Marca automáticamente las filas que venían con empresa
   (clase `has-empresa`) para que cuenten como seleccionadas
------------------------- */
function autoCheckHasEmpresaRows() {
    if (!tableRef) return;
    tableRef.querySelectorAll('tbody tr.has-empresa').forEach(row => {
        const chk = row.querySelector('.row-check');
        if (chk && !chk.disabled) {
            chk.checked = true;
            // ensure the change handler runs (if attached)
            chk.dispatchEvent(new Event('change', { bubbles: true }));
        }
    });
}

/* -------------------------
   Resumen de selección
------------------------- */
function updateSelectedSummary() {
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
    // Uses centralized getServerData() from serverData.js
    const sd = (typeof getServerData === 'function') ? getServerData() : {};
    if (sd && sd.interest != null) {
        const prev = parseFloat(String(sd.interest).replace(',', '.'));
        const prevEl = document.getElementById('previous-value');
        const remEl = document.getElementById('remaining-value');

        //console.log('🔢 Server interest:', prev);  
        //console.log('🔢 Selected sum:', sum);

        if (prevEl) prevEl.textContent = formatCurrency(prev);
        // Restar el sum en lugar de sumarlo
        if (remEl) remEl.textContent = formatCurrency(prev + sum);
    } else {
        // show dash if no server interest available
        const prevEl = document.getElementById('previous-value');
        const remEl = document.getElementById('remaining-value');
        if (prevEl) prevEl.textContent = '-';
        if (remEl) remEl.textContent = '-';
    }

    updateSelectAllState();
}

/* -------------------------
   Mostrar / ocultar filas desmarcadas
------------------------- */
function updateUncheckedVisibility() {
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

// Manejo de selección de texto en celdas .descripcion-cell
// Captura la selección con el ratón y la muestra en #selected-text-display
(function () {
    function closestDescripcion(node) {
        while (node) {
            if (node.nodeType === 1 && node.classList && node.classList.contains('descripcion-cell')) return node;
            node = node.parentNode;
        }
        return null;
    }

    const display = document.getElementById('selected-text-display');
    const textEl = document.getElementById('selected-text');
    const copyBtn = document.getElementById('selected-copy-btn');

    // Mostrar la selección si está dentro de la misma celda .descripcion-cell
    document.addEventListener('mouseup', function (ev) {
        try {
            const sel = window.getSelection();
            if (!sel || sel.isCollapsed) return;

            const anchor = sel.anchorNode;
            const focus = sel.focusNode;
            const descA = closestDescripcion(anchor);
            const descB = closestDescripcion(focus);

            if (descA && descA === descB) {
                const txt = sel.toString().trim();
                if (txt.length === 0) return;

                textEl.textContent = txt;
                display.style.display = 'inline-flex';

                // --- NEW: copiar automáticamente el texto seleccionado al campo empresa/nombre de la misma fila
                try {
                    const row = descA.closest('tr');
                    if (row) {
                        const empresaInput = row.querySelector('.empresa-input');
                        if (empresaInput) {
                            // Set value and notify other listeners
                            empresaInput.value = txt;
                            empresaInput.dispatchEvent(new Event('input', { bubbles: true }));
                            empresaInput.dispatchEvent(new Event('change', { bubbles: true }));
                        }
                    }
                } catch (ex) {
                    console.error('Error al volcar selección en input empresa:', ex);
                }
            }
        } catch (ex) {
            console.error('Error leyendo selección:', ex);
        }
    });

    // Copiar al portapapeles
    if (copyBtn) {
        copyBtn.addEventListener('click', function () {
            const txt = textEl.textContent || '';
            if (!txt) return;
            if (navigator.clipboard && navigator.clipboard.writeText) {
                navigator.clipboard.writeText(txt).catch(err => console.error('Clipboard error', err));
            } else {
                // Fallback
                const ta = document.createElement('textarea');
                ta.value = txt;
                document.body.appendChild(ta);
                ta.select();
                try { document.execCommand('copy'); } catch (e) { console.error(e); }
                document.body.removeChild(ta);
            }
        });
    }

    // Click fuera -> limpiar visual y selección
    document.addEventListener('mousedown', function (ev) {
        // si el click no es dentro de una descripcion-cell ni dentro del display, ocultar
        if (!ev.target.closest || (!ev.target.closest('.descripcion-cell') && !ev.target.closest('#selected-text-display'))) {
            if (display) display.style.display = 'none';
            try { const s = window.getSelection(); if (s) s.removeAllRanges(); } catch {}
        }
    });

    // Tecla Escape limpia selección
    document.addEventListener('keydown', function (ev) {
        if (ev.key === 'Escape') {
            if (display) display.style.display = 'none';
            try { const s = window.getSelection(); if (s) s.removeAllRanges(); } catch {}
        }
    });
})();