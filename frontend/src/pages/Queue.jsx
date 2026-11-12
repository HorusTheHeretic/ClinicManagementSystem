import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { api, qs } from '../api.js';
import { useAuth } from '../auth.jsx';
import { Badge, Empty, ErrorNote, Field, Loading, Modal, PageHead, Ticket, useAsync, usePolling, useUi } from '../components/ui.jsx';
import { PatientPicker } from '../components/shared.jsx';
import { fmtTime, minutesSince, queueNo, toDateInput } from '../utils.js';

function WalkInModal({ doctors, onClose, onDone }) {
  const [patient, setPatient] = useState(null);
  const [doctorId, setDoctorId] = useState(doctors.length === 1 ? String(doctors[0].id) : '');
  const [reason, setReason] = useState('');
  const [urgent, setUrgent] = useState(false);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  const submit = async (e) => {
    e.preventDefault();
    if (!patient) { setError('Choose a patient.'); return; }
    setBusy(true); setError('');
    try {
      const a = await api.post('/api/queue/walk-in', { patientId: patient.id, doctorId: Number(doctorId), reason: reason || null, priority: urgent ? 'Urgent' : 'Normal' });
      onDone(a);
    } catch (err) { setError(err.message); } finally { setBusy(false); }
  };

  return (
    <Modal title="Add walk-in patient" onClose={onClose}>
      <form onSubmit={submit}>
        {error && <div className="notice error" role="alert">{error}</div>}
        <PatientPicker value={patient} onChange={setPatient} />
        <Field label="Doctor">
          <select required value={doctorId} onChange={(e) => setDoctorId(e.target.value)}>
            <option value="">Select a doctor</option>
            {doctors.map((d) => <option key={d.id} value={d.id}>{d.fullName} ({d.specialty})</option>)}
          </select>
        </Field>
        <Field label="Reason for visit (optional)"><input type="text" value={reason} onChange={(e) => setReason(e.target.value)} /></Field>
        <label className="check"><input type="checkbox" checked={urgent} onChange={(e) => setUrgent(e.target.checked)} /> Urgent: call before other waiting patients</label>
        <div className="actions">
          <button type="button" className="btn" onClick={onClose}>Cancel</button>
          <button className="btn primary" disabled={busy}>Add to queue</button>
        </div>
      </form>
    </Modal>
  );
}

export default function Queue() {
  const { user } = useAuth();
  const { toast, confirm } = useUi();
  const nav = useNavigate();
  const isDoctor = user.role === 'Doctor';
  const [walkIn, setWalkIn] = useState(false);

  const docs = useAsync(() => api.get('/api/doctors'), []);
  const queue = useAsync(() => api.get('/api/queue'), []);
  const arrivals = useAsync(() => (isDoctor ? Promise.resolve([]) : api.get(`/api/appointments${qs({ date: toDateInput(), status: 'Confirmed' })}`)), [isDoctor]);
  const refresh = () => { queue.reload(true); arrivals.reload(true); };
  usePolling(refresh, 8000);

  const act = async (fn, okMsg) => {
    try { const r = await fn(); if (okMsg) toast(okMsg); refresh(); return r; } catch (e) { toast(e.message, 'error'); refresh(); return null; }
  };
  const callNext = async () => {
    const a = await act(() => api.post('/api/queue/call-next'));
    if (a) nav(`/consultation/${a.id}`);
  };
  const noShow = async (a) => {
    const ok = await confirm({ title: `Remove ${a.patientName} from the queue?`, message: 'They are marked as a no-show and their time becomes available again.', confirmLabel: 'Mark no-show', cancelLabel: 'Keep in queue', danger: true });
    if (ok) act(() => api.post(`/api/appointments/${a.id}/no-show`), 'Marked as no-show.');
  };

  const doctors = isDoctor ? (docs.data ?? []).filter((d) => d.id === user.doctorId) : (docs.data ?? []);
  const entries = queue.data ?? [];
  const lanes = isDoctor
    ? [{ id: user.doctorId, name: user.fullName, specialty: doctors[0]?.specialty }]
    : doctors.map((d) => ({ id: d.id, name: d.fullName, specialty: d.specialty }));
  const expected = (arrivals.data ?? []);

  return (
    <>
      <PageHead title="Queue" sub="Today's waiting line. Urgent patients go first, then by queue number. Updates every few seconds.">
        <a className="btn" href="/board" target="_blank" rel="noreferrer">Open waiting-room display</a>
        {!isDoctor && <button className="btn primary" onClick={() => setWalkIn(true)}>Add walk-in</button>}
      </PageHead>

      {queue.error && <ErrorNote error={queue.error} retry={queue.reload} />}

      {!isDoctor && (
        <div className="section" style={{ marginTop: 0 }}>
          <h2>Expected arrivals <span className="muted">confirmed for today, not yet checked in</span></h2>
          {expected.length === 0 ? <div className="panel"><Empty title="No one else is expected today" /></div> : (
            <div className="table-wrap"><table className="table"><tbody>
              {expected.map((a) => (
                <tr key={a.id}>
                  <td className="nowrap"><strong>{fmtTime(a.startsAt)}</strong></td>
                  <td>{a.patientName}<span className="sub">{a.patientNo}</span></td>
                  <td>{a.doctorName}</td>
                  <td>{a.reason || <span className="muted">-</span>}</td>
                  <td className="actions-cell">
                    <button className="btn small primary" onClick={() => act(() => api.post(`/api/queue/check-in/${a.id}`, { priority: 'Normal' }), 'Patient checked in.')}>Check in</button>
                    <button className="btn small" onClick={() => act(() => api.post(`/api/queue/check-in/${a.id}`, { priority: 'Urgent' }), 'Checked in as urgent.')}>Urgent</button>
                  </td>
                </tr>
              ))}
            </tbody></table></div>
          )}
        </div>
      )}

      <div className="section">
        <h2>{isDoctor ? 'Your queue' : 'Doctors'}</h2>
        {queue.loading && !queue.data ? <Loading /> : lanes.length === 0 ? <div className="panel"><Empty title="No active doctors">Approve a doctor account under Users.</Empty></div> : (
          <div className="panel">
            {lanes.map((lane) => {
              const mine = entries.filter((e) => e.doctorId === lane.id);
              const current = mine.find((e) => e.status === 'InConsultation');
              const waiting = mine.filter((e) => e.status === 'Waiting');
              return (
                <div className="lane" key={lane.id}>
                  <div className="lane-side">
                    <div><h3>{lane.name}</h3><span className="muted">{lane.specialty}</span></div>
                    <Ticket number={queueNo(current?.queueNumber)} caption={current ? 'Now serving' : 'Room is free'} size="lg" />
                    {isDoctor && (current
                      ? <Link className="btn primary" to={`/consultation/${current.id}`}>Continue consultation</Link>
                      : <button className="btn amber big" onClick={callNext} disabled={waiting.length === 0}>Call next patient</button>)}
                  </div>
                  <div>
                    {current && !isDoctor && <p><strong>{current.patientName}</strong> is in consultation.</p>}
                    {waiting.length === 0 ? <p className="muted">Nobody is waiting.</p> : (
                      <div className="table-wrap"><table className="table">
                        <thead><tr><th>No.</th><th>Patient</th><th>Waiting</th><th>Reason</th><th /></tr></thead>
                        <tbody>
                          {waiting.map((w) => (
                            <tr key={w.id}>
                              <td><span className="line-no">{queueNo(w.queueNumber)}</span></td>
                              <td>{w.patientName} {w.priority === 'Urgent' && <Badge value="Urgent" />}<span className="sub">{w.isWalkIn ? 'Walk-in' : `Booked for ${fmtTime(w.startsAt)}`}</span></td>
                              <td>{minutesSince(w.checkedInAt)} min</td>
                              <td>{w.reason || <span className="muted">-</span>}</td>
                              <td className="actions-cell"><button className="btn small danger" onClick={() => noShow(w)}>Remove</button></td>
                            </tr>
                          ))}
                        </tbody>
                      </table></div>
                    )}
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>

      {walkIn && <WalkInModal doctors={docs.data ?? []} onClose={() => setWalkIn(false)}
        onDone={(a) => { setWalkIn(false); toast(`${a.patientName} added as queue no. ${queueNo(a.queueNumber)}.`); refresh(); }} />}
    </>
  );
}
