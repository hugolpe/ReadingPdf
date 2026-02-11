// contextMenu.js
function initContextMenu() {
    console.log('🎯 Inicializando menú contextual...');

    const contextMenu = document.getElementById('context-menu');
    const table = document.getElementById('resultados-table');

    if (!contextMenu) {
        console.error('❌ No se encontró #context-menu');
        return;
    }

    if (!table) {
        console.error('❌ No se encontró #resultados-table');
        return;
    }

    console.log('✅ Elementos encontrados para menú contextual');

    let currentRow = null;

    // Click derecho
    table.addEventListener('contextmenu', (e) => {
        console.log('🖱️ Click derecho detectado');

        const row = e.target.closest('tbody tr');

        if (!row) {
            console.log('⚠️ No hay fila');
            return;
        }

        e.preventDefault();

        // 🔍 DIAGNÓSTICO
        console.log('=== DIAGNÓSTICO ===');
        console.log('clientX:', e.clientX, 'clientY:', e.clientY);
        console.log('pageX:', e.pageX, 'pageY:', e.pageY);
        console.log('==================');

        // Limpiar highlights
        document.querySelectorAll('tr.context-active').forEach(r => {
            r.classList.remove('context-active');
        });

        currentRow = row;
        row.classList.add('context-active');

        // Posicionar menú
        contextMenu.style.left = `${e.clientX}px`;
        contextMenu.style.top = `${e.clientY}px`;
        contextMenu.style.display = 'block';

        // Actualizar select/deselect
        const checkbox = row.querySelector('.row-check');
        const selectItem = contextMenu.querySelector('[data-action="select"]');
        const deselectItem = contextMenu.querySelector('[data-action="deselect"]');

        if (checkbox && checkbox.checked) {
            if (selectItem) selectItem.style.display = 'none';
            if (deselectItem) deselectItem.style.display = 'flex';
        } else {
            if (selectItem) selectItem.style.display = 'flex';
            if (deselectItem) deselectItem.style.display = 'none';
        }
    });

    // Cerrar con click fuera
    document.addEventListener('click', (e) => {
        if (!contextMenu.contains(e.target)) {
            contextMenu.style.display = 'none';
            if (currentRow) {
                currentRow.classList.remove('context-active');
            }
        }
    });

    // Cerrar con ESC
    document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape') {
            contextMenu.style.display = 'none';
            if (currentRow) {
                currentRow.classList.remove('context-active');
            }
        }
    });

    // Manejar acciones
    contextMenu.addEventListener('click', async (e) => {
        const li = e.target.closest('li');
        if (!li) return;

        const action = li.dataset.action;
        console.log('🎬 Acción:', action);

        if (!action || !currentRow) return;

        contextMenu.style.display = 'none';
        currentRow.classList.remove('context-active');

        switch (action) {
            case 'register':
                const registerBtn = currentRow.querySelector('.registrar-btn');
                if (registerBtn) registerBtn.click();
                break;

            case 'view':
                const viewBtn = currentRow.querySelector('.qb-view-btn');
                if (viewBtn) viewBtn.click();
                break;

            case 'edit':
                const empresaInput = currentRow.querySelector('.empresa-input');
                if (empresaInput) {
                    empresaInput.focus();
                    empresaInput.select();
                }
                break;

            case 'copy-description':
                const descripcionCell = currentRow.querySelector('.descripcion-cell');
                if (descripcionCell) {
                    const text = descripcionCell.textContent.trim();
                    try {
                        await navigator.clipboard.writeText(text);
                        showToast('✅ Descripción copiada');
                    } catch (err) {
                        const textarea = document.createElement('textarea');
                        textarea.value = text;
                        textarea.style.position = 'fixed';
                        textarea.style.opacity = '0';
                        document.body.appendChild(textarea);
                        textarea.select();
                        document.execCommand('copy');
                        document.body.removeChild(textarea);
                        showToast('✅ Descripción copiada');
                    }
                }
                break;

            case 'select':
                const checkbox = currentRow.querySelector('.row-check');
                if (checkbox && !checkbox.checked) {
                    checkbox.checked = true;
                    checkbox.dispatchEvent(new Event('change', { bubbles: true }));
                }
                break;

            case 'deselect':
                const checkboxUncheck = currentRow.querySelector('.row-check');
                if (checkboxUncheck && checkboxUncheck.checked) {
                    checkboxUncheck.checked = false;
                    checkboxUncheck.dispatchEvent(new Event('change', { bubbles: true }));
                }
                break;
        }
    });

    console.log('✅ Menú contextual configurado');
}

function showToast(message) {
    const toast = document.createElement('div');
    toast.textContent = message;
    toast.style.cssText = `
        position: fixed;
        bottom: 20px;
        right: 20px;
        background: #28a745;
        color: white;
        padding: 12px 24px;
        border-radius: 6px;
        box-shadow: 0 4px 12px rgba(0,0,0,0.15);
        z-index: 10001;
        font-family: Arial, sans-serif;
        font-size: 14px;
    `;

    document.body.appendChild(toast);

    setTimeout(() => {
        toast.style.opacity = '0';
        toast.style.transition = 'opacity 0.3s';
        setTimeout(() => toast.remove(), 300);
    }, 2000);
}