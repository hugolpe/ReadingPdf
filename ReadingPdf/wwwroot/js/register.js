// register.js
import { getServerData } from './serverData.js';

console.log('register.js loaded');

export function attachRegisterHandlers() {
    document.querySelectorAll('.registrar-btn').forEach(btn => {
        if (btn._attached) return;
        btn.addEventListener('click', async e => {
            e.preventDefault();
            const idx = btn.dataset.index;
            const status = document.querySelector(`.registrar-status[data-index="${idx}"]`);
            await registerCharge(idx, btn, status);
        });
        btn._attached = true;
    });
}

async function registerCharge(index, btn, statusEl) {
    const row = document.querySelector(`tr[data-index="${index}"]`);
    if (!row) {
        console.error('Fila no encontrada para index:', index);
        return;
    }

    const sd = getServerData();
    const cuenta = row.dataset.cuenta;
    const cuentaQB = sd.selectedQB?.trim() || cuenta;

    // Obtener la celda de TxnID
    const txnCell = row.querySelector('.txn-id-cell');

    const payload = {
        TxnDate: new Date().toISOString(),
        PayeeFullName: (row.querySelector('.empresa-input')?.value || '').trim(),
        AccountFullName: cuentaQB,
        Memo: row.querySelector('.descripcion-cell')?.textContent?.trim() || '',
        ExpenseAccountFullName: (row.dataset.cuenta || '').trim(),
        Amount: Math.abs(
            parseFloat(String(row.dataset.monto || '0').replace('$', '').replace(',', '.'))
        )
    };

    // Validaciones
    if (!payload.PayeeFullName) {
        statusEl.textContent = '❌ Sin empresa';
        statusEl.style.color = 'crimson';
        return;
    }
    if (!payload.ExpenseAccountFullName) {
        statusEl.textContent = '❌ Sin cuenta';
        statusEl.style.color = 'crimson';
        return;
    }
    if (!payload.Amount || payload.Amount <= 0) {
        statusEl.textContent = '❌ Monto inválido';
        statusEl.style.color = 'crimson';
        return;
    }

    // Mostrar indicador de carga
    statusEl.innerHTML = '<span class="spinner-border spinner-border-sm"></span>';
    statusEl.style.color = '';
    btn.disabled = true;

    console.log('Enviando payload a QuickBooks:', payload);

    try {
        const res = await fetch('https://localhost:7059/api/quickbooks/charge', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });

        // ⭐ PARSEAR JSON UNA SOLA VEZ
        const data = await res.json();

        console.log('Respuesta del servidor:', data);

        // Verificar si fue exitoso
        if (res.ok && data.txnId) {
            // Éxito - registro completado
            statusEl.textContent = `✔ OK`;
            statusEl.style.color = 'green';
            btn.disabled = true;
            btn.textContent = 'Registrado';

            // Guardar TxnID en la celda
            if (txnCell) {
                txnCell.textContent = data.txnId;
                console.log('TxnID guardado:', data.txnId);
            }

            // Habilitar botón de ver/editar
            const viewBtn = row.querySelector('.qb-view-btn');
            if (viewBtn) {
                viewBtn.dataset.txnid = data.txnId;
                viewBtn.disabled = false;
                viewBtn.classList.remove('btn-secondary');
                viewBtn.classList.add('btn-info');
            }

            // Marcar fila como registrada
            row.classList.add('registered');

        } else {
            // Error del servidor
            const errorMsg = data.error || data.message || res.statusText || 'Error desconocido';
            statusEl.textContent = `❌ Error`;
            statusEl.title = errorMsg;
            statusEl.style.color = 'crimson';
            btn.disabled = false;

            console.error('Error del servidor:', errorMsg);
            alert(`Error al registrar en QuickBooks:\n${errorMsg}`);
        }

    } catch (err) {
        // Error de red o parsing
        console.error('Error en registerCharge:', err);
        statusEl.textContent = '❌ Error de conexión';
        statusEl.title = err.message;
        statusEl.style.color = 'crimson';
        btn.disabled = false;

        alert(`Error de conexión:\n${err.message}`);
    }
}