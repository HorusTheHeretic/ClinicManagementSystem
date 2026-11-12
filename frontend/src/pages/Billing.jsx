import { useState } from 'react';
import { api, qs } from '../api.js';
import { Badge, Empty, ErrorNote, Field, Loading, Modal, PageHead, useAsync, useUi } from '../components/ui.jsx';
import { Receipt } from '../components/shared.jsx';
import { fmtDate, fmtDateTime, label, peso } from '../utils.js';

function ReceiptModal({ paymentId, receipt, onClose }) {
  const loaded = useAsync(() => (receipt ? Promise.resolve(receipt) : api.get(`/api/billing/payments/${paymentId}/receipt`)), [paymentId]);
  const r = receipt ?? loaded.data;
  return (
    <Modal title="Receipt" onClose={onClose} small>
      {!r ? (loaded.error ? <ErrorNote error={loaded.error} /> : <Loading />) : (
        <>
          <Receipt r={r} />
          <div className="actions no-print">
            <button className="btn" onClick={onClose}>Close</button>
            <button className="btn primary" onClick={() => window.print()}>Print receipt</button>
          </div>
        </>
      )}
    </Modal>
  );
}

function PayModal({ invoiceId, onClose, onChanged }) {
  const { toast } = useUi();
  const { data: inv, loading, error, reload } = useAsync(() => api.get(`/api/billing/invoices/${invoiceId}`), [invoiceId]);
  const [amount, setAmount] = useState('');
  const [method, setMethod] = useState('Cash');
  const [reference, setReference] = useState('');
  const [err, setErr] = useState('');
  const [busy, setBusy] = useState(false);
  const [receipt, setReceipt] = useState(null);
  const [viewing, setViewing] = useState(null);

  const balance = inv?.balance ?? 0;
  const entered = amount === '' ? NaN : Number(amount);
  const value = Number.isNaN(entered) ? balance : entered;
  const feedback = value >= balance
    ? { text: `Change to give back: ${peso(value - balance)}`, kind: 'ok' }
    : { text: `Balance remaining after this payment: ${peso(balance - value)}`, kind: 'warn' };

  const submit = async (e) => {
    e.preventDefault();
    setBusy(true); setErr('');
    try {
      const r = await api.post(`/api/billing/invoices/${invoiceId}/pay`, { amount: value, method, reference: reference || null });
      toast(r.balanceAfter > 0 ? `Partial payment recorded. ${peso(r.balanceAfter)} still due.` : 'Payment recorded. Invoice paid in full.');
      setReceipt(r);
      onChanged();
    } catch (ex) { setErr(ex.message); reload(true); } finally { setBusy(false); }
  };

  if (receipt) return <ReceiptModal receipt={receipt} onClose={onClose} />;
  if (viewing) return <ReceiptModal paymentId={viewing} onClose={() => setViewing(null)} />;

  return (
    <Modal title={inv ? `Invoice ${inv.invoiceNo}` : 'Invoice'} onClose={onClose}>
      {loading && !inv && <Loading />}
      {error && <ErrorNote error={error} retry={reload} />}
      {inv && (
        <>
          <p>{inv.patientName} <span className="muted">{inv.patientNo} · {inv.doctorName} · {fmtDate(inv.createdAt)}</span></p>
          <div className="table-wrap" style={{ marginBottom: 14 }}>
            <table className="table">
              <thead><tr><th>Item</th><th className="num">Qty</th><th className="num">Price</th><th className="num">Amount</th></tr></thead>
              <tbody>
                {inv.items.map((i, n) => <tr key={n}><td>{i.description}</td><td className="num">{i.quantity}</td><td className="num">{peso(i.unitPrice)}</td><td className="num">{peso(i.lineTotal)}</td></tr>)}
                <tr><td colSpan="3" className="right"><strong>Total</strong></td><td className="num"><strong>{peso(inv.total)}</strong></td></tr>
                <tr><td colSpan="3" className="right">Paid so far</td><td className="num">{peso(inv.amountPaid)}</td></tr>
                <tr><td colSpan="3" className="right"><strong>Balance due</strong></td><td className="num"><strong>{peso(inv.balance)}</strong></td></tr>
              </tbody>
            </table>
          </div>

          {inv.payments.length > 0 && (
            <div style={{ marginBottom: 14 }}>
              <h3 style={{ marginBottom: 4 }}>Payments</h3>
              {inv.payments.map((p) => (
                <div className="row" key={p.id} style={{ padding: '4px 0' }}>
                  <span>{fmtDateTime(p.paidAt)} · {label(p.method)} · {peso(p.amountApplied)}</span><span className="spacer" />
                  <button className="link-btn" onClick={() => setViewing(p.id)}>Receipt {p.receiptNo}</button>
                </div>
              ))}
            </div>
          )}

          {inv.balance > 0 ? (
            <form onSubmit={submit}>
              {err && <div className="notice error" role="alert">{err}</div>}
              <div className="grid2">
                <Field label="Amount received"><input type="number" step="0.01" min="0.01" required value={amount} placeholder={balance.toFixed(2)} onChange={(e) => setAmount(e.target.value)} /></Field>
                <Field label="Payment method">
                  <select value={method} onChange={(e) => setMethod(e.target.value)}>
                    <option value="Cash">Cash</option><option value="Card">Card</option><option value="EWallet">E-wallet</option>
                  </select>
                </Field>
              </div>
              {method !== 'Cash' && <Field label="Reference number"><input type="text" value={reference} onChange={(e) => setReference(e.target.value)} /></Field>}
              <div className={`notice ${feedback.kind}`}>{feedback.text}</div>
              <div className="actions">
                <button type="button" className="btn" onClick={onClose}>Close</button>
                <button className="btn primary" disabled={busy || value <= 0}>Record payment</button>
              </div>
            </form>
          ) : <div className="notice ok">This invoice is paid in full.</div>}
        </>
      )}
    </Modal>
  );
}

