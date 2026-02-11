// serverData.js - VERSIÓN COMPLETA
function getServerData() {
    const el = document.getElementById('server-data');
    if (!el) {
        console.warn('⚠️ No se encontró #server-data');
        return {
            interest: null,
            selectedQB: '',
            accountStats: [],
            batchId: '',
            currency: '$',
            bankName: ''
        };
    }

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
        selectedQB: parseAttr('data-selected-qb') || '',
        accountStats: parseAttr('data-account-stats') || [],
        batchId: parseAttr('data-batch-id') || '',
        currency: parseAttr('data-currency') || '$',
        bankName: parseAttr('data-bankname') || ''
    };
}