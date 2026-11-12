import { useState } from 'react';
import { Link, Navigate } from 'react-router-dom';
import { useAuth } from '../auth.jsx';
import { api } from '../api.js';
import { Field } from '../components/ui.jsx';

export default function AuthPage() {
  const { user, login, notice } = useAuth();
  const [mode, setMode] = useState('login');
  const [form, setForm] = useState({ username: '', password: '', confirm: '', fullName: '', email: '', role: 'Staff', specialty: '' });
  const [error, setError] = useState('');
  const [info, setInfo] = useState('');
  const [busy, setBusy] = useState(false);

  if (user) return <Navigate to="/" replace />;
  const set = (k) => (e) => setForm({ ...form, [k]: e.target.value });

  const submit = async (e) => {
    e.preventDefault();
    setError(''); setInfo('');
    if (mode === 'signup' && form.password !== form.confirm) { setError('The two passwords do not match.'); return; }
    setBusy(true);
    try {
      if (mode === 'login') {
        await login(form.username.trim(), form.password);
      } else {
        const res = await api.post('/api/auth/register', {
          username: form.username.trim(), password: form.password, fullName: form.fullName.trim(),
          email: form.email.trim() || null, role: form.role, specialty: form.role === 'Doctor' ? form.specialty.trim() : null,
        });
        setInfo(res.message);
        setMode('login');
        setForm({ ...form, password: '', confirm: '' });
      }
    } catch (err) { setError(err.message); } finally { setBusy(false); }
  };

  return (
    <div className="auth">
      <div className="auth-box">
        <h1>{mode === 'login' ? 'Sign in' : 'Create an account'}</h1>
        <p className="muted">{mode === 'login' ? 'Clinic Management System' : 'An administrator approves new accounts before they can sign in.'}</p>
        {notice && mode === 'login' && <div className="notice warn">{notice}</div>}
        {info && <div className="notice ok" role="status">{info}</div>}
        {error && <div className="notice error" role="alert">{error}</div>}

        <form onSubmit={submit}>
          {mode === 'signup' && (
            <>
              <Field label="Full name"><input type="text" required value={form.fullName} onChange={set('fullName')} autoComplete="name" /></Field>
              <Field label="Email (optional)"><input type="email" value={form.email} onChange={set('email')} autoComplete="email" /></Field>
              <div className="grid2">
                <Field label="I am a">
                  <select value={form.role} onChange={set('role')}>
                    <option value="Staff">Staff member</option>
                    <option value="Doctor">Doctor</option>
                  </select>
                </Field>
                {form.role === 'Doctor' && (
                  <Field label="Specialty"><input type="text" required value={form.specialty} onChange={set('specialty')} placeholder="General Medicine" /></Field>
                )}
              </div>
            </>
          )}
          <Field label="Username"><input type="text" required value={form.username} onChange={set('username')} autoComplete="username" /></Field>
          <Field label="Password" hint={mode === 'signup' ? 'At least 8 characters, with a letter and a number.' : undefined}>
            <input type="password" required value={form.password} onChange={set('password')} autoComplete={mode === 'login' ? 'current-password' : 'new-password'} />
          </Field>
          {mode === 'signup' && (
            <Field label="Confirm password"><input type="password" required value={form.confirm} onChange={set('confirm')} autoComplete="new-password" /></Field>
          )}
          <button className="btn primary big" style={{ width: '100%' }} disabled={busy}>
            {busy ? 'Please wait...' : mode === 'login' ? 'Sign in' : 'Create account'}
          </button>
        </form>

        <p className="auth-switch">
          {mode === 'login'
            ? <>New here? <button className="link-btn" onClick={() => { setMode('signup'); setError(''); setInfo(''); }}>Create an account</button></>
            : <>Already have an account? <button className="link-btn" onClick={() => { setMode('login'); setError(''); }}>Sign in</button></>}
        </p>
        <p className="auth-switch muted"><Link to="/board" target="_blank">Open the waiting-room display</Link></p>
      </div>
    </div>
  );
}
