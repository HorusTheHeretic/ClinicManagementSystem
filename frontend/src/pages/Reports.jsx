import { useState } from 'react';
import { api, qs } from '../api.js';
import { Empty, ErrorNote, Field, PageHead } from '../components/ui.jsx';
import { addDays, downloadCsv, fmtDate, peso, toDateInput } from '../utils.js';

const TYPES = [
  ['appointments', 'Appointments by doctor'],
  ['revenue', 'Revenue'],
  ['patients', 'New patients'],
  ['diagnoses', 'Diagnoses seen'],
];

const cell = (kind, v) => (kind === 'money' ? peso(v) : kind === 'date' && v ? fmtDate(`${v}T00:00:00`) : v ?? '');

export default function Reports() {
  const [type, setType] = useState('appointments');
  const [from, setFrom] = useState(addDays(toDateInput(), -29));
  const [to, setTo] = useState(toDateInput());
  const [report, setReport] = useState(null);
  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(false);

  const generate = async (e) => {
    e.preventDefault();
    setBusy(true); setError(null);
    try { setReport(await api.get(`/api/reports/${type}${qs({ from, to })}`)); }
    catch (err) { setError(err); setReport(null); } finally { setBusy(false); }
  };

  const exportCsv = () => downloadCsv(`${type}-${from}-to-${to}.csv`, report.columns, report.rows.map((r) => r.map((v, i) => cell(report.columns[i].kind, v))));

  return (
    <>
      <PageHead title="Reports" sub="Choose a report and a date range, then print it or save it as a spreadsheet file." />
      <form className="filters no-print" onSubmit={generate}>
        <Field label="Report"><select value={type} onChange={(e) => setType(e.target.value)}>{TYPES.map(([k, t]) => <option key={k} value={k}>{t}</option>)}</select></Field>
        <Field label="From"><input type="date" required value={from} max={to} onChange={(e) => setFrom(e.target.value)} /></Field>
        <Field label="To"><input type="date" required value={to} min={from} onChange={(e) => setTo(e.target.value)} /></Field>
        <button className="btn primary" disabled={busy}>{busy ? 'Generating...' : 'Generate report'}</button>
      </form>

      {error && <ErrorNote error={error} />}
      {!report && !error && <div className="panel"><Empty title="No report yet">Pick a report type and dates, then select Generate report.</Empty></div>}
      {report && (
        <div className="print-area">
          <div className="page-head">
            <div><h2>{report.title}</h2><p className="muted">{fmtDate(report.from)} to {fmtDate(report.to)}</p></div>
            <div className="page-actions no-print">
              <button className="btn" onClick={exportCsv} disabled={report.rows.length === 0}>Save as CSV</button>
              <button className="btn primary" onClick={() => window.print()}>Print</button>
            </div>
          </div>
          <dl className="figures" style={{ marginBottom: 18 }}>
            {report.summary.map((s) => <div className="figure" key={s.label}><dt>{s.label}</dt><dd>{/^[\d,]+\.\d{2}$/.test(s.value) ? peso(Number(s.value.replace(/,/g, ''))) : s.value}</dd></div>)}
          </dl>
          {report.rows.length === 0 ? <div className="panel"><Empty title="No records in this period" /></div> : (
            <div className="table-wrap">
              <table className="table">
                <thead><tr>{report.columns.map((c) => <th key={c.label} className={c.kind === 'number' || c.kind === 'money' ? 'num' : ''}>{c.label}</th>)}</tr></thead>
                <tbody>
                  {report.rows.map((r, n) => (
                    <tr key={n}>{r.map((v, i) => <td key={i} className={report.columns[i].kind === 'number' || report.columns[i].kind === 'money' ? 'num' : ''}>{cell(report.columns[i].kind, v)}</td>)}</tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}
    </>
  );
}
