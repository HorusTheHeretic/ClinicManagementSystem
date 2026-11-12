import { useEffect, useState } from 'react';
import { api } from '../api.js';
import { Ticket, useAsync, usePolling } from '../components/ui.jsx';
import { queueNo } from '../utils.js';

/** Public waiting-room display. Shows queue numbers only: no names, no sign-in. */
export default function Board() {
  const { data, error, reload } = useAsync(() => api.get('/api/queue/board'), []);
  usePolling(() => reload(true), 5000);
  const [now, setNow] = useState(new Date());
  useEffect(() => { const t = setInterval(() => setNow(new Date()), 1000); return () => clearInterval(t); }, []);

  return (
    <div className="board">
      <div className="board-head">
        <h1>{data?.clinicName ?? 'Clinic'}</h1>
        <span className="board-clock">{now.toLocaleTimeString('en-PH', { hour: 'numeric', minute: '2-digit' })}</span>
      </div>
      {error && <p>The display lost its connection. Retrying...</p>}
      <div className="board-grid">
        {data?.doctors.map((d) => (
          <section className="board-lane" key={d.doctorId}>
            <h2>{d.doctorName}</h2>
            <div className="muted">{d.specialty}</div>
            <Ticket size="xl" number={queueNo(d.nowServing)} caption={d.nowServing != null ? 'Now serving' : 'Please wait'} />
            <div className="board-next">
              {d.waiting.length > 0 ? (<><span>Next:</span>{d.waiting.map((n) => <span className="chip" key={n}>{queueNo(n)}</span>)}
                {d.waitingCount > d.waiting.length && <span>+{d.waitingCount - d.waiting.length} more</span>}</>) : <span>No one is waiting.</span>}
            </div>
          </section>
        ))}
      </div>
    </div>
  );
}
