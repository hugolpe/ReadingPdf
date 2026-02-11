// ====================================================================
// CORRECCIÓN DE FORMATO DE FECHA EN register.js
// Reemplazar el archivo completo wwwroot/js/register.js
// ====================================================================

// ===============================
//  REGISTRO EN QUICKBOOKS
// ===============================

/**
 * ✅ Convierte una fecha al formato yyyy-MM-dd que QuickBooks espera
 * @param {string} dateString - Fecha en formato M/d/yyyy o MM/dd/yyyy
 * @returns {string} - Fecha en formato yyyy-MM-dd
 */
function convertirFechaParaQuickBooks(dateString) {
    try {
        // Si ya está en formato yyyy-MM-dd, retornar tal cual
        if (/^\d{4}-\d{2}-\d{2}$/.test(dateString)) {
            return dateString;
        }

        // Parsear fecha en formato M/d/yyyy o MM/dd/yyyy
        const parts = dateString.split('/');
        if (parts.length === 3) {
            const month = parts[0].padStart(2, '0');
            const day = parts[1].padStart(2, '0');
            const year = parts[2];
            return `${year}-${month}-${day}`;
        }

        // Intentar con Date.parse
        const date = new Date(dateString);
        if (!isNaN(date.getTime())) {
            const year = date.getFullYear();
            const month = String(date.getMonth() + 1).padStart(2, '0');
            const day = String(date.getDate()).padStart(2, '0');
            return `${year}-${month}-${day}`;
        }

        console.error('❌ No se pudo convertir la fecha:', dateString);
        return dateString; // Retornar original si no se puede convertir
    } catch (error) {
        console.error('❌ Error convirtiendo fecha:', error);
        return dateString;
    }
}

/**
 * ✅ Obtiene el tipo de transacción actual desde sessionStorage
 */
function obtenerTipoTransaccionActual() {
    return sessionStorage.getItem('cuentaQB_TipoTransaccion') || 'CreditCardCharge';
}

/**
 * ✅ Obtiene el tipo de cuenta actual desde sessionStorage
 */
function obtenerTipoCuentaActual() {
    return sessionStorage.getItem('cuentaQB_Tipo') || 'atCreditCard';
}

/**
 * ✅ Obtiene el nombre de la cuenta QB desde sessionStorage
 */
function obtenerNombreCuentaQB() {
    return sessionStorage.getItem('cuentaQB_Nombre') || '';
}

/**
 * ✅ Detecta si la cuenta es AMEX
 */
function esAmex() {
    const nombreCuenta = obtenerNombreCuentaQB();
    return /amex|american express/i.test(nombreCuenta);
}

/**
 * ✅ FUNCIÓN PRINCIPAL: Determina el tipo de documento QB
 */
function determinarTipoDocumentoParaRegistro(monto, tipoCuenta) {
    const esDebito = monto < 0;
    const esCredito = monto > 0;
    const isAmex = esAmex();

    console.log(`🔍 [register.js] Determinando tipo para registro:`, {
        monto,
        tipoCuenta,
        esDebito,
        esCredito,
        isAmex
    });

    // CASO 1: CUENTA BANCARIA
    if (tipoCuenta === 'atBank') {
        if (esCredito) {
            console.log('✅ Cuenta Bancaria + Crédito → Deposit');
            return 'Deposit';
        } else {
            console.log('✅ Cuenta Bancaria + Débito → Check');
            return 'Check';
        }
    }

    // CASO 2: TARJETA DE CRÉDITO
    if (tipoCuenta === 'atCreditCard') {
        if (isAmex) {
            if (esCredito) {
                console.log('✅ AMEX + Crédito → CreditCardCharge (invertido)');
                return 'CreditCardCharge';
            } else {
                console.log('✅ AMEX + Débito → CreditCardCredit (invertido)');
                return 'CreditCardCredit';
            }
        }

        if (esDebito) {
            console.log('✅ Tarjeta + Débito → CreditCardCharge');
            return 'CreditCardCharge';
        } else {
            console.log('✅ Tarjeta + Crédito → CreditCardCredit');
            return 'CreditCardCredit';
        }
    }

    // FALLBACK
    console.warn('⚠️ Tipo de cuenta desconocido, usando fallback');
    return esDebito ? 'CreditCardCharge' : 'Deposit';
}