export default function Billing() {
  const [tab, setTab] = useState('due');
  const [search, setSearch] = useState('');
  const [query, setQuery] = useState('');
  const [openId, setOpenId] = useState(null);
  const { data, loading, error, reload } = useAsync(() => api.get(`/api/billing/invoices${qs({ search: query })}`), [query]);

  const rows = (data ?? []).filter((i) => (tab === 'due' ? i.status !== 'Paid' : tab === 'paid' ? i.status === 'Paid' : true));
  const dueTotal = (data ?? []).filter((i) => i.status !== 'Paid').reduce((s, i) => s + i.balance, 0);

  return (
    <>
      <PageHead title="Billing" sub={data ? `${peso(dueTotal)} still to collect` : undefined} />
      <form className="filters" onSubmit={(e) => { e.preventDefault(); setQuery(search.trim()); }}>
        <Field label="Find an invoice"><input type="search" value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Patient name, patient no. or invoice no." style={{ minWidth: 320 }} /></Field>
        <button className="btn">Search</button>
      </form>
      <div className="tabs" role="tablist">
        {[['due', 'To collect'], ['paid', 'Paid'], ['all', 'All']].map(([k, t]) => <button key={k} role="tab" aria-selected={tab === k} onClick={() => setTab(k)}>{t}</button>)}
      </div>
      {error && <ErrorNote error={error} retry={reload} />}
      {loading && !data ? <Loading /> : rows.length === 0 ? <div className="panel"><Empty title={tab === 'due' ? 'Nothing to collect' : 'No invoices found'}>Invoices appear here as soon as a doctor completes a consultation.</Empty></div> : (
        <div className="table-wrap">
          <table className="table">
            <thead><tr><th>Invoice</th><th>Patient</th><th>Date</th><th className="num">Total</th><th className="num">Paid</th><th className="num">Balance</th><th>Status</th><th /></tr></thead>
            <tbody>
              {rows.map((i) => (
                <tr key={i.id}>
                  <td>{i.invoiceNo}</td>
                  <td>{i.patientName}<span className="sub">{i.patientNo} · {i.doctorName}</span></td>
                  <td>{fmtDate(i.createdAt)}</td>
                  <td className="num">{peso(i.total)}</td><td className="num">{peso(i.amountPaid)}</td><td className="num">{peso(i.balance)}</td>
                  <td><Badge value={i.status} /></td>
                  <td className="actions-cell"><button className={`btn small ${i.status === 'Paid' ? '' : 'primary'}`} onClick={() => setOpenId(i.id)}>{i.status === 'Paid' ? 'View' : 'Collect payment'}</button></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {openId && <PayModal invoiceId={openId} onClose={() => setOpenId(null)} onChanged={() => reload(true)} />}
    </>
  );
}
