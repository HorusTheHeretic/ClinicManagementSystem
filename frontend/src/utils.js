// Dates from the API are clinic local time without a zone ("2026-10-31T09:30:00"), so the browser shows them as-is.
const parse = (s) => (s instanceof Date ? s : new Date(s));

export const peso = (n) => new Intl.NumberFormat('en-PH', { style: 'currency', currency: 'PHP' }).format(n ?? 0);
export const fmtDate = (s) => (s ? parse(s).toLocaleDateString('en-PH', { year: 'numeric', month: 'short', day: 'numeric' }) : '-');
export const fmtTime = (s) => (s ? parse(s).toLocaleTimeString('en-PH', { hour: 'numeric', minute: '2-digit' }) : '-');
export const fmtDateTime = (s) => (s ? `${fmtDate(s)}, ${fmtTime(s)}` : '-');
export const fmtWeekday = (s) => parse(s).toLocaleDateString('en-PH', { weekday: 'short' });

/** yyyy-mm-dd in the browser's local zone, for <input type="date"> and query strings. */
export const toDateInput = (d = new Date()) => {
  const x = parse(d);
  const m = String(x.getMonth() + 1).padStart(2, '0');
  const day = String(x.getDate()).padStart(2, '0');
  return `${x.getFullYear()}-${m}-${day}`;
};
export const addDays = (dateStr, n) => {
  const [y, m, d] = dateStr.split('-').map(Number);
  return toDateInput(new Date(y, m - 1, d + n));
};
/** Local "yyyy-MM-ddTHH:mm:ss" with no zone, which is what the API expects for appointment times. */
export const toLocalIso = (d) => `${toDateInput(d)}T${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}:00`;

export const queueNo = (n) => (n == null ? '-' : String(n).padStart(3, '0'));
export const minutesSince = (s) => Math.max(0, Math.round((Date.now() - parse(s).getTime()) / 60000));

export const STATUS_LABEL = {
  Scheduled: 'Scheduled', Confirmed: 'Confirmed', Waiting: 'Waiting', InConsultation: 'In consultation',
  Completed: 'Completed', Cancelled: 'Cancelled', NoShow: 'No-show',
  Unpaid: 'Unpaid', PartiallyPaid: 'Partly paid', Paid: 'Paid',
  Pending: 'Pending approval', Active: 'Active', Disabled: 'Disabled',
  EWallet: 'E-wallet',
};
export const label = (s) => STATUS_LABEL[s] ?? s;

export const DAYS = [
  { n: 1, short: 'Mon' }, { n: 2, short: 'Tue' }, { n: 3, short: 'Wed' }, { n: 4, short: 'Thu' },
  { n: 5, short: 'Fri' }, { n: 6, short: 'Sat' }, { n: 0, short: 'Sun' },
];

export function downloadCsv(filename, columns, rows) {
  const esc = (v) => `"${String(v ?? '').replace(/"/g, '""')}"`;
  const lines = [columns.map((c) => esc(c.label)).join(','), ...rows.map((r) => r.map(esc).join(','))];
  const blob = new Blob(['\ufeff' + lines.join('\r\n')], { type: 'text/csv;charset=utf-8' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
}
