import { useState } from 'react';
import { NavLink, Outlet } from 'react-router-dom';
import { useAuth } from '../auth.jsx';
import { api } from '../api.js';
import { Field, Modal, useUi } from './ui.jsx';

const ALL = ['Admin', 'Staff', 'Doctor'];
const NAV = [
  { to: '/', label: 'Dashboard', roles: ALL, end: true },
  { to: '/patients', label: 'Patients', roles: ALL },
  { to: '/appointments', label: 'Appointments', roles: ALL },
  { to: '/queue', label: 'Queue', roles: ALL },
  { to: '/billing', label: 'Billing', roles: ['Admin', 'Staff'] },
  { to: '/reports', label: 'Reports', roles: ['Admin'] },
  { to: '/users', label: 'Users', roles: ['Admin'] },
  { to: '/services', label: 'Services and fees', roles: ['Admin'] },
  { to: '/schedule', label: 'My schedule', roles: ['Doctor'] },
];

function ChangePassword({ onClose }) {
  const { toast } = useUi();
  const [form, setForm] = useState({ currentPassword: '', newPassword: '' });
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  const submit = async (e) => {
    e.preventDefault();
    setBusy(true); setError('');
    try {
      await api.post('/api/auth/change-password', form);
      toast('Password changed.');
      onClose();
    } catch (err) { setError(err.message); } finally { setBusy(false); }
  };

  return (
    <Modal title="Change password" onClose={onClose} small>
      <form onSubmit={submit}>
        {error && <div className="notice error" role="alert">{error}</div>}
        <Field label="Current password">
          <input type="password" required autoComplete="current-password" value={form.currentPassword}
            onChange={(e) => setForm({ ...form, currentPassword: e.target.value })} />
        </Field>
        <Field label="New password" hint="At least 8 characters, with a letter and a number.">
          <input type="password" required autoComplete="new-password" value={form.newPassword}
            onChange={(e) => setForm({ ...form, newPassword: e.target.value })} />
        </Field>
        <div className="actions">
          <button type="button" className="btn" onClick={onClose}>Cancel</button>
          <button className="btn primary" disabled={busy}>Save password</button>
        </div>
      </form>
    </Modal>
  );
}

export default function Layout() {
  const { user, logout } = useAuth();
  const [changing, setChanging] = useState(false);

  return (
    <div className="shell">
      <aside className="side">
        <div className="brand">Clinic Desk<small>Clinic Management System</small></div>
        <nav className="nav" aria-label="Main">
          {NAV.filter((n) => n.roles.includes(user.role)).map((n) => (
            <NavLink key={n.to} to={n.to} end={n.end}>{n.label}</NavLink>
          ))}
        </nav>
        <div className="side-foot">
          <strong>{user.fullName}</strong>
          <span>{user.role}</span>
          <div style={{ marginTop: 8 }}>
            <button onClick={() => setChanging(true)}>Password</button>
            <button onClick={() => logout()}>Sign out</button>
          </div>
        </div>
      </aside>
      <main className="main"><Outlet /></main>
      {changing && <ChangePassword onClose={() => setChanging(false)} />}
    </div>
  );
}
