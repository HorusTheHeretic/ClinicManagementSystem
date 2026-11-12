import { useEffect, useState } from 'react';
import { api, qs } from '../api.js';
import { DAYS, fmtDateTime, fmtTime, label, peso } from '../utils.js';
import { ErrorNote, Field, Loading, useUi } from './ui.jsx';

/** Type a name, number or phone; pick one patient. */
export function PatientPicker({ value, onChange }) {
  const [text, setText] = useState('');
  const [results, setResults] = useState([]);

  useEffect(() => {
    if (text.trim().length < 2) { setResults([]); return undefined; }
    const t = setTimeout(async () => {
      try {
        const res = await api.get(`/api/patients${qs({ search: text.trim(), pageSize: 8 })}`);
        setResults(res.items);
      } catch { setResults([]); }
    }, 250);
    return () => clearTimeout(t);
  }, [text]);

  if (value) {
    return (
      <div className="field">
        <span className="field-label">Patient</span>
        <div className="chosen">
          <span><strong>{value.firstName} {value.lastName}</strong> <span className="muted">{value.patientNo}</span></span>
          <button type="button" className="link-btn" onClick={() => onChange(null)}>Change</button>
        </div>
      </div>
    );
  }
  return (
    <div className="field">
      <span className="field-label">Patient</span>
      <input type="search" placeholder="Search by name, patient number or phone" value={text}
        onChange={(e) => setText(e.target.value)} aria-label="Search patients" autoFocus />
      {results.length > 0 && (
        <div className="picker-results">
          {results.map((p) => (
            <button type="button" key={p.id} onClick={() => onChange(p)}>
              <strong>{p.firstName} {p.lastName}</strong> <span className="muted">{p.patientNo} · {p.age} y/o · {p.contactNumber}</span>
            </button>
          ))}
        </div>
      )}
      {text.trim().length >= 2 && results.length === 0 && <span className="field-hint">No match yet. Register the patient first if they are new.</span>}
    </div>
  );
}

/** Time slots for one doctor on one day. Booked and past slots are disabled. */
export function SlotPicker({ doctorId, date, value, onChange, ignoreId }) {
  const [state, setState] = useState({ loading: false, slots: [], error: null });

  useEffect(() => {
    if (!doctorId || !date) { setState({ loading: false, slots: [], error: null }); return undefined; }
    let live = true;
    setState((s) => ({ ...s, loading: true, error: null }));
    api.get(`/api/appointments/availability${qs({ doctorId, date, ignoreAppointmentId: ignoreId })}`)
      .then((slots) => live && setState({ loading: false, slots, error: null }))
      .catch((error) => live && setState({ loading: false, slots: [], error }));
    return () => { live = false; };
  }, [doctorId, date, ignoreId]);

  if (!doctorId || !date) return <p className="muted small-text">Choose a doctor and a date to see open times.</p>;
  if (state.loading) return <Loading text="Checking the schedule..." />;
  if (state.error) return <ErrorNote error={state.error} />;
  if (state.slots.length === 0) return <div className="notice warn">This doctor does not see patients on that day. Pick another date.</div>;
  if (!state.slots.some((s) => s.available)) return <div className="notice warn">Every slot on this day is taken or already past. Pick another date.</div>;
  return (
    <div className="slots" role="group" aria-label="Available times">
      {state.slots.map((s) => (
        <button type="button" key={s.start} className="slot" disabled={!s.available}
          aria-pressed={value === s.start} onClick={() => onChange(s.start)}>
          {fmtTime(s.start)}
        </button>
      ))}
    </div>
  );
}

/** Working days, hours, slot length, fee. Used by admins (any doctor) and by doctors (themselves). */
export function ScheduleForm({ doctorId, isAdmin, onSaved }) {
  const { toast } = useUi();
  const [form, setForm] = useState(null);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    api.get(`/api/doctors/${doctorId}`).then((d) => setForm({
      name: d.fullName, specialty: d.specialty, days: d.workDays.split(',').filter(Boolean).map(Number),
      startTime: d.startTime, endTime: d.endTime, slotMinutes: d.slotMinutes, consultationFee: d.consultationFee,
    })).catch((e) => setError(e.message));
  }, [doctorId]);

  if (!form) return error ? <div className="notice error">{error}</div> : <Loading />;

  const toggle = (n) => setForm((f) => ({ ...f, days: f.days.includes(n) ? f.days.filter((x) => x !== n) : [...f.days, n] }));
  const submit = async (e) => {
    e.preventDefault();
    setBusy(true); setError('');
    try {
      await api.put(`/api/doctors/${doctorId}`, {
        specialty: form.specialty, workDays: form.days.join(','), startTime: form.startTime, endTime: form.endTime,
        slotMinutes: Number(form.slotMinutes), consultationFee: Number(form.consultationFee),
      });
      toast('Schedule saved.');
      onSaved?.();
    } catch (err) { setError(err.message); } finally { setBusy(false); }
  };

  return (
    <form onSubmit={submit}>
      {error && <div className="notice error" role="alert">{error}</div>}
      <Field label="Specialty">
        <input type="text" required value={form.specialty} onChange={(e) => setForm({ ...form, specialty: e.target.value })} />
      </Field>
      <div className="field">
        <span className="field-label">Working days</span>
        <div className="days">
          {DAYS.map((d) => (
            <button type="button" key={d.n} className="day" aria-pressed={form.days.includes(d.n)} onClick={() => toggle(d.n)}>{d.short}</button>
          ))}
        </div>
      </div>
      <div className="grid3">
        <Field label="Starts"><input type="time" required value={form.startTime} onChange={(e) => setForm({ ...form, startTime: e.target.value })} /></Field>
        <Field label="Ends"><input type="time" required value={form.endTime} onChange={(e) => setForm({ ...form, endTime: e.target.value })} /></Field>
        <Field label="Minutes per visit">
          <select value={form.slotMinutes} onChange={(e) => setForm({ ...form, slotMinutes: e.target.value })}>
            {[10, 15, 20, 30, 45, 60].map((m) => <option key={m} value={m}>{m}</option>)}
          </select>
        </Field>
      </div>
      <Field label="Consultation fee" hint={isAdmin ? undefined : 'Only an administrator can change the fee.'}>
        <input type="number" min="0" step="0.01" value={form.consultationFee} disabled={!isAdmin}
          onChange={(e) => setForm({ ...form, consultationFee: e.target.value })} />
      </Field>
      <div className="actions"><button className="btn primary" disabled={busy}>Save schedule</button></div>
    </form>
  );
}

/** Printable receipt. The .print-area class is what the print stylesheet keeps. */
export function Receipt({ r }) {
  return (
    <div className="print-area receipt">
      <h3>{r.clinicName}</h3>
      <p className="center">{r.clinicAddress}</p>
      <hr />
      <table><tbody>
        <tr><td>Receipt</td><td>{r.receiptNo}</td></tr>
        <tr><td>Invoice</td><td>{r.invoiceNo}</td></tr>
        <tr><td>Date</td><td>{fmtDateTime(r.paidAt)}</td></tr>
        <tr><td>Patient</td><td>{r.patientName} ({r.patientNo})</td></tr>
        <tr><td>Doctor</td><td>{r.doctorName}</td></tr>
      </tbody></table>
      <hr />
      <table><tbody>
        {r.items.map((i, n) => (
          <tr key={n}><td>{i.description}{i.quantity > 1 ? ` x${i.quantity}` : ''}</td><td>{peso(i.lineTotal)}</td></tr>
        ))}
        <tr className="strong"><td>Total</td><td>{peso(r.invoiceTotal)}</td></tr>
      </tbody></table>
      <hr />
      <table><tbody>
        <tr><td>Paid by {label(r.method)}{r.reference ? ` (${r.reference})` : ''}</td><td>{peso(r.amountTendered)}</td></tr>
        {r.changeGiven > 0 && <tr><td>Change</td><td>{peso(r.changeGiven)}</td></tr>}
        <tr><td>Applied to invoice</td><td>{peso(r.amountApplied)}</td></tr>
        <tr className="strong"><td>{r.balanceAfter > 0 ? 'Balance remaining' : 'Status'}</td><td>{r.balanceAfter > 0 ? peso(r.balanceAfter) : 'Paid in full'}</td></tr>
      </tbody></table>
      <hr />
      <p className="center">Received by {r.cashierName}. Thank you.</p>
    </div>
  );
}

