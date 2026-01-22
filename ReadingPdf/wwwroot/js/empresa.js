// empresa.js
import { normalizeText } from './utils.js';

export function attachDescripcionHandler() {
    document.querySelectorAll('.descripcion-cell').forEach(cell => {
        cell.addEventListener('mouseup', onDescripcionMouseUp);
    });
}

function onDescripcionMouseUp(ev) {
    const sel = window.getSelection();
    const text = sel.toString().trim();
    if (text.length < 2) return;

    const needle = normalizeText(text);
    const value = text.toUpperCase();
    let updated = false;

    document.querySelectorAll('#resultados-table tbody tr').forEach(r => {
        const desc = r.querySelector('.descripcion-cell');
        const input = r.querySelector('.empresa-input');
        if (desc && input && normalizeText(desc.textContent).includes(needle)) {
            input.value = value;
            r.classList.add('has-empresa');
            updated = true;
        }
    });

    if (updated) aprenderEmpresa(value);
    sel.removeAllRanges();
}

function aprenderEmpresa(texto) {
    fetch('/Empresa/Aprender', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || ''
        },
        body: JSON.stringify({ TextoSeleccionado: texto })
    });
}
