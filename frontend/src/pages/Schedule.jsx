import { useAuth } from '../auth.jsx';
import { PageHead } from '../components/ui.jsx';
import { ScheduleForm } from '../components/shared.jsx';

export default function Schedule() {
  const { user } = useAuth();
  return (
    <>
      <PageHead title="My schedule" sub="Patients can only be booked on your working days, inside these hours." />
      <div className="panel" style={{ maxWidth: 560 }}>
        <ScheduleForm doctorId={user.doctorId} isAdmin={false} />
      </div>
    </>
  );
}
