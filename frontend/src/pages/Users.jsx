import { useState } from 'react';
import { api } from '../api.js';
import { useAuth } from '../auth.jsx';
import { Badge, Empty, ErrorNote, Field, Loading, Modal, PageHead, useAsync, useUi } from '../components/ui.jsx';
import { ScheduleForm } from '../components/shared.jsx';
import { fmtDateTime } from '../utils.js';

function UserForm({ onClose, onSaved }) {
  const [form, setForm] = useState({ username: '', password: '', fullName: '', email: '', role: 'Staff', specialty: '' });
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const set = (k) => (e) => setForm({ ...form, [k]: e.target.value });

  const submit = async (e) => {
    e.preventDefault();
    setBusy(true); setError('');
    try {
      await api.post('/api/users', { ...form, email: form.email || null, specialty: form.role === 'Doctor' ? form.specialty : null });
      onSaved();
    } catch (err) { setError(err.message); } finally { setBusy(false); }
  };

  return (
    <Modal title="Add user" onClose={onClose}>
      <form onSubmit={submit}>
        {error && <div className="notice error" role="alert">{error}</div>}
        <Field label="Full name"><input type="text" required value={form.fullName} onChange={set('fullName')} /></Field>
        <div className="grid2">
          <Field label="Username"><input type="text" required value={form.username} onChange={set('username')} /></Field>
          <Field label="Role">
            <select value={form.role} onChange={set('role')}><option>Staff</option><option>Doctor</option><option>Admin</option></select>
          </Field>
        </div>
        {form.role === 'Doctor' && <Field label="Specialty"><input type="text" required value={form.specialty} onChange={set('specialty')} /></Field>}
        <Field label="Email (optional)"><input type="email" value={form.email} onChange={set('email')} /></Field>
        <Field label="Temporary password" hint="At least 8 characters, with a letter and a number. Ask the user to change it after signing in."><input type="text" required value={form.password} onChange={set('password')} autoComplete="off" /></Field>
        <div className="actions">
          <button type="button" className="btn" onClick={onClose}>Cancel</button>
          <button className="btn primary" disabled={busy}>Create account</button>
        </div>
      </form>
    </Modal>
  );
}

function ResetPassword({ user, onClose }) {
  const { toast } = useUi();
  const [pw, setPw] = useState('');
  const [error, setError] = useState('');
  const submit = async (e) => {
    e.preventDefault();
    try { await api.post(`/api/users/${user.id}/reset-password`, { newPassword: pw }); toast(`Password reset for ${user.fullName}.`); onClose(); }
    catch (err) { setError(err.message); }
  };
  return (
    <Modal title={`Reset password for ${user.fullName}`} onClose={onClose} small>
      <form onSubmit={submit}>
        {error && <div className="notice error" role="alert">{error}</div>}
        <Field label="New temporary password"><input type="text" required value={pw} onChange={(e) => setPw(e.target.value)} autoComplete="off" /></Field>
        <div className="actions"><button type="button" className="btn" onClick={onClose}>Cancel</button><button className="btn primary">Reset password</button></div>
      </form>
    </Modal>
  );
}

export default function Users() {
  const { user: me } = useAuth();
  const { toast, confirm } = useUi();
  const [tab, setTab] = useState('all');
  const [adding, setAdding] = useState(false);
  const [resetting, setResetting] = useState(null);
  const [schedule, setSchedule] = useState(null);
  const { data, loading, error, reload } = useAsync(() => api.get('/api/users'), []);

  const setStatus = async (u, status, okMsg) => {
    try { await api.put(`/api/users/${u.id}/status`, { status }); toast(okMsg); reload(true); } catch (e) { toast(e.message, 'error'); }
  };
  const disable = async (u) => {
    const ok = await confirm({ title: `Disable ${u.fullName}?`, message: 'They are signed out right away and cannot sign in until you enable the account again.', confirmLabel: 'Disable account', cancelLabel: 'Keep active', danger: true });
    if (ok) setStatus(u, 'Disabled', 'Account disabled.');
  };

  const pending = (data ?? []).filter((u) => u.status === 'Pending');
  const rows = tab === 'pending' ? pending : data ?? [];

  return (
    <>
      <PageHead title="Users" sub="Approve sign-ups, add accounts and manage access."><button className="btn primary" onClick={() => setAdding(true)}>Add user</button></PageHead>
      <div className="tabs" role="tablist">
        <button role="tab" aria-selected={tab === 'all'} onClick={() => setTab('all')}>All users</button>
        <button role="tab" aria-selected={tab === 'pending'} onClick={() => setTab('pending')}>Waiting for approval{pending.length > 0 ? ` (${pending.length})` : ''}</button>
      </div>
      {error && <ErrorNote error={error} retry={reload} />}
      {loading && !data ? <Loading /> : rows.length === 0 ? <div className="panel"><Empty title="No accounts waiting">New sign-ups will appear here.</Empty></div> : (
        <div className="table-wrap">
          <table className="table">
            <thead><tr><th>Name</th><th>Role</th><th>Status</th><th>Last sign-in</th><th /></tr></thead>
            <tbody>
              {rows.map((u) => (
                <tr key={u.id}>
                  <td><strong>{u.fullName}</strong><span className="sub">{u.username}{u.specialty ? ` · ${u.specialty}` : ''}</span></td>
                  <td>{u.role}</td>
                  <td><Badge value={u.status} /></td>
                  <td>{u.lastLoginAt ? fmtDateTime(u.lastLoginAt) : <span className="muted">Never</span>}</td>
                  <td className="actions-cell">
                    {u.status === 'Pending' && <button className="btn small primary" onClick={() => setStatus(u, 'Active', 'Account approved.')}>Approve</button>}
                    {u.status === 'Pending' && <button className="btn small danger" onClick={() => setStatus(u, 'Disabled', 'Sign-up declined.')}>Decline</button>}
                    {u.doctorId && u.status !== 'Pending' && <button className="btn small" onClick={() => setSchedule(u)}>Schedule</button>}
                    {u.status !== 'Pending' && <button className="btn small" onClick={() => setResetting(u)}>Reset password</button>}
                    {u.status === 'Active' && u.id !== me.id && <button className="btn small danger" onClick={() => disable(u)}>Disable</button>}
                    {u.status === 'Disabled' && <button className="btn small" onClick={() => setStatus(u, 'Active', 'Account enabled.')}>Enable</button>}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {adding && <UserForm onClose={() => setAdding(false)} onSaved={() => { setAdding(false); toast('Account created.'); reload(true); }} />}
      {resetting && <ResetPassword user={resetting} onClose={() => setResetting(null)} />}
      {schedule && <Modal title={`Schedule: ${schedule.fullName}`} onClose={() => setSchedule(null)}>
        <ScheduleForm doctorId={schedule.doctorId} isAdmin onSaved={() => { setSchedule(null); reload(true); }} />
      </Modal>}
    </>
  );
}
