import { Navigate, Route, Routes } from 'react-router-dom';
import { useAuth } from './auth.jsx';
import Layout from './components/Layout.jsx';
import AuthPage from './pages/AuthPage.jsx';
import Dashboard from './pages/Dashboard.jsx';
import Patients from './pages/Patients.jsx';
import Appointments from './pages/Appointments.jsx';
import Queue from './pages/Queue.jsx';
import Board from './pages/Board.jsx';
import Consultation from './pages/Consultation.jsx';
import Billing from './pages/Billing.jsx';
import Reports from './pages/Reports.jsx';
import Users from './pages/Users.jsx';
import Services from './pages/Services.jsx';
import Schedule from './pages/Schedule.jsx';

function RequireAuth({ children }) {
  const { user } = useAuth();
  return user ? children : <Navigate to="/login" replace />;
}

/** Role-based routing: anyone else is sent back to the dashboard. */
function Only({ roles, children }) {
  const { user } = useAuth();
  return roles.includes(user.role) ? children : <Navigate to="/" replace />;
}

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<AuthPage />} />
      <Route path="/board" element={<Board />} />
      <Route element={<RequireAuth><Layout /></RequireAuth>}>
        <Route index element={<Dashboard />} />
        <Route path="patients" element={<Patients />} />
        <Route path="appointments" element={<Appointments />} />
        <Route path="queue" element={<Queue />} />
        <Route path="consultation/:appointmentId" element={<Only roles={['Doctor']}><Consultation /></Only>} />
        <Route path="billing" element={<Only roles={['Admin', 'Staff']}><Billing /></Only>} />
        <Route path="reports" element={<Only roles={['Admin']}><Reports /></Only>} />
        <Route path="users" element={<Only roles={['Admin']}><Users /></Only>} />
        <Route path="services" element={<Only roles={['Admin']}><Services /></Only>} />
        <Route path="schedule" element={<Only roles={['Doctor']}><Schedule /></Only>} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
