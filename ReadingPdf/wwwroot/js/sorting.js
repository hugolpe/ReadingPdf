// sorting.js
export function attachSortingHandlers(table) {
    if (!table) return;

    table.querySelectorAll('thead th.sortable').forEach(th => {
        if (th._attached) return;

        th.addEventListener('click', () => {
            const col = +th.dataset.col;
            const type = th.dataset.type || 'string';
            const tbody = table.querySelector('tbody');
            const rows = [...tbody.querySelectorAll('tr')];
            const asc = !th.classList.contains('asc');

            table.querySelectorAll('th').forEach(h => h.classList.remove('asc', 'desc'));
            th.classList.add(asc ? 'asc' : 'desc');

            rows.sort((a, b) => {
                const A = a.cells[col]?.dataset.sortValue || a.cells[col]?.innerText;
                const B = b.cells[col]?.dataset.sortValue || b.cells[col]?.innerText;
                if (type === 'number') return (parseFloat(A) - parseFloat(B)) * (asc ? 1 : -1);
                if (type === 'date') return (new Date(A) - new Date(B)) * (asc ? 1 : -1);
                return A.localeCompare(B) * (asc ? 1 : -1);
            });

            rows.forEach(r => tbody.appendChild(r));
        });

        th._attached = true;
    });
}
