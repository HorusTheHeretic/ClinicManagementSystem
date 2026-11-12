import { useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { api } from '../api.js';
import { Badge, ErrorNote, Field, Loading, PageHead, useAsync, useUi } from '../components/ui.jsx';
import { fmtDate, peso, queueNo } from '../utils.js';

const EMPTY = { symptoms: '', bloodPressure: '', temperatureC: '', weightKg: '', diagnosis: '', treatment: '', prescription: '', notes: '', followUpDate: '' };

export default function Consultation() {
  const { appointmentId } = useParams();
  const nav = useNavigate();
  const { toast } = useUi();
  const ctx = useAsync(() => api.get(`/api/consultations/context/${appointmentId}`), [appointmentId]);
  const catalog = useAsync(() => api.get('/api/services'), []);
  const [form, setForm] = useState(EMPTY);
  const [picked, setPicked] = useState({});   // serviceId -> quantity
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  const saved = ctx.data?.consultation;
  const appt = ctx.data?.appointment;
  const readOnlyBilling = Boolean(saved);

  useEffect(() => {
    if (saved) {
      setForm({
        symptoms: saved.symptoms ?? '', bloodPressure: saved.bloodPressure ?? '', temperatureC: saved.temperatureC ?? '', weightKg: saved.weightKg ?? '',
        diagnosis: saved.diagnosis ?? '', treatment: saved.treatment ?? '', prescription: saved.prescription ?? '', notes: saved.notes ?? '',
        followUpDate: saved.followUpDate ? saved.followUpDate.slice(0, 10) : '',
      });
    }
  }, [saved]);

  const set = (k) => (e) => setForm({ ...form, [k]: e.target.value });
  const toggle = (id) => setPicked((p) => { const n = { ...p }; if (n[id]) delete n[id]; else n[id] = 1; return n; });
  const services = catalog.data ?? [];
  const servicesTotal = services.reduce((s, x) => s + (picked[x.id] ? x.fee * picked[x.id] : 0), 0);
  const fee = ctx.data?.consultationFee ?? 0;

  const submit = async (e) => {
    e.preventDefault();
    setBusy(true); setError('');
    const clinical = { symptoms: form.symptoms, diagnosis: form.diagnosis, treatment: form.treatment || null, prescription: form.prescription || null, notes: form.notes || null, followUpDate: form.followUpDate || null };
    try {
      if (saved) {
        await api.put(`/api/consultations/${saved.id}`, clinical);
        toast('Consultation updated.');
        nav('/queue');
      } else {
        const res = await api.post('/api/consultations', {
          appointmentId: Number(appointmentId), ...clinical,
          bloodPressure: form.bloodPressure || null,
          temperatureC: form.temperatureC === '' ? null : Number(form.temperatureC),
          weightKg: form.weightKg === '' ? null : Number(form.weightKg),
          services: Object.entries(picked).map(([serviceId, quantity]) => ({ serviceId: Number(serviceId), quantity: Number(quantity) })),
        });
        toast(`Consultation saved. Invoice ${res.invoiceNo} is ready for billing.`);
        nav('/queue');
      }
    } catch (err) { setError(err.message); } finally { setBusy(false); }
  };

  if (ctx.loading && !ctx.data) return <Loading />;
  if (ctx.error) return <><PageHead title="Consultation" /><ErrorNote error={ctx.error} retry={ctx.reload} /><Link to="/queue">Back to the queue</Link></>;

  const p = ctx.data.patient;
  const notInRoom = !saved && appt.status !== 'InConsultation';

  return (
    <>
      <PageHead title={`Consultation: ${p.firstName} ${p.lastName}`} sub={`Queue no. ${queueNo(appt.queueNumber)} · ${appt.reason || 'No reason recorded'}`}>
        <Badge value={appt.status} /><Link className="btn" to="/queue">Back to the queue</Link>
      </PageHead>

      <div className="consult">
        <aside className="panel patient-panel">
          {p.allergies && <div className="alert-allergy">Allergies: {p.allergies}</div>}
          <dl className="kv" style={{ gridTemplateColumns: '110px 1fr' }}>
            <dt>Patient no.</dt><dd>{p.patientNo}</dd>
            <dt>Age</dt><dd>{p.age} years, {p.gender}</dd>
            <dt>Contact</dt><dd>{p.contactNumber}</dd>
            <dt>History</dt><dd>{p.medicalHistory || 'None recorded'}</dd>
          </dl>
          <h3 style={{ margin: '20px 0 6px' }}>Previous visits</h3>
          {ctx.data.history.length === 0 ? <p className="muted small-text">First visit at this clinic.</p> : ctx.data.history.map((h) => (
            <div className="visit" key={h.consultationId}>
              <strong>{fmtDate(h.date)}</strong> <span className="muted">{h.doctorName}</span>
              <div>{h.diagnosis}</div>
              {h.prescription && <div className="muted">Rx: {h.prescription}</div>}
            </div>
          ))}
        </aside>

        <form className="panel" onSubmit={submit}>
          {notInRoom && <div className="notice warn">This patient is not in consultation. Call them from the queue first.</div>}
          {error && <div className="notice error" role="alert">{error}</div>}
          {saved && <div className="notice">This consultation is already saved and billed. You can correct the notes below; services and vitals stay as recorded.</div>}

          <Field label="Symptoms and findings"><textarea required value={form.symptoms} onChange={set('symptoms')} /></Field>
          <div className="grid3">
            <Field label="Blood pressure"><input type="text" placeholder="120/80" value={form.bloodPressure} onChange={set('bloodPressure')} disabled={readOnlyBilling} /></Field>
            <Field label="Temperature (°C)"><input type="number" step="0.1" min="30" max="45" value={form.temperatureC} onChange={set('temperatureC')} disabled={readOnlyBilling} /></Field>
            <Field label="Weight (kg)"><input type="number" step="0.1" min="0.5" max="500" value={form.weightKg} onChange={set('weightKg')} disabled={readOnlyBilling} /></Field>
          </div>
          <Field label="Diagnosis"><textarea required value={form.diagnosis} onChange={set('diagnosis')} style={{ minHeight: 64 }} /></Field>
          <Field label="Treatment and recommendations"><textarea value={form.treatment} onChange={set('treatment')} /></Field>
          <Field label="Prescription"><textarea value={form.prescription} onChange={set('prescription')} /></Field>
          <div className="grid2">
            <Field label="Notes (optional)"><input type="text" value={form.notes} onChange={set('notes')} /></Field>
            <Field label="Follow-up date (optional)"><input type="date" value={form.followUpDate} onChange={set('followUpDate')} /></Field>
          </div>

          <h3 style={{ margin: '10px 0 4px' }}>Services for the bill</h3>
          {readOnlyBilling ? (
            saved.services.length === 0 ? <p className="muted">No extra services were billed.</p> : saved.services.map((s) => (
              <div className="service-row" key={s.serviceId} style={{ gridTemplateColumns: '1fr 70px 110px' }}>
                <span>{s.name}</span><span>x{s.quantity}</span><span className="right">{peso(s.unitPrice * s.quantity)}</span>
              </div>
            ))
          ) : (
            <>
              <div className="service-row"><span /><span>Consultation fee</span><span /><span className="right">{peso(fee)}</span></div>
              {services.map((s) => (
                <div className="service-row" key={s.id}>
                  <input type="checkbox" checked={Boolean(picked[s.id])} onChange={() => toggle(s.id)} aria-label={`Add ${s.name}`} />
                  <span>{s.name}</span>
                  <input type="number" min="1" max="99" value={picked[s.id] ?? ''} disabled={!picked[s.id]} aria-label={`Quantity of ${s.name}`}
                    onChange={(e) => setPicked({ ...picked, [s.id]: Math.max(1, Number(e.target.value) || 1) })} />
                  <span className="right">{peso(s.fee)}</span>
                </div>
              ))}
              <div className="total-line"><span>Estimated total</span><span>{peso(fee + servicesTotal)}</span></div>
            </>
          )}

          <div className="actions" style={{ marginTop: 20 }}>
            <button className="btn primary big" disabled={busy || notInRoom}>{saved ? 'Save corrections' : 'Complete consultation'}</button>
          </div>
        </form>
      </div>
    </>
  );
}
