import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { api, setToken, setUnauthorizedHandler } from './api.js';

const AuthContext = createContext(null);
const KEY = 'clinic.session';

// sessionStorage: the session ends when the tab closes, which suits shared front-desk computers.
function loadSession() {
  try {
    const s = JSON.parse(sessionStorage.getItem(KEY));
    if (s && new Date(s.expiresAt) > new Date()) { setToken(s.token); return s; }
  } catch { /* ignore */ }
  sessionStorage.removeItem(KEY);
  return null;
}

export function AuthProvider({ children }) {
  const [session, setSession] = useState(loadSession);
  const [notice, setNotice] = useState('');

  const logout = useCallback((message = '') => {
    sessionStorage.removeItem(KEY);
    setToken(null);
    setSession(null);
    setNotice(message);
  }, []);

  useEffect(() => {
    setUnauthorizedHandler(() => logout('Your session ended. Sign in again to continue.'));
  }, [logout]);

  // Sign out automatically when the token expires.
  useEffect(() => {
    if (!session) return undefined;
    const ms = new Date(session.expiresAt).getTime() - Date.now();
    const t = setTimeout(() => logout('Your session expired. Sign in again to continue.'), Math.min(Math.max(ms, 0), 2 ** 31 - 1));
    return () => clearTimeout(t);
  }, [session, logout]);

  const login = useCallback(async (username, password) => {
    const res = await api.post('/api/auth/login', { username, password });
    sessionStorage.setItem(KEY, JSON.stringify(res));
    setToken(res.token);
    setNotice('');
    setSession(res);
  }, []);

  const value = useMemo(() => ({ user: session?.user ?? null, login, logout, notice }), [session, login, logout, notice]);
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export const useAuth = () => useContext(AuthContext);
