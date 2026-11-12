import { Link, useNavigate } from 'react-router-dom';
import { api } from '../api.js';
import { useAuth } from '../auth.jsx';
import { Badge, Empty, ErrorNote, Loading, PageHead, Ticket, useAsync, usePolling, useUi } from '../components/ui.jsx';
import { fmtDate, fmtTime, fmtWeekday, peso, queueNo } from '../utils.js';

function Figures({ items }) {
  return (
    <dl className="figures">
      {items.map(([name, value]) => <div className="figure" key={name}><dt>{name}</dt><dd>{value}</dd></div>)}
    </dl>
  );
}

function AdminView({ d, reload }) {
  const { toast } = useUi();
  const max = Math.max(1, ...d.trend.map((t) => t.count));
  const approve = async (id) => {
    try { await api.put(`/api/users/${id}/status`, { status: 'Active' }); toast('Account approved.'); reload(true); }
    catch (e) { toast(e.message, 'error'); }
  };
  return (
    <>
      <Figures items={[
        ['Appointments today', d.appointmentsToday], ['Waiting now', d.waitingNow], ['Collected today', peso(d.collectedToday)],
        ['Unpaid balance', peso(d.outstanding)], ['Patients on file', d.totalPatients], ['Active users', d.activeUsers],
      ]} />
      <div className="two-col section">
        <div>
          <h2>Sign-ups waiting for approval <span className="muted">{d.pendingUsers} in total</span></h2>
          {d.pending.length === 0 ? <div className="panel"><Empty title="Nothing to approve" /></div> : (
            <div className="panel worklist">
              {d.pending.map((u) => (
                <div key={u.id}>
                  <span><strong>{u.fullName}</strong> <Badge value={u.role} tone="plain" /><span className="muted small-text"> signed up {fmtDate(u.createdAt)}</span></span>
                  <button className="btn small primary" onClick={() => approve(u.id)}>Approve</button>
                </div>
              ))}
              {d.pendingUsers > d.pending.length && <div><Link to="/users">See all pending accounts</Link></div>}
            </div>
          )}
        </div>
        <div>
          <h2>Appointments, last 7 days</h2>
          <div className="panel">
            <div className="bars" role="img" aria-label="Appointments per day for the last seven days">
              {d.trend.map((t) => (
                <div className="bar" key={t.date}>
                  <b>{t.count}</b><i style={{ height: `${(t.count / max) * 80}%` }} /><span>{fmtWeekday(`${t.date}T00:00:00`)}</span>
                </div>
              ))}
            </div>
          </div>
        </div>
      </div>
    </>
  );
}

function StaffView({ d, reload }) {
  const { toast } = useUi();
  const checkIn = async (id, priority) => {
    try { await api.post(`/api/queue/check-in/${id}`, { priority }); toast('Patient checked in.'); reload(true); }
    catch (e) { toast(e.message, 'error'); }
  };
  return (
    <>
      <Figures items={[
        ['Appointments today', d.appointmentsToday], ['To confirm', d.toConfirm], ['Waiting now', d.waitingNow],
        ['Unpaid invoices', d.unpaidInvoices], ['Collected today', peso(d.collectedToday)],
      ]} />
      <div className="section">
        <h2>Expected today <span className="muted">confirmed, not yet arrived</span></h2>
        {d.arrivals.length === 0 ? <div className="panel"><Empty title="No one is expected right now">Book an appointment or add a walk-in from the Queue page.</Empty></div> : (
          <div className="panel worklist">
            {d.arrivals.map((a) => (
              <div key={a.id}>
                <span><strong className="nowrap">{fmtTime(a.startsAt)}</strong> &nbsp;{a.patientName} <span className="muted small-text">with {a.doctorName}</span></span>
                <button className="btn small primary" onClick={() => checkIn(a.id, 'Normal')}>Check in</button>
              </div>
            ))}
          </div>
        )}
      </div>
    </>
  );
}

function DoctorView({ d, reload }) {
  const { toast } = useUi();
  const nav = useNavigate();
  const callNext = async () => {
    try { const a = await api.post('/api/queue/call-next'); nav(`/consultation/${a.id}`); }
    catch (e) { toast(e.message, 'error'); reload(true); }
  };
  return (
    <>
      <Figures items={[['Appointments today', d.appointmentsToday], ['Waiting for you', d.waiting], ['Seen today', d.completedToday]]} />
      <div className="two-col section">
        <div>
          <h2>Now</h2>
          <div className="panel row" style={{ gap: 24 }}>
            <Ticket number={queueNo(d.current?.queueNumber)} caption={d.current ? 'In consultation' : 'No one in the room'} />
            <div className="stack">
              {d.current ? (
                <>
                  <div><strong>{d.current.patientName}</strong><div className="muted">{d.current.reason || 'No reason recorded'}</div></div>
                  <Link className="btn primary" to={`/consultation/${d.current.id}`}>Continue consultation</Link>
                </>
              ) : d.next ? (
                <>
                  <div>Next in line: <strong>{queueNo(d.next.queueNumber)}</strong> {d.next.patientName}
                    {d.next.priority === 'Urgent' && <> <Badge value="Urgent" /></>}</div>
                  <button className="btn primary big" onClick={callNext}>Call next patient</button>
                </>
              ) : <p className="muted">Nobody is waiting. New arrivals appear here once they are checked in.</p>}
            </div>
          </div>
        </div>
        <div>
          <h2>Later today</h2>
          {d.upcoming.length === 0 ? <div className="panel"><Empty title="No more appointments today" /></div> : (
            <div className="panel worklist">
              {d.upcoming.map((a) => (
                <div key={a.id}><span><strong>{fmtTime(a.startsAt)}</strong> &nbsp;{a.patientName}</span><Badge value={a.status} /></div>
              ))}
            </div>
          )}
        </div>
      </div>
    </>
  );
}

export default function Dashboard() {
  const { user } = useAuth();
  const { data, loading, error, reload } = useAsync(() => api.get('/api/dashboard'), []);
  usePolling(() => reload(true), 15000);
  const hour = new Date().getHours();
  const greeting = hour < 12 ? 'Good morning' : hour < 18 ? 'Good afternoon' : 'Good evening';

  return (
    <>
      <PageHead title={`${greeting}, ${user.fullName.split(' ')[0] === 'Dr.' ? user.fullName.split(' ').slice(0, 2).join(' ') : user.fullName.split(' ')[0]}`}
        sub={new Date().toLocaleDateString('en-PH', { weekday: 'long', month: 'long', day: 'numeric' })} />
      {loading && !data && <Loading />}
      {error && <ErrorNote error={error} retry={reload} />}
      {data?.role === 'Admin' && <AdminView d={data} reload={reload} />}
      {data?.role === 'Staff' && <StaffView d={data} reload={reload} />}
      {data?.role === 'Doctor' && <DoctorView d={data} reload={reload} />}
    </>
  );
}
