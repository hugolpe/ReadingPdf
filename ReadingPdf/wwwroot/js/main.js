// main.js - SIN IMPORTS
console.log('🚀 main.js cargado');

document.addEventListener('DOMContentLoaded', () => {
    console.log('📄 DOM cargado');

    const table = document.getElementById('resultados-table');

    if (table) {
        console.log('✅ Tabla encontrada');

        // Llamar funciones que están en otros archivos
        if (typeof initSelection === 'function') initSelection(table);
        if (typeof attachSortingHandlers === 'function') attachSortingHandlers(table);
        if (typeof attachDescripcionHandler === 'function') attachDescripcionHandler();
        if (typeof attachRegisterHandlers === 'function') attachRegisterHandlers();
        if (typeof attachViewHandlers === 'function') attachViewHandlers();
        if (typeof initContextMenu === 'function') initContextMenu();
        if (typeof onTableMouseUp === 'function') onTableMouseUp();

        console.log('✅ Inicialización completa');
    } else {
        console.error('❌ Tabla no encontrada');
    }
});