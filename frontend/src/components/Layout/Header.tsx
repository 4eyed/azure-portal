import { useAuth } from '../../auth/useAuth';
import { useMenu } from '../../contexts/MenuContext';
import { useLocation } from 'react-router-dom';
import './Header.css';

export function Header() {
  const { user, logout } = useAuth();
  const { menuGroups } = useMenu();
  const location = useLocation();

  // Find the current menu item based on the URL
  const getCurrentMenuItem = () => {
    for (const group of menuGroups) {
      const item = group.items.find(i => i.url === location.pathname);
      if (item) {
        return { group: group.name, item: item.name };
      }
    }
    return null;
  };

  const currentPage = getCurrentMenuItem();

  return (
    <header className="header">
      <div className="breadcrumb">
        <button className="breadcrumb-back">‹</button>
        <button className="breadcrumb-forward">›</button>
        <span className="breadcrumb-text">
          {currentPage ? `${currentPage.group} / ${currentPage.item}` : 'Dashboard'}
        </span>
      </div>
      <div className="header-actions">
        <button className="fullscreen-btn" title="Toggle fullscreen">⛶</button>
        <button className="info-btn" title="Information">ⓘ</button>
      </div>
      {user && (
        <div className="user-info">
          <span>{user.name || user.username}</span>
          <button onClick={logout}>Logout</button>
        </div>
      )}
    </header>
  );
}