/**
 * ✅ Registra una transacción en QuickBooks
 */
async function registrarEnQuickBooks(params) {
    const {
        index,
        vendor,
        memo,
        amount,
        txnDate,
        accountFullName,
        expenseAccount,
        tipoDocumento
    } = params;

    const $statusSpan = $(`.registrar-status[data-index="${index}"]`);
    const $btn = $(`.registrar-btn[data-index="${index}"]`);
    const $row = $(`#resultados-table tbody tr[data-index="${index}"]`);

    try {
        // VALIDACIONES
        if (!vendor || vendor.trim() === '') {
            Swal.fire({
                icon: 'warning',
                title: 'Falta información',
                text: 'Por favor ingresa el nombre de la empresa antes de registrar',
                confirmButtonText: 'OK'
            });
            return;
        }

        if (!accountFullName) {
            Swal.fire({
                icon: 'error',
                title: 'Error',
                text: 'No se ha seleccionado una cuenta de QuickBooks',
                confirmButtonText: 'OK'
            });
            return;
        }

        const tiposValidos = ['CreditCardCharge', 'Deposit', 'Check', 'CreditCardCredit'];
        if (!tiposValidos.includes(tipoDocumento)) {
            console.error('❌ Tipo de documento inválido:', tipoDocumento);
            Swal.fire({
                icon: 'error',
                title: 'Error',
                text: `Tipo de documento inválido: ${tipoDocumento}`,
                confirmButtonText: 'OK'
            });
            return;
        }

        // ✅ CONVERTIR FECHA AL FORMATO CORRECTO
        const txnDateFormatted = convertirFechaParaQuickBooks(txnDate);

        console.log('📤 [register.js] Iniciando registro en QuickBooks:', {
            vendor,
            amount,
            txnDateOriginal: txnDate,
            txnDateFormatted: txnDateFormatted,  // ✅ MOSTRAR FECHA CONVERTIDA
            tipoDocumento,
            accountFullName,
            expenseAccount
        });

        // MOSTRAR ESTADO DE CARGA
        $btn.prop('disabled', true);
        $statusSpan.html('<span class="spinner-border spinner-border-sm" role="status"></span>');

        // PREPARAR PAYLOAD
        const payload = {
            Vendor: vendor,
            Memo: memo || '',
            Amount: Math.abs(amount),
            TxnDate: txnDateFormatted,  // ✅ USAR LA FECHA FORMATEADA
            AccountFullName: accountFullName,
            ExpenseAccount: expenseAccount || '',
            TipoDocumento: tipoDocumento
        };

        console.log('📦 Payload para API:', payload);

        // LLAMAR AL ENDPOINT
        const response = await fetch('/PdfNew/RegistrarEnQuickBooks', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val()
            },
            body: JSON.stringify(payload)
        });

        const data = await response.json();

        // MANEJO DE RESPUESTA
        if (data.success) {
            console.log('✅ Registro exitoso:', data);

            $statusSpan.html('<span class="text-success"><i class="fas fa-check-circle"></i></span>');

            const $txnIdCell = $row.find('.txn-id-cell');
            $txnIdCell.text(data.txnId || '');
            $txnIdCell.attr('data-quickbooks-txnid', data.txnId || '');

            Swal.fire({
                icon: 'success',
                title: '✅ Registrado',
                html: `
                    <p>Transacción registrada exitosamente en QuickBooks</p>
                    <p><strong>Tipo:</strong> ${data.tipoDocumento}</p>
                    <p><strong>TxnID:</strong> ${data.txnId || 'N/A'}</p>
                `,
                timer: 3000,
                showConfirmButton: false
            });

            $btn.removeClass('btn-success').addClass('btn-info');
            $btn.find('i').removeClass('fa-file-invoice-dollar').addClass('fa-check');

        } else {
            console.error('❌ Error en registro:', data.message);

            $statusSpan.html('<span class="text-danger"><i class="fas fa-times-circle"></i></span>');

            Swal.fire({
                icon: 'error',
                title: 'Error',
                text: data.message || 'Error al registrar en QuickBooks',
                confirmButtonText: 'OK'
            });
        }

    } catch (error) {
        console.error('💥 Error fatal:', error);

        $statusSpan.html('<span class="text-danger"><i class="fas fa-exclamation-triangle"></i></span>');

        Swal.fire({
            icon: 'error',
            title: 'Error de comunicación',
            text: 'No se pudo conectar con el servidor',
            confirmButtonText: 'OK'
        });
    } finally {
        setTimeout(() => {
            $btn.prop('disabled', false);
            $statusSpan.html('');
        }, 2000);
    }
}

