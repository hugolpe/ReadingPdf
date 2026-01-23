// qbView.js
console.log('qbView.js loaded');

export function attachViewHandlers() {
    console.log('Attaching view handlers...');

    document.querySelectorAll('.qb-view-btn').forEach(btn => {
        if (btn._viewAttached) return;

        btn.addEventListener('click', async (e) => {
            e.preventDefault();
            const index = btn.dataset.index;
            console.log('View button clicked for index:', index);

            const row = document.querySelector(`tr[data-index="${index}"]`);

            if (!row) {
                alert('Fila no encontrada');
                return;
            }

            const txnCell = row.querySelector('.txn-id-cell');
            const txnId = txnCell?.textContent?.trim();

            console.log('TxnID found:', txnId);

            if (!txnId) {
                alert('Esta transacción aún no ha sido registrada en QuickBooks.\n\nPor favor, registre primero la transacción usando el botón verde de registro.');
                return;
            }

            await openQBModal(txnId);
        });

        btn._viewAttached = true;
    });

    console.log('View handlers attached');
}

async function openQBModal(txnId) {
    console.log('Opening modal for TxnID:', txnId);

    const modalEl = document.getElementById('qbViewModal');
    if (!modalEl) {
        console.error('Modal element not found');
        alert('Error: Modal no encontrado');
        return;
    }

    const modal = new bootstrap.Modal(modalEl);
    const modalBody = document.getElementById('qbModalBody');

    modalBody.innerHTML = `
        <div class="text-center py-5">
            <div class="spinner-border text-primary" role="status" style="width: 3rem; height: 3rem;">
                <span class="visually-hidden">Cargando...</span>
            </div>
            <p class="mt-3 text-muted">Obteniendo datos de QuickBooks...</p>
            <small class="text-muted">TxnID: ${escapeHtml(txnId)}</small>
        </div>
    `;

    modal.show();

    try {
        console.log('Fetching data from API...');
        const response = await fetch(`https://localhost:7059/api/quickbooks/charge/${encodeURIComponent(txnId)}`);
        const result = await response.json();

        console.log('API Response:', result);
        console.log('Response data:', result.data);

        if (response.ok && result.success && result.data) {
            const data = result.data;

            // ⭐ LOG PARA DEBUG - verificar qué propiedades existen
            console.log('TxnId:', data.txnId);
            console.log('TxnDate:', data.txnDate);
            console.log('PayeeFullName:', data.payeeFullName);
            console.log('AccountFullName:', data.accountFullName);
            console.log('ExpenseAccountFullName:', data.expenseAccountFullName);
            console.log('Amount:', data.amount);
            console.log('Memo:', data.memo);
            console.log('RefNumber:', data.refNumber);

            // Renderizar datos exitosamente
            modalBody.innerHTML = `
                <div class="container-fluid">
                    <div class="qb-success-badge mb-4">
                        <i class="fas fa-check-circle me-2"></i>
                        <strong>Registro encontrado en QuickBooks</strong>
                    </div>
                    
                    <div class="row qb-data-row">
                        <div class="col-md-6">
                            <label class="form-label">TxnID (Código de Transacción):</label>
                            <input type="text" class="form-control" value="${escapeHtml(data.txnId || '')}" readonly>
                        </div>
                        <div class="col-md-6">
                            <label class="form-label">Fecha de Transacción:</label>
                            <input type="text" class="form-control" value="${formatDate(data.txnDate)}" readonly>
                        </div>
                    </div>
                    
                    <div class="row qb-data-row">
                        <div class="col-md-6">
                            <label class="form-label">Proveedor / Empresa:</label>
                            <input type="text" class="form-control" value="${escapeHtml(data.payeeFullName || '')}" readonly>
                        </div>
                        <div class="col-md-6">
                            <label class="form-label">Monto:</label>
                            <div class="input-group">
                                <span class="input-group-text">$</span>
                                <input type="text" class="form-control" value="${formatCurrency(data.amount)}" readonly>
                            </div>
                        </div>
                    </div>
                    
                    <div class="row qb-data-row">
                        <div class="col-md-6">
                            <label class="form-label">Cuenta de Tarjeta de Crédito:</label>
                            <input type="text" class="form-control" value="${escapeHtml(data.accountFullName || '')}" readonly>
                        </div>
                        <div class="col-md-6">
                            <label class="form-label">Cuenta de Gasto (Split):</label>
                            <input type="text" class="form-control" 
                                   value="${escapeHtml(data.expenseAccountFullName || 'No especificada')}" 
                                   readonly
                                   style="${!data.expenseAccountFullName ? 'background-color: #fff3cd; color: #856404;' : ''}">
                            ${!data.expenseAccountFullName ? '<small class="text-warning">⚠️ No se pudo obtener la cuenta de gasto</small>' : ''}
                        </div>
                    </div>
                    
                    <div class="row qb-data-row">
                        <div class="col-12">
                            <label class="form-label">Memo / Descripción:</label>
                            <textarea class="form-control" rows="3" readonly>${escapeHtml(data.memo || '')}</textarea>
                        </div>
                    </div>
                    
                    ${data.refNumber ? `
                    <div class="row qb-data-row">
                        <div class="col-12">
                            <label class="form-label">Número de Referencia:</label>
                            <input type="text" class="form-control" value="${escapeHtml(data.refNumber)}" readonly>
                        </div>
                    </div>
                    ` : ''}
                    
                    <!-- Mostrar información raw para debugging -->
                    <div class="mt-3">
                        <button class="btn btn-sm btn-outline-secondary" type="button" data-bs-toggle="collapse" data-bs-target="#rawData">
                            <i class="fas fa-code"></i> Ver datos completos (Debug)
                        </button>
                        <div class="collapse mt-2" id="rawData">
                            <pre class="bg-light p-3 border rounded" style="max-height: 300px; overflow-y: auto; font-size: 0.85rem;">${JSON.stringify(data, null, 2)}</pre>
                        </div>
                    </div>
                </div>
            `;

        } else {
            const errorMsg = result.error || result.message || 'No se pudo obtener el cargo';
            console.error('Error from API:', errorMsg);

            modalBody.innerHTML = `
                <div class="qb-error-badge">
                    <i class="fas fa-exclamation-triangle me-2"></i>
                    <strong>Error al obtener datos</strong>
                    <p class="mb-0 mt-2">${escapeHtml(errorMsg)}</p>
                </div>
                <div class="mt-3 text-muted">
                    <small>TxnID buscado: ${escapeHtml(txnId)}</small>
                </div>
            `;
        }

    } catch (error) {
        console.error('Error al obtener datos de QuickBooks:', error);

        modalBody.innerHTML = `
            <div class="qb-error-badge">
                <i class="fas fa-exclamation-circle me-2"></i>
                <strong>Error de conexión</strong>
                <p class="mb-0 mt-2">${escapeHtml(error.message)}</p>
            </div>
            <div class="mt-3">
                <p class="text-muted mb-1">Posibles causas:</p>
                <ul class="text-muted small">
                    <li>El servidor API no está ejecutándose (puerto 7059)</li>
                    <li>QuickBooks no está abierto o conectado</li>
                    <li>Problemas de red o certificados SSL</li>
                </ul>
            </div>
        `;
    }
}

function escapeHtml(text) {
    if (!text) return '';
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

function formatDate(dateString) {
    if (!dateString) return '';
    try {
        const date = new Date(dateString);
        return date.toLocaleDateString('es-ES', {
            year: 'numeric',
            month: 'long',
            day: 'numeric'
        });
    } catch {
        return dateString;
    }
}

function formatCurrency(amount) {
    if (amount === null || amount === undefined) return '0.00';
    try {
        return parseFloat(amount).toLocaleString('en-US', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        });
    } catch {
        return amount.toString();
    }
}