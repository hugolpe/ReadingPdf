// utils.js - SIN getServerData (ya está en serverData.js)
function normalizeText(s) {
    if (!s) return '';
    const noDiacritics = s.normalize('NFD').replace(/[\u0300-\u036f]/g, '');
    const cleaned = noDiacritics.replace(/[^\p{L}\p{N}\s]/gu, ' ');
    return cleaned.replace(/\s+/g, ' ').trim().toUpperCase();
}

function formatCurrency(value) {
    const sd = getServerData();
    const symbol = sd.currency || '$';
    const num = Number(value) || 0;
    const sign = num < 0 ? '-' : '';
    const abs = Math.abs(num);
    const formatted = abs.toLocaleString(undefined, {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2
    });
    return sign + symbol + formatted;
}