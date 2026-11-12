import { useEffect, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { api, qs } from '../api.js';
import { useAuth } from '../auth.jsx';
import { Badge, Empty, ErrorNote, Field, Loading, Modal, PageHead, useAsync, useUi } from '../components/ui.jsx';
import { PatientPicker, SlotPicker } from '../components/shared.jsx';
import { fmtDate, fmtTime, toDateInput } from '../utils.js';

function DoctorSelect({ value, onChange, doctors, required = false, any = false }) {
  return (
    <select value={value} onChange={(e) => onChange(e.target.value)} required={required}>
      <option value="">{any ? 'All doctors' : 'Select a doctor'}</option>
      {doctors.map((d) => <option key={d.id} value={d.id}>{d.fullName} ({d.specialty})</option>)}
    </select>
  );
}

function BookModal({ doctors, presetPatient, onClose, onBooked }) {
  const [patient, setPatient] = useState(null);
  const [doctorId, setDoctorId] = useState('');
  const [date, setDate] = useState(toDateInput());
  const [slot, setSlot] = useState('');
  const [reason, setReason] = useState('');
  const [confirmNow, setConfirmNow] = useState(true);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  useEffect(() => { if (presetPatient) api.get(`/api/patients/${presetPatient}`).then(setPatient).catch(() => {}); }, [presetPatient]);
  useEffect(() => { setSlot(''); }, [doctorId, date]);

  const submit = async (e) => {
    e.preventDefault();
    if (!patient) { setError('Choose a patient.'); return; }
    if (!slot) { setError('Pick an available time.'); return; }
    setBusy(true); setError('');
    try {
      await api.post('/api/appointments', { patientId: patient.id, doctorId: Number(doctorId), startsAt: slot, reason: reason || null, confirm: confirmNow });
      onBooked();
    } catch (err) { setError(err.message); setSlot(''); } finally { setBusy(false); }
  };

  return (
    <Modal title="Book appointment" onClose={onClose} wide>
      <form onSubmit={submit}>
        {error && <div className="notice error" role="alert">{error}</div>}
        <PatientPicker value={patient} onChange={setPatient} />
        <div className="grid2">
          <Field label="Doctor"><DoctorSelect value={doctorId} onChange={setDoctorId} doctors={doctors} required /></Field>
          <Field label="Date"><input type="date" required min={toDateInput()} value={date} onChange={(e) => setDate(e.target.value)} /></Field>
        </div>
        <div className="field"><span className="field-label">Time</span>
          <SlotPicker doctorId={doctorId} date={date} value={slot} onChange={setSlot} />
        </div>
        <Field label="Reason for visit (optional)"><input type="text" value={reason} onChange={(e) => setReason(e.target.value)} /></Field>
        <label className="check"><input type="checkbox" checked={confirmNow} onChange={(e) => setConfirmNow(e.target.checked)} /> The patient has confirmed this time</label>
        <div className="actions">
          <button type="button" className="btn" onClick={onClose}>Cancel</button>
          <button className="btn primary" disabled={busy}>Book appointment</button>
        </div>
      </form>
    </Modal>
  );
}

function RescheduleModal({ appt, doctors, onClose, onDone }) {
  const [doctorId, setDoctorId] = useState(String(appt.doctorId));
  const [date, setDate] = useState(toDateInput(appt.startsAt));
  const [slot, setSlot] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  useEffect(() => { setSlot(''); }, [doctorId, date]);

  const submit = async (e) => {
    e.preventDefault();
    if (!slot) { setError('Pick an available time.'); return; }
    setBusy(true); setError('');
    try { await api.put(`/api/appointments/${appt.id}/reschedule`, { startsAt: slot, doctorId: Number(doctorId) }); onDone(); }
    catch (err) { setError(err.message); } finally { setBusy(false); }
  };

  return (
    <Modal title={`Reschedule ${appt.patientName}`} onClose={onClose} wide>
      <form onSubmit={submit}>
        {error && <div className="notice error" role="alert">{error}</div>}
        <p className="muted">Currently {fmtDate(appt.startsAt)} at {fmtTime(appt.startsAt)}. After moving it the appointment needs to be confirmed again.</p>
        <div className="grid2">
          <Field label="Doctor"><DoctorSelect value={doctorId} onChange={setDoctorId} doctors={doctors} required /></Field>
          <Field label="New date"><input type="date" required min={toDateInput()} value={date} onChange={(e) => setDate(e.target.value)} /></Field>
        </div>
        <SlotPicker doctorId={doctorId} date={date} value={slot} onChange={setSlot} ignoreId={appt.id} />
        <div className="actions">
          <button type="button" className="btn" onClick={onClose}>Cancel</button>
          <button className="btn primary" disabled={busy}>Move appointment</button>
        </div>
      </form>
    </Modal>
  );
}

export default function Appointments() {
  const { user } = useAuth();
  const { toast, confirm } = useUi();
  const [params, setParams] = useSearchParams();
  const [date, setDate] = useState(toDateInput());
  const [allDates, setAllDates] = useState(false);
  const [doctorId, setDoctorId] = useState('');
  const [status, setStatus] = useState('');
  const [booking, setBooking] = useState(params.get('patient') !== null);
  const [moving, setMoving] = useState(null);
  const canManage = user.role !== 'Doctor';
  const today = toDateInput();

  const docs = useAsync(() => api.get('/api/doctors'), []);
  const { data, loading, error, reload } = useAsync(
    () => api.get(`/api/appointments${qs({ date: allDates ? '' : date, from: allDates ? today : '', doctorId, status })}`),
    [date, allDates, doctorId, status],
  );
  const doctors = docs.data ?? [];

  const act = async (fn, okMsg) => {
    try { await fn(); toast(okMsg); reload(true); } catch (e) { toast(e.message, 'error'); reload(true); }
  };
  const cancel = async (a) => {
    const ok = await confirm({ title: `Cancel ${a.patientName}'s appointment?`, message: `${fmtDate(a.startsAt)} at ${fmtTime(a.startsAt)} with ${a.doctorName}. The time becomes available again.`, confirmLabel: 'Cancel appointment', cancelLabel: 'Keep appointment', danger: true });
    if (ok) act(() => api.post(`/api/appointments/${a.id}/cancel`, {}), 'Appointment cancelled.');
  };

  return (
    <>
      <PageHead title="Appointments" sub={user.role === 'Doctor' ? 'Your schedule.' : 'Schedule, confirm, reschedule and cancel.'}>
        {canManage && <button className="btn primary" onClick={() => setBooking(true)}>Book appointment</button>}
      </PageHead>

      <div className="filters">
        <Field label="Date"><input type="date" value={date} disabled={allDates} onChange={(e) => setDate(e.target.value)} /></Field>
        <label className="check" style={{ margin: 0, minHeight: 40 }}><input type="checkbox" checked={allDates} onChange={(e) => setAllDates(e.target.checked)} /> All upcoming</label>
        {user.role !== 'Doctor' && <Field label="Doctor"><DoctorSelect any value={doctorId} onChange={setDoctorId} doctors={doctors} /></Field>}
        <Field label="Status">
          <select value={status} onChange={(e) => setStatus(e.target.value)}>
            <option value="">Any status</option>
            {['Scheduled', 'Confirmed', 'Waiting', 'InConsultation', 'Completed', 'Cancelled', 'NoShow'].map((s) => <option key={s} value={s}>{s === 'InConsultation' ? 'In consultation' : s === 'NoShow' ? 'No-show' : s}</option>)}
          </select>
        </Field>
      </div>

      {error && <ErrorNote error={error} retry={reload} />}
      {loading && !data ? <Loading /> : data && (data.length === 0
        ? <div className="panel"><Empty title="No appointments found">Try another date or clear the filters.</Empty></div>
        : (
          <div className="table-wrap">
            <table className="table">
              <thead><tr><th>When</th><th>Patient</th><th>Doctor</th><th>Reason</th><th>Status</th>{canManage || user.role === 'Doctor' ? <th /> : null}</tr></thead>
              <tbody>
                {data.map((a) => {
                  const isToday = toDateInput(a.startsAt) === today;
                  const open = a.status === 'Scheduled' || a.status === 'Confirmed';
                  return (
                    <tr key={a.id}>
                      <td className="nowrap"><strong>{fmtTime(a.startsAt)}</strong>{a.isWalkIn ? <span className="sub">Walk-in</span> : <span className="sub">{fmtDate(a.startsAt)}</span>}</td>
                      <td>{a.patientName}<span className="sub">{a.patientNo}</span></td>
                      <td>{a.doctorName}<span className="sub">{a.specialty}</span></td>
                      <td>{a.reason || <span className="muted">-</span>}</td>
                      <td><Badge value={a.status} />{a.queueNumber != null && <span className="sub">Queue no. {String(a.queueNumber).padStart(3, '0')}</span>}</td>
                      <td className="actions-cell">
                        {canManage && a.status === 'Scheduled' && <button className="btn small primary" onClick={() => act(() => api.post(`/api/appointments/${a.id}/confirm`), 'Appointment confirmed.')}>Confirm</button>}
                        {canManage && a.status === 'Confirmed' && isToday && <button className="btn small primary" onClick={() => act(() => api.post(`/api/queue/check-in/${a.id}`, { priority: 'Normal' }), 'Patient checked in.')}>Check in</button>}
                        {canManage && open && <button className="btn small" onClick={() => setMoving(a)}>Reschedule</button>}
                        {canManage && (open || a.status === 'Waiting') && <button className="btn small danger" onClick={() => cancel(a)}>Cancel</button>}
                        {user.role === 'Doctor' && a.status === 'InConsultation' && <Link className="btn small primary" to={`/consultation/${a.id}`}>Open consultation</Link>}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        ))}

      {booking && <BookModal doctors={doctors} presetPatient={params.get('patient')}
        onClose={() => { setBooking(false); if (params.get('patient')) setParams({}); }}
        onBooked={() => { setBooking(false); setParams({}); toast('Appointment booked.'); reload(true); }} />}
      {moving && <RescheduleModal appt={moving} doctors={doctors} onClose={() => setMoving(null)}
        onDone={() => { setMoving(null); toast('Appointment rescheduled. Confirm it again with the patient.'); reload(true); }} />}
    </>
  );
}
