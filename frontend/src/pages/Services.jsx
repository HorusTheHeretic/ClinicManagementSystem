import { useState } from 'react';
import { api } from '../api.js';
import { Badge, Empty, ErrorNote, Field, Loading, Modal, PageHead, useAsync, useUi } from '../components/ui.jsx';
import { peso } from '../utils.js';

function ServiceForm({ service, onClose, onSaved }) {
  const [form, setForm] = useState({ name: service?.name ?? '', description: service?.description ?? '', fee: service?.fee ?? '', isActive: service?.isActive ?? true });
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  const submit = async (e) => {
    e.preventDefault();
    setBusy(true); setError('');
    const body = { name: form.name, description: form.description || null, fee: Number(form.fee), isActive: form.isActive };
    try { if (service) await api.put(`/api/services/${service.id}`, body); else await api.post('/api/services', body); onSaved(); }
    catch (err) { setError(err.message); } finally { setBusy(false); }
  };

  return (
    <Modal title={service ? 'Edit service' : 'Add service'} onClose={onClose} small>
      <form onSubmit={submit}>
        {error && <div className="notice error" role="alert">{error}</div>}
        <Field label="Name"><input type="text" required value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} /></Field>
        <Field label="Description (optional)"><input type="text" value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} /></Field>
        <Field label="Fee"><input type="number" required min="0" step="0.01" value={form.fee} onChange={(e) => setForm({ ...form, fee: e.target.value })} /></Field>
        <label className="check"><input type="checkbox" checked={form.isActive} onChange={(e) => setForm({ ...form, isActive: e.target.checked })} /> Doctors can add this service to a bill</label>
        <div className="actions"><button type="button" className="btn" onClick={onClose}>Cancel</button><button className="btn primary" disabled={busy}>Save service</button></div>
      </form>
    </Modal>
  );
}

export default function Services() {
  const { toast } = useUi();
  const [editing, setEditing] = useState(undefined);   // undefined = closed, null = new, object = edit
  const { data, loading, error, reload } = useAsync(() => api.get('/api/services?all=true'), []);

  return (
    <>
      <PageHead title="Services and fees" sub="Add-on services that doctors can put on a bill. The consultation fee itself is set per doctor under Users, then Schedule.">
        <button className="btn primary" onClick={() => setEditing(null)}>Add service</button>
      </PageHead>
      {error && <ErrorNote error={error} retry={reload} />}
      {loading && !data ? <Loading /> : data && (data.length === 0 ? <div className="panel"><Empty title="No services yet" /></div> : (
        <div className="table-wrap">
          <table className="table">
            <thead><tr><th>Service</th><th className="num">Fee</th><th>Status</th><th /></tr></thead>
            <tbody>
              {data.map((s) => (
                <tr key={s.id}>
                  <td><strong>{s.name}</strong>{s.description && <span className="sub">{s.description}</span>}</td>
                  <td className="num">{peso(s.fee)}</td>
                  <td>{s.isActive ? <Badge value="Active" /> : <Badge value="Disabled" />}</td>
                  <td className="actions-cell"><button className="btn small" onClick={() => setEditing(s)}>Edit</button></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ))}
      {editing !== undefined && <ServiceForm service={editing} onClose={() => setEditing(undefined)}
        onSaved={() => { setEditing(undefined); toast('Service saved.'); reload(true); }} />}
    </>
  );
}
