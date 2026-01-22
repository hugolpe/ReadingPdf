// serverData.js
export function getServerData() {
    const el = document.getElementById('server-data');
    if (!el) return { interest: null, currency: '$', selectedQB: '' };

    function parseAttr(name) {
        try {
            const v = el.getAttribute(name);
            if (v === null || v === undefined) return null;
            return JSON.parse(v);
        } catch {
            return el.getAttribute(name);
        }
    }

    return {
        interest: parseAttr('data-interest'),
        currency: parseAttr('data-currency') || '$',
        selectedQB: parseAttr('data-selected-qb') || ''
    };
}
