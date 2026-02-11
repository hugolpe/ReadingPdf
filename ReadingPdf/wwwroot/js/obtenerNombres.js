function applySelectionToMatches(selected, sourceIndex) {
    if (!selected) return;
    const normalizedSel = normalizeText(selected);

    document.querySelectorAll('#resultados-table tbody tr').forEach(row => {
        const idx = parseInt(row.getAttribute('data-index') || '-1', 10);
        if (isNaN(idx) || idx < 0) return;
        if (idx === sourceIndex) return;

        const descEl = row.querySelector('.descripcion-cell');
        const descText = descEl ? normalizeText(descEl.innerText || '') : '';

        const input = row.querySelector('.empresa-input');
        const inputVal = input ? normalizeText(input.value || '') : '';
        const origVal = input ? normalizeText(input.dataset.original || '') : '';

        const isMatch = (descText && descText.includes(normalizedSel)) ||
            (inputVal && inputVal.includes(normalizedSel)) ||
            (origVal && origVal.includes(normalizedSel));

        if (isMatch && input) {
            const display = normalizeText(selected);
            input.value = display;
            input.dataset.original = display;

            const r = input.closest('tr');
            if (r) {
                r.classList.remove('empresa-modified');
                r.classList.add('has-empresa');
                r.dataset.fromTable = 'true';
            }

            const hidden = document.querySelector('input[type="hidden"][name="movimientos[' + idx + '].Empresa"]');
            if (hidden) hidden.value = display;

            // server learning
            sendSelection(idx, display);
        }
    });
}

function getSelectionText(ev) {
    try {
        const sel = window.getSelection();
        let text = (sel && sel.toString()) || '';
        if (text && text.trim().length >= 2) return text.trim();
        const target = ev && ev.target;
        if (target && (target.tagName === 'INPUT' || target.TAGNAME === 'TEXTAREA') && typeof target.selectionStart === 'number') {
            const start = target.selectionStart, end = target.selectionEnd;
            if (end > start) return target.value.substring(start, end).trim();
        }
    } catch (e) { console.debug(e); }
    return '';
}

function onTableMouseUp(ev) {
    const text = getSelectionText(ev);
    console.log('📝 Selección detectada:', text);

    if (!text || text.length < 2) return;

    // find source row and index
    let node = (window.getSelection() && window.getSelection().anchorNode) || ev.target;
    while (node && node.nodeType !== Node.ELEMENT_NODE) node = node.parentElement;
    if (!node) return;
    const row = node.closest && node.closest('tr');
    if (!row) return;
    const index = parseInt(row.getAttribute('data-index') || '-1', 10);
    if (isNaN(index) || index < 0) return;

    // optimistic UI and set source row input immediately
    const displayValue = normalizeText(text);
    const sourceInput = document.querySelector('.empresa-input[data-index="' + index + '"]');
    if (sourceInput) {
        sourceInput.value = displayValue;
        sourceInput.dataset.original = displayValue;
        // update hidden server-side field so form posts current value
        const hidden = document.querySelector('input[type="hidden"][name="movimientos[' + index + '].Empresa"]');
        if (hidden) hidden.value = displayValue;
    }
    row.classList.remove('empresa-modified');
    row.classList.add('has-empresa');
    row.dataset.fromTable = 'true';

    // send to server then apply to other matches using server response (if any)
    sendSelection(index, text).then(result => {
        const serverValue = (result && result.empresa) ? result.empresa : displayValue;
        // ensure source input reflects server normalization (uppercase) once server responds
        const src = document.querySelector('.empresa-input[data-index="' + index + '"]');
        if (src) {
            const serverDisplay = (serverValue || '').toString().toUpperCase();
            src.value = serverDisplay;
            src.dataset.original = serverDisplay;
            const hidden = document.querySelector('input[type="hidden"][name="movimientos[' + index + '].Empresa"]');
            if (hidden) hidden.value = serverDisplay;
        }
        // apply to other rows
        applySelectionToMatches(serverValue, index);
    }).catch(err => console.error(err));

    try { const s = window.getSelection(); if (s) s.removeAllRanges(); } catch (e) { }
}

/**
 * sendSelection
 * - Sends a small "learning" request to the server so the backend can persist or normalize
 * - Returns a promise resolved with the server response or a graceful fallback { empresa: UPPERCASE_VALUE }
 */
async function sendSelection(indexOrIdx, empresa) {
    const payload = {
        index: typeof indexOrIdx === 'number' ? indexOrIdx : parseInt(indexOrIdx || '-1', 10),
        empresa: empresa || ''
    };

    // Attempt to include antiforgery token if present
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

    try {
        const resp = await fetch('/api/selection/learn', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                ...(token ? { 'RequestVerificationToken': token } : {})
            },
            body: JSON.stringify(payload)
        });

        if (!resp.ok) {
            throw new Error('HTTP ' + resp.status);
        }

        const data = await resp.json();
        // Expecting server to return something like { empresa: "NORMALIZED NAME" }
        return data || { empresa: (empresa || '').toUpperCase() };
    } catch (err) {
        console.warn('sendSelection failed, falling back to local normalization:', err);
        return { empresa: (empresa || '').toUpperCase() };
    }
}

/*
  NEW: propagate manual changes in the empresa input to other matching rows.
  - Uses event delegation to listen user-initiated 'change' events on .empresa-input
  - Will call sendSelection then applySelectionToMatches with the normalized server value
*/
document.addEventListener('change', function (ev) {
    try {
        const target = ev.target;
        if (!target || !target.classList) return;
        if (!target.classList.contains('empresa-input')) return;
        // ensure user-originated change where possible
        if (ev.isTrusted === false) return;

        const indexAttr = target.getAttribute('data-index');
        const index = parseInt(indexAttr || '-1', 10);
        if (isNaN(index) || index < 0) return;

        const rawVal = (target.value || '').trim();
        if (!rawVal || rawVal.length < 2) return;

        // Update hidden field and row state immediately
        const hidden = document.querySelector('input[type="hidden"][name="movimientos[' + index + '].Empresa"]');
        if (hidden) hidden.value = rawVal;

        const row = target.closest && target.closest('tr');
        if (row) {
            row.classList.remove('empresa-modified');
            row.classList.add('has-empresa');
            row.dataset.fromTable = 'true';
        }

        // Send to server and apply to matches once server responds
        sendSelection(index, rawVal).then(result => {
            const serverValue = (result && result.empresa) ? result.empresa : rawVal;
            // reflect server-normalized value on source
            const src = document.querySelector('.empresa-input[data-index="' + index + '"]');
            if (src) {
                const display = (serverValue || '').toString().toUpperCase();
                src.value = display;
                src.dataset.original = display;
                const hidden2 = document.querySelector('input[type="hidden"][name="movimientos[' + index + '].Empresa"]');
                if (hidden2) hidden2.value = display;
            }
            applySelectionToMatches(serverValue, index);
        }).catch(err => console.error('Error during propagation:', err));
    } catch (ex) {
        console.error('Propagation handler error:', ex);
    }
}, true);

// Hook selection-based propagation into mouseup on table (keep existing behavior)
document.addEventListener('mouseup', function (ev) {
    try {
        // only react if selection is inside resultados-table
        const table = document.getElementById('resultados-table');
        if (!table) return;
        if (!ev.target.closest || !ev.target.closest('#resultados-table')) return;
        onTableMouseUp(ev);
    } catch (e) {
        console.error('mouseup handler error:', e);
    }
});