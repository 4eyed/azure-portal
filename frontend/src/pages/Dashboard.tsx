import { useParams } from 'react-router-dom';
import './Dashboard.css';

export function Dashboard() {
  const { id } = useParams();

  return (
    <div className="dashboard">
      <h2>Dashboard {id || 'Home'}</h2>
      <p>Welcome to the portal dashboard. Select a menu item to view content.</p>
    </div>
  );
}
