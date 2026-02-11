// Agregar este código en un nuevo archivo: ~/js/multiSelection.js

(function () {
    'use strict';

    let isMouseDown = false;
    let startRowIndex = null;
    let lastClickedIndex = null;
    let dragSelectionMode = null; // 'select' o 'deselect'

    /**
     * Inicializa el sistema de selección múltiple
     */
    function initMultiSelection() {
        const $table = $('#resultados-table tbody');

        // Prevenir la selección de texto durante el arrastre
        $table.on('mousedown', 'tr', function (e) {
            // Si se presionó Ctrl/Cmd, no iniciar arrastre
            if (e.ctrlKey || e.metaKey) {
                return;
            }

            // Si se hizo clic en un input o botón, no iniciar arrastre
            if ($(e.target).is('input, button, select, a')) {
                return;
            }

            e.preventDefault();
            isMouseDown = true;
            startRowIndex = $(this).index();

            const $checkbox = $(this).find('.row-check');
            // Determinar si vamos a seleccionar o deseleccionar
            dragSelectionMode = !$checkbox.prop('checked') ? 'select' : 'deselect';

            toggleRowSelection($(this), dragSelectionMode === 'select');
        });

        // Selección mientras se arrastra
        $table.on('mouseenter', 'tr', function () {
            if (!isMouseDown || startRowIndex === null) return;

            toggleRowSelection($(this), dragSelectionMode === 'select');
        });

        // Finalizar selección
        $(document).on('mouseup', function () {
            isMouseDown = false;
            startRowIndex = null;
            dragSelectionMode = null;
        });

        // Selección con Ctrl + Click (agregar/quitar individual)
        $table.on('click', 'tr', function (e) {
            // Si se hizo clic en un input, botón o select, ignorar
            if ($(e.target).is('input, button, select, a, .empresa-actions, .empresa-actions *')) {
                return;
            }

            const $checkbox = $(this).find('.row-check');
            const currentIndex = $(this).index();

            // Ctrl/Cmd + Click: alternar selección individual
            if (e.ctrlKey || e.metaKey) {
                e.preventDefault();
                toggleRowSelection($(this), !$checkbox.prop('checked'));
                lastClickedIndex = currentIndex;
                return;
            }

            // Shift + Click: seleccionar rango
            if (e.shiftKey && lastClickedIndex !== null) {
                e.preventDefault();
                selectRange(lastClickedIndex, currentIndex);
                return;
            }

            // Click normal en el checkbox se maneja automáticamente
            if (!$(e.target).is('.row-check')) {
                // Si hicieron clic en la fila (no en el checkbox), alternar
                toggleRowSelection($(this), !$checkbox.prop('checked'));
            }

            lastClickedIndex = currentIndex;
        });

        // Mejorar el comportamiento del checkbox
        $table.on('change', '.row-check', function (e) {
            e.stopPropagation();
            const $row = $(this).closest('tr');
            const isChecked = $(this).prop('checked');
            updateRowVisualState($row, isChecked);
            updateSelectionSummary();
            lastClickedIndex = $row.index();
        });

        console.log('✅ Sistema de selección múltiple inicializado');
    }

    /**
     * Alterna la selección de una fila
     */
    function toggleRowSelection($row, shouldSelect) {
        const $checkbox = $row.find('.row-check');
        $checkbox.prop('checked', shouldSelect);
        updateRowVisualState($row, shouldSelect);
        updateSelectionSummary();
    }

    /**
     * Actualiza el estado visual de una fila
     */
    function updateRowVisualState($row, isSelected) {
        if (isSelected) {
            $row.addClass('table-active');
        } else {
            $row.removeClass('table-active');
        }
    }

    /**
     * Selecciona un rango de filas (Shift + Click)
     */
    function selectRange(startIndex, endIndex) {
        const start = Math.min(startIndex, endIndex);
        const end = Math.max(startIndex, endIndex);

        $('#resultados-table tbody tr').each(function (index) {
            if (index >= start && index <= end) {
                toggleRowSelection($(this), true);
            }
        });
    }

    /**
     * Actualiza el resumen de selección
     */
    function updateSelectionSummary() {
        // Esta función ya existe en selection.js, solo la llamamos
        if (typeof window.updateSelectionSummary === 'function') {
            window.updateSelectionSummary();
        }
    }

    /**
     * Añadir indicador visual de que se puede arrastrar
     */
    function addDragCursor() {
        const style = document.createElement('style');
        style.textContent = `
            #resultados-table tbody tr {
                cursor: pointer;
                user-select: none;
            }
            
            #resultados-table tbody tr:hover {
                background-color: rgba(0, 123, 255, 0.05);
            }
            
            #resultados-table tbody tr.table-active {
                background-color: rgba(0, 123, 255, 0.1) !important;
            }
            
            /* Mantener el color verde para filas con empresa */
            #resultados-table tbody tr.has-empresa td {
                background-color: #c1e9c7 !important;
            }
            
            #resultados-table tbody tr.has-empresa.table-active td {
                background-color: #a8d5af !important;
            }
        `;
        document.head.appendChild(style);
    }

    // Inicializar cuando el documento esté listo
    $(document).ready(function () {
        initMultiSelection();
        addDragCursor();
    });

})();