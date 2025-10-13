import { useState } from 'react';
import { useAuth } from '../../auth/useAuth';
import { useMenu } from '../../contexts/MenuContext';
import { useLocation } from 'react-router-dom';
import { SqlTesterDialog } from '../Debug/SqlTesterDialog';
import './Header.css';

export function Header() {
  const { user, logout } = useAuth();
  const { menuGroups } = useMenu();
  const location = useLocation();
  const [showSqlTester, setShowSqlTester] = useState(false);

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
    <>
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
          <button
            className="sql-test-btn"
            onClick={() => setShowSqlTester(true)}
            title="Test SQL Database Connection"
            style={{
              backgroundColor: '#667eea',
              color: 'white',
              border: 'none',
              padding: '8px 16px',
              borderRadius: '6px',
              cursor: 'pointer',
              fontSize: '14px',
              fontWeight: 600,
              display: 'flex',
              alignItems: 'center',
              gap: '6px',
              transition: 'all 0.2s'
            }}
            onMouseOver={(e) => {
              e.currentTarget.style.backgroundColor = '#5568d3';
              e.currentTarget.style.transform = 'translateY(-1px)';
            }}
            onMouseOut={(e) => {
              e.currentTarget.style.backgroundColor = '#667eea';
              e.currentTarget.style.transform = 'translateY(0)';
            }}
          >
            🔌 SQL Tester
          </button>
        </div>
        {user && (
          <div className="user-info">
            <span>{user.name || user.username}</span>
            <button onClick={logout}>Logout</button>
          </div>
        )}
      </header>

      <SqlTesterDialog
        isOpen={showSqlTester}
        onClose={() => setShowSqlTester(false)}
      />
    </>
  );
}