// ===============================
//  EVENT LISTENERS
// ===============================

$(document).ready(function () {

    /**
     * ✅ Click en botón de registro individual
     */
    $(document).on('click', '.registrar-btn', async function () {
        const $btn = $(this);
        const index = $btn.data('index');
        const $row = $(`#resultados-table tbody tr[data-index="${index}"]`);

        const monto = parseFloat($row.data('monto')) || 0;
        const empresa = $row.find('.empresa-input').val() || '';
        const descripcion = $row.find('.descripcion-cell').text().trim();
        const fecha = $row.find('td:eq(1)').text().trim();

        const cuentaQB = obtenerNombreCuentaQB();
        const tipoCuenta = obtenerTipoCuentaActual();
        const cuentaExpense = $row.data('cuenta') || '';

        const tipoDocumentoFromRow = $row.data('tipo-documento');
        const tipoDocumentoFromCell = $row.find('td[data-tipo-calculado]').attr('data-tipo-calculado');
        const tipoDocumento = tipoDocumentoFromRow ||
            tipoDocumentoFromCell ||
            determinarTipoDocumentoParaRegistro(monto, tipoCuenta);

        console.log('🎯 [register.js] Datos para registro:', {
            index,
            empresa,
            monto,
            fecha,
            tipoCuenta,
            tipoDocumento,
            cuentaQB,
            cuentaExpense
        });

        if (!empresa || empresa.trim() === '') {
            Swal.fire({
                icon: 'warning',
                title: 'Falta información',
                text: 'Por favor ingresa el nombre de la empresa antes de registrar',
                confirmButtonText: 'OK'
            });
            return;
        }

        if (!cuentaQB) {
            Swal.fire({
                icon: 'error',
                title: 'Error',
                text: 'No se ha seleccionado una cuenta de QuickBooks',
                confirmButtonText: 'OK'
            });
            return;
        }

        const confirmResult = await Swal.fire({
            title: '💳 Registrar en QuickBooks',
            html: `
                <div style="text-align: left;">
                    <p><strong>Empresa:</strong> ${empresa}</p>
                    <p><strong>Fecha:</strong> ${fecha}</p>
                    <p><strong>Monto:</strong> $${Math.abs(monto).toFixed(2)}</p>
                    <p><strong>Tipo:</strong> ${tipoDocumento}</p>
                    <p><strong>Cuenta:</strong> ${cuentaQB}</p>
                    <p><strong>Cuenta Expense:</strong> ${cuentaExpense || 'N/A'}</p>
                </div>
                <hr>
                <p>¿Deseas registrar esta transacción?</p>
            `,
            icon: 'question',
            showCancelButton: true,
            confirmButtonText: '✅ Sí, registrar',
            cancelButtonText: '❌ Cancelar',
            confirmButtonColor: '#28a745',
            cancelButtonColor: '#dc3545'
        });

        if (!confirmResult.isConfirmed) {
            return;
        }

        await registrarEnQuickBooks({
            index: index,
            vendor: empresa,
            memo: descripcion,
            amount: monto,
            txnDate: fecha,
            accountFullName: cuentaQB,
            expenseAccount: cuentaExpense,
            tipoDocumento: tipoDocumento
        });
    });

    /**
     * ✅ Registrar múltiples transacciones seleccionadas
     */
    $(document).on('click', '#btn-registrar-seleccionadas', async function () {
        const $selectedRows = $('#resultados-table tbody tr').filter(function () {
            return $(this).find('.row-check').is(':checked');
        });

        if ($selectedRows.length === 0) {
            Swal.fire({
                icon: 'warning',
                title: 'Ninguna seleccionada',
                text: 'Por favor selecciona al menos una transacción',
                confirmButtonText: 'OK'
            });
            return;
        }

        const tipoCuenta = obtenerTipoCuentaActual();
        const cuentaQB = obtenerNombreCuentaQB();

        if (!cuentaQB) {
            Swal.fire({
                icon: 'error',
                title: 'Error',
                text: 'No se ha seleccionado una cuenta de QuickBooks',
                confirmButtonText: 'OK'
            });
            return;
        }

        const confirmResult = await Swal.fire({
            title: '💳 Registrar múltiples',
            html: `
                <p>¿Deseas registrar <strong>${$selectedRows.length}</strong> transacciones en QuickBooks?</p>
                <p><small>Tipo de cuenta: ${tipoCuenta === 'atBank' ? 'Banco' : 'Tarjeta de Crédito'}</small></p>
            `,
            icon: 'question',
            showCancelButton: true,
            confirmButtonText: '✅ Sí, registrar todas',
            cancelButtonText: '❌ Cancelar',
            confirmButtonColor: '#28a745',
            cancelButtonColor: '#dc3545'
        });

        if (!confirmResult.isConfirmed) {
            return;
        }

        Swal.fire({
            title: 'Registrando...',
            html: `<p>Procesando <span id="progress-counter">0</span> de ${$selectedRows.length}</p>`,
            allowOutsideClick: false,
            didOpen: () => {
                Swal.showLoading();
            }
        });

        let exitosos = 0;
        let errores = 0;

        for (let i = 0; i < $selectedRows.length; i++) {
            const $row = $selectedRows.eq(i);
            const index = $row.data('index');
            const monto = parseFloat($row.data('monto')) || 0;
            const empresa = $row.find('.empresa-input').val() || '';
            const descripcion = $row.find('.descripcion-cell').text().trim();
            const fecha = $row.find('td:eq(1)').text().trim();
            const cuentaExpense = $row.data('cuenta') || '';

            const tipoDocumentoFromRow = $row.data('tipo-documento');
            const tipoDocumentoFromCell = $row.find('td[data-tipo-calculado]').attr('data-tipo-calculado');
            const tipoDocumento = tipoDocumentoFromRow ||
                tipoDocumentoFromCell ||
                determinarTipoDocumentoParaRegistro(monto, tipoCuenta);

            $('#progress-counter').text(i + 1);

            // ✅ CONVERTIR FECHA
            const txnDateFormatted = convertirFechaParaQuickBooks(fecha);

            try {
                const response = await fetch('/PdfNew/RegistrarEnQuickBooks', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val()
                    },
                    body: JSON.stringify({
                        Vendor: empresa,
                        Memo: descripcion,
                        Amount: Math.abs(monto),
                        TxnDate: txnDateFormatted,  // ✅ USAR FECHA FORMATEADA
                        AccountFullName: cuentaQB,
                        ExpenseAccount: cuentaExpense,
                        TipoDocumento: tipoDocumento
                    })
                });

                const data = await response.json();

                if (data.success) {
                    exitosos++;

                    const $txnIdCell = $row.find('.txn-id-cell');
                    $txnIdCell.text(data.txnId || '');
                    $txnIdCell.attr('data-quickbooks-txnid', data.txnId || '');

                    $row.find('.registrar-btn')
                        .removeClass('btn-success')
                        .addClass('btn-info')
                        .find('i')
                        .removeClass('fa-file-invoice-dollar')
                        .addClass('fa-check');
                } else {
                    errores++;
                    console.error(`Error registrando fila ${index}:`, data.message);
                }

            } catch (error) {
                errores++;
                console.error(`Error fatal registrando fila ${index}:`, error);
            }

            await new Promise(resolve => setTimeout(resolve, 300));
        }

        Swal.fire({
            icon: exitosos > 0 && errores === 0 ? 'success' : errores > 0 ? 'warning' : 'error',
            title: 'Proceso completado',
            html: `
                <p><strong>Exitosas:</strong> ${exitosos}</p>
                <p><strong>Errores:</strong> ${errores}</p>
            `,
            confirmButtonText: 'OK'
        });
    });

    console.log('✅ register.js cargado correctamente');
});