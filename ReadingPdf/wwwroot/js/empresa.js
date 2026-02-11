// empresa.js - Gestión de empresa para cargos

/* -------------------------
   Inicialización
------------------------- */
function initEmpresa() {
    console.log('🏢 Inicializando empresa.js...');

    const empresaSelect = document.getElementById('empresa-select');
    if (!empresaSelect) {
        console.warn('⚠️ No se encontró #empresa-select');
        return;
    }

    // Cargar empresas disponibles
    loadEmpresas();

    // Listener para cambio de empresa
    empresaSelect.addEventListener('change', handleEmpresaChange);

    console.log('✅ Empresa.js configurado');
}

/* -------------------------
   Cargar lista de empresas
------------------------- */
async function loadEmpresas() {
    try {
        const response = await fetch('/api/empresas');
        const data = await response.json();

        if (data.success && data.empresas) {
            populateEmpresaSelect(data.empresas);
        }
    } catch (error) {
        console.error('❌ Error cargando empresas:', error);
    }
}

/* -------------------------
   Poblar select de empresas
------------------------- */
function populateEmpresaSelect(empresas) {
    const select = document.getElementById('empresa-select');
    if (!select) return;

    select.innerHTML = '<option value="">-- Seleccionar empresa --</option>';

    empresas.forEach(empresa => {
        const option = document.createElement('option');
        option.value = empresa.id;
        option.textContent = empresa.nombre;
        select.appendChild(option);
    });
}

/* -------------------------
   Handler para cambio de empresa
------------------------- */
async function handleEmpresaChange(event) {
    const empresaId = event.target.value;

    if (!empresaId) {
        console.log('ℹ️ Empresa deseleccionada');
        return;
    }

    console.log('🔄 Cambiando a empresa:', empresaId);

    try {
        const response = await fetch('/api/set-empresa', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({ empresaId })
        });

        const data = await response.json();

        if (data.success) {
            console.log('✅ Empresa cambiada exitosamente');
            // Recargar la página para actualizar los datos
            window.location.reload();
        } else {
            console.error('❌ Error al cambiar empresa:', data.message);
            alert('Error al cambiar de empresa: ' + data.message);
        }
    } catch (error) {
        console.error('❌ Error en la petición:', error);
        alert('Error de conexión al cambiar de empresa');
    }
}