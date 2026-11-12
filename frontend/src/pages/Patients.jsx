import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api, qs } from '../api.js';
import { useAuth } from '../auth.jsx';
import { Empty, ErrorNote, Field, Loading, Modal, PageHead, useAsync, useUi } from '../components/ui.jsx';
import { fmtDate, toDateInput } from '../utils.js';

const BLANK = {
  firstName: '', lastName: '', dateOfBirth: '', gender: '', contactNumber: '', address: '',
  email: '', emergencyContact: '', allergies: '', medicalHistory: '',
};

function PatientForm({ patient, onClose, onSaved, onOpenExisting }) {
  const [form, setForm] = useState(patient ? { ...BLANK, ...Object.fromEntries(Object.entries(patient).map(([k, v]) => [k, v ?? ''])), dateOfBirth: toDateInput(patient.dateOfBirth) } : BLANK);
  const [error, setError] = useState('');
  const [existing, setExisting] = useState(null);
  const [busy, setBusy] = useState(false);
  const set = (k) => (e) => setForm({ ...form, [k]: e.target.value });

  const submit = async (e) => {
    e.preventDefault();
    setBusy(true); setError(''); setExisting(null);
    const body = { ...form, email: form.email || null, emergencyContact: form.emergencyContact || null, allergies: form.allergies || null, medicalHistory: form.medicalHistory || null };
    try {
      const saved = patient ? await api.put(`/api/patients/${patient.id}`, body) : await api.post('/api/patients', body);
      onSaved(saved, !patient);
    } catch (err) {
      if (err.status === 409 && err.data?.existing) setExisting(err.data.existing);
      setError(err.message);
    } finally { setBusy(false); }
  };

  return (
    <Modal title={patient ? 'Edit patient' : 'Register patient'} onClose={onClose} wide>
      <form onSubmit={submit}>
        {error && (
          <div className="notice error" role="alert">
            <span>{error}{existing && <> Existing record: <strong>{existing.patientNo}</strong>, {existing.firstName} {existing.lastName}.</>}</span>
            {existing && <button type="button" className="btn small" onClick={() => onOpenExisting(existing)}>Open that record</button>}
          </div>
        )}
        <div className="grid2">
          <Field label="First name"><input type="text" required value={form.firstName} onChange={set('firstName')} /></Field>
          <Field label="Last name"><input type="text" required value={form.lastName} onChange={set('lastName')} /></Field>
          <Field label="Date of birth"><input type="date" required max={toDateInput()} value={form.dateOfBirth} onChange={set('dateOfBirth')} /></Field>
          <Field label="Gender">
            <select required value={form.gender} onChange={set('gender')}>
              <option value="">Select</option><option>Female</option><option>Male</option><option>Other</option>
            </select>
          </Field>
          <Field label="Contact number"><input type="text" required value={form.contactNumber} onChange={set('contactNumber')} placeholder="0917 123 4567" /></Field>
          <Field label="Email (optional)"><input type="email" value={form.email} onChange={set('email')} /></Field>
        </div>
        <Field label="Address"><input type="text" required value={form.address} onChange={set('address')} /></Field>
        <Field label="Emergency contact (optional)"><input type="text" value={form.emergencyContact} onChange={set('emergencyContact')} placeholder="Name and phone number" /></Field>
        <Field label="Allergies (optional)" hint="Shown to the doctor during the consultation."><input type="text" value={form.allergies} onChange={set('allergies')} /></Field>
        <Field label="Medical history (optional)"><textarea value={form.medicalHistory} onChange={set('medicalHistory')} /></Field>
        <div className="actions">
          <button type="button" className="btn" onClick={onClose}>Cancel</button>
          <button className="btn primary" disabled={busy}>{patient ? 'Save changes' : 'Register patient'}</button>
        </div>
      </form>
    </Modal>
  );
}

function PatientDrawer({ id, onClose, onChanged }) {
  const { user } = useAuth();
  const { toast, confirm } = useUi();
  const { data: p, loading, error, reload } = useAsync(() => api.get(`/api/patients/${id}`), [id]);
  const canSeeHistory = user.role === 'Doctor' || user.role === 'Admin';
  const canEdit = user.role !== 'Doctor';
  const [history, setHistory] = useState(null);
  const [editing, setEditing] = useState(false);

  useEffect(() => {
    if (canSeeHistory) api.get(`/api/patients/${id}/history`).then(setHistory).catch(() => setHistory([]));
  }, [id, canSeeHistory]);

  const remove = async () => {
    const ok = await confirm({ title: 'Delete this patient record?', message: 'The record is hidden from searches. Billing and consultation history are kept. Patients with open appointments cannot be deleted.', confirmLabel: 'Delete record', cancelLabel: 'Keep record', danger: true });
    if (!ok) return;
    try { await api.del(`/api/patients/${id}`); toast('Patient record deleted.'); onChanged(); onClose(); }
    catch (e) { toast(e.message, 'error'); }
  };

  return (
    <Modal title={p ? `${p.firstName} ${p.lastName}` : 'Patient'} onClose={onClose} drawer>
      {loading && <Loading />}
      {error && <ErrorNote error={error} retry={reload} />}
      {p && (
        <>
          {p.allergies && <div className="alert-allergy">Allergies: {p.allergies}</div>}
          <dl className="kv">
            <dt>Patient number</dt><dd>{p.patientNo}</dd>
            <dt>Age and gender</dt><dd>{p.age} years, {p.gender}</dd>
            <dt>Date of birth</dt><dd>{fmtDate(p.dateOfBirth)}</dd>
            <dt>Contact</dt><dd>{p.contactNumber}{p.email ? `, ${p.email}` : ''}</dd>
            <dt>Address</dt><dd>{p.address}</dd>
            <dt>Emergency contact</dt><dd>{p.emergencyContact || '-'}</dd>
            <dt>Medical history</dt><dd>{p.medicalHistory || '-'}</dd>
            <dt>Registered</dt><dd>{fmtDate(p.createdAt)}</dd>
          </dl>
          <div className="row" style={{ margin: '18px 0' }}>
            {canEdit && <Link className="btn primary" to={`/appointments?patient=${p.id}`}>Book appointment</Link>}
            {canEdit && <button className="btn" onClick={() => setEditing(true)}>Edit</button>}
            {user.role === 'Admin' && <button className="btn danger" onClick={remove}>Delete</button>}
          </div>
          {canSeeHistory && (
            <div className="section">
              <h2>Consultation history</h2>
              {!history ? <Loading /> : history.length === 0 ? <p className="muted">No consultations recorded yet.</p> : history.map((h) => (
                <div className="visit" key={h.consultationId}>
                  <strong>{fmtDate(h.date)}</strong> <span className="muted">with {h.doctorName}</span>
                  <div><span className="muted">Symptoms:</span> {h.symptoms}</div>
                  <div><span className="muted">Diagnosis:</span> {h.diagnosis}</div>
                  {h.treatment && <div><span className="muted">Treatment:</span> {h.treatment}</div>}
                  {h.prescription && <div><span className="muted">Prescription:</span> {h.prescription}</div>}
                  {h.followUpDate && <div><span className="muted">Follow-up:</span> {fmtDate(h.followUpDate)}</div>}
                </div>
              ))}
            </div>
          )}
          {editing && <PatientForm patient={p} onClose={() => setEditing(false)}
            onSaved={() => { setEditing(false); toast('Patient updated.'); reload(true); onChanged(); }} onOpenExisting={() => {}} />}
        </>
      )}
    </Modal>
  );
}

export default function Patients() {
  const { user } = useAuth();
  const { toast } = useUi();
  const [search, setSearch] = useState('');
  const [debounced, setDebounced] = useState('');
  const [page, setPage] = useState(1);
  const [creating, setCreating] = useState(false);
  const [openId, setOpenId] = useState(null);
  const pageSize = 20;

  useEffect(() => { const t = setTimeout(() => { setDebounced(search); setPage(1); }, 300); return () => clearTimeout(t); }, [search]);
  const { data, loading, error, reload } = useAsync(() => api.get(`/api/patients${qs({ search: debounced, page, pageSize })}`), [debounced, page]);

  return (
    <>
      <PageHead title="Patients" sub="Search by name, patient number or phone number.">
        {user.role !== 'Doctor' && <button className="btn primary" onClick={() => setCreating(true)}>Register patient</button>}
      </PageHead>
      <div className="filters">
        <Field label="Search"><input type="search" value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Maria Santos, PT-2026-00001, 0917..." style={{ minWidth: 320 }} /></Field>
      </div>
      {error && <ErrorNote error={error} retry={reload} />}
      {loading && !data ? <Loading /> : data && (
        data.items.length === 0 ? <div className="panel"><Empty title="Patient not found">{debounced ? 'Check the spelling, or register them as a new patient.' : 'Register the first patient to get started.'}</Empty></div> : (
          <>
            <div className="table-wrap">
              <table className="table">
                <thead><tr><th>Patient no.</th><th>Name</th><th>Age</th><th>Gender</th><th>Contact</th><th>Registered</th></tr></thead>
                <tbody>
                  {data.items.map((p) => (
                    <tr key={p.id} className="click" onClick={() => setOpenId(p.id)}>
                      <td>{p.patientNo}</td>
                      <td><strong>{p.lastName}, {p.firstName}</strong>{p.allergies && <span className="sub">Allergies: {p.allergies}</span>}</td>
                      <td>{p.age}</td><td>{p.gender}</td><td>{p.contactNumber}</td><td>{fmtDate(p.createdAt)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div className="row" style={{ marginTop: 12 }}>
              <span className="muted small-text">{data.total} patient{data.total === 1 ? '' : 's'}</span><span className="spacer" />
              <button className="btn small" disabled={page <= 1} onClick={() => setPage(page - 1)}>Previous</button>
              <button className="btn small" disabled={page * pageSize >= data.total} onClick={() => setPage(page + 1)}>Next</button>
            </div>
          </>
        )
      )}
      {creating && <PatientForm onClose={() => setCreating(false)}
        onSaved={(p) => { setCreating(false); toast(`Registered ${p.firstName} ${p.lastName} as ${p.patientNo}.`); reload(true); setOpenId(p.id); }}
        onOpenExisting={(p) => { setCreating(false); setOpenId(p.id); }} />}
      {openId && <PatientDrawer id={openId} onClose={() => setOpenId(null)} onChanged={() => reload(true)} />}
    </>
  );
}
