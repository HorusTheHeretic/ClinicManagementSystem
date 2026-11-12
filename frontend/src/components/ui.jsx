import { createContext, useCallback, useContext, useEffect, useRef, useState } from 'react';
import { label } from '../utils.js';

// ---------- data loading ----------
export function useAsync(fn, deps) {
  const [state, setState] = useState({ loading: true, data: null, error: null });
  const latest = useRef(0);
  // eslint-disable-next-line react-hooks/exhaustive-deps
  const reload = useCallback(async (silent = false) => {
    const id = ++latest.current;
    if (!silent) setState((s) => ({ ...s, loading: true, error: null }));
    try {
      const data = await fn();
      if (id === latest.current) setState({ loading: false, data, error: null });
    } catch (error) {
      if (id === latest.current) setState((s) => ({ loading: false, data: silent ? s.data : null, error }));
    }
  }, deps);
  useEffect(() => { reload(); }, [reload]);
  return { ...state, reload };
}

/** Calls fn every `ms` while the tab is visible. */
export function usePolling(fn, ms) {
  const ref = useRef(fn);
  ref.current = fn;
  useEffect(() => {
    const t = setInterval(() => { if (!document.hidden) ref.current(); }, ms);
    return () => clearInterval(t);
  }, [ms]);
}

// ---------- toasts and confirm dialogs ----------
const UiContext = createContext(null);
export const useUi = () => useContext(UiContext);

export function UiProvider({ children }) {
  const [toasts, setToasts] = useState([]);
  const [dialog, setDialog] = useState(null);

  const toast = useCallback((message, kind = 'ok') => {
    const id = Math.random().toString(36).slice(2);
    setToasts((t) => [...t, { id, message, kind }]);
    setTimeout(() => setToasts((t) => t.filter((x) => x.id !== id)), 4500);
  }, []);

  const confirm = useCallback((opts) => new Promise((resolve) => setDialog({ ...opts, resolve })), []);
  const close = (result) => { dialog.resolve(result); setDialog(null); };

  return (
    <UiContext.Provider value={{ toast, confirm }}>
      {children}
      <div className="toasts" role="status" aria-live="polite">
        {toasts.map((t) => <div key={t.id} className={`toast ${t.kind}`}>{t.message}</div>)}
      </div>
      {dialog && (
        <Modal title={dialog.title} onClose={() => close(false)} small>
          <p className="muted">{dialog.message}</p>
          <div className="actions">
            <button className="btn" onClick={() => close(false)}>{dialog.cancelLabel ?? 'Keep it'}</button>
            <button className={`btn ${dialog.danger ? 'danger' : 'primary'}`} onClick={() => close(true)}>{dialog.confirmLabel ?? 'Confirm'}</button>
          </div>
        </Modal>
      )}
    </UiContext.Provider>
  );
}

// ---------- building blocks ----------
export function Modal({ title, onClose, children, small = false, drawer = false, wide = false }) {
  const ref = useRef(null);
  useEffect(() => {
    const onKey = (e) => { if (e.key === 'Escape') onClose(); };
    document.addEventListener('keydown', onKey);
    const prev = document.activeElement;
    ref.current?.focus();
    return () => { document.removeEventListener('keydown', onKey); prev?.focus?.(); };
  }, [onClose]);

  const cls = ['modal', small && 'small', drawer && 'drawer', wide && 'wide'].filter(Boolean).join(' ');
  return (
    <div className="backdrop" onMouseDown={(e) => { if (e.target === e.currentTarget) onClose(); }}>
      <div className={cls} role="dialog" aria-modal="true" aria-label={title} tabIndex={-1} ref={ref}>
        <header className="modal-head">
          <h2>{title}</h2>
          <button className="icon-btn" onClick={onClose} aria-label="Close">&times;</button>
        </header>
        <div className="modal-body">{children}</div>
      </div>
    </div>
  );
}

export function Field({ label: text, hint, children, className = '' }) {
  return (
    <label className={`field ${className}`}>
      <span className="field-label">{text}</span>
      {children}
      {hint && <span className="field-hint">{hint}</span>}
    </label>
  );
}

export function Badge({ value, tone }) {
  const t = tone ?? ({
    Waiting: 'amber', InConsultation: 'teal', Completed: 'green', Cancelled: 'grey', NoShow: 'grey',
    Confirmed: 'blue', Scheduled: 'plain', Unpaid: 'red', PartiallyPaid: 'amber', Paid: 'green',
    Pending: 'amber', Active: 'green', Disabled: 'grey', Urgent: 'red',
  }[value] ?? 'plain');
  return <span className={`badge ${t}`}>{label(value)}</span>;
}

export function Loading({ text = 'Loading...' }) { return <p className="muted pad">{text}</p>; }
export function ErrorNote({ error, retry }) {
  return (
    <div className="notice error" role="alert">
      <span>{error?.message ?? 'Something went wrong.'}</span>
      {retry && <button className="btn small" onClick={() => retry()}>Try again</button>}
    </div>
  );
}
export function Empty({ title, children }) {
  return <div className="empty"><strong>{title}</strong>{children && <p>{children}</p>}</div>;
}

export function PageHead({ title, children, sub }) {
  return (
    <div className="page-head">
      <div><h1>{title}</h1>{sub && <p className="muted">{sub}</p>}</div>
      <div className="page-actions">{children}</div>
    </div>
  );
}

/** Amber queue ticket. The one loud element of the interface. */
export function Ticket({ number, caption, size = 'md' }) {
  return (
    <div className={`ticket ${size}`}>
      <span className="ticket-caption">{caption}</span>
      <span className="ticket-number" key={number}>{number}</span>
    </div>
  );
}
