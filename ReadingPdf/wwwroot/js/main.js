// main.js
import { initSelection } from './selection.js';
import { attachSortingHandlers } from './sorting.js';
import { attachDescripcionHandler } from './empresa.js';
import { attachRegisterHandlers } from './register.js';
import { attachViewHandlers } from './qbView.js';

document.addEventListener('DOMContentLoaded', () => {
    const table = document.getElementById('resultados-table');

    initSelection(table);        // ✔ selección
    attachSortingHandlers(table);
    attachDescripcionHandler();
    attachRegisterHandlers();    // ✔ register (AHORA SÍ)
    attachViewHandlers();
});
