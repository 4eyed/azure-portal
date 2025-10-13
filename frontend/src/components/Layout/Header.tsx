import { useState } from 'react';
import { useAuth } from '../../auth/useAuth';
import { useMenu } from '../../contexts/MenuContext';
import { useLocation } from 'react-router-dom';
import { useMsal } from '@azure/msal-react';
import { apiGet } from '../../services/apiClient';
import './Header.css';

export function Header() {
  const { user, logout } = useAuth();
  const { menuGroups } = useMenu();
  const location = useLocation();
  const { instance } = useMsal();
  const [testing, setTesting] = useState(false);
  const [testResult, setTestResult] = useState<any>(null);

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

  const handleTestSql = async () => {
    setTesting(true);
    setTestResult(null);

    try {
      console.group('🔍 SQL Connectivity Test');
      const response = await apiGet(instance, '/test-sql');
      const data = await response.json();

      setTestResult(data);

      if (data.success) {
        console.log('✅ SQL Connection Successful!');
        console.log('Database:', data.database);
        console.log('Tables Found:', data.tablesFound);
        console.log('Environment:', data.environment);
        console.log('Connection Info:', data.connectionInfo);
        console.log('SQL User:', data.user);

        alert(
          `✅ SQL Connected!\n\n` +
          `Database: ${data.database}\n` +
          `Server: ${data.connectionInfo?.server || 'unknown'}\n` +
          `Auth: ${data.connectionInfo?.authMethod || 'unknown'}\n` +
          `SQL User: ${data.user?.currentUser || 'unknown'}\n` +
          `Login: ${data.user?.loginName || 'unknown'}\n\n` +
          `Tables Found:\n` +
          `- MenuItems: ${data.tablesFound.menuItems}\n` +
          `- MenuGroups: ${data.tablesFound.menuGroups}\n` +
          `- PowerBI Configs: ${data.tablesFound.powerBIConfigs}\n\n` +
          `Environment: ${data.environment?.isAzure ? 'Azure (' + data.environment?.websiteSiteName + ')' : 'Local'}\n` +
          `Has SQL Token: ${data.environment?.hasSqlToken ? 'Yes (' + data.environment?.sqlTokenLength + ' chars)' : 'No (using Managed Identity)'}`
        );
      } else {
        console.error('❌ SQL Connection Failed');
        console.error('Error:', data.error);
        alert(`❌ SQL Connection Failed\n\nError: ${data.error}\nType: ${data.errorType}`);
      }
      console.groupEnd();
    } catch (error) {
      console.error('❌ Failed to test SQL connection:', error);
      alert(`❌ Test Failed\n\n${error}`);
      setTestResult({ success: false, error: String(error) });
    } finally {
      setTesting(false);
    }
  };

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
        <button
          className="sql-test-btn"
          onClick={handleTestSql}
          disabled={testing}
          title="Test SQL Database Connection"
          style={{
            backgroundColor: testResult?.success === true ? '#4caf50' : testResult?.success === false ? '#f44336' : '#2196f3',
            color: 'white',
            border: 'none',
            padding: '8px 12px',
            borderRadius: '4px',
            cursor: testing ? 'wait' : 'pointer',
            fontSize: '14px',
            fontWeight: 'bold'
          }}
        >
          {testing ? '⏳' : testResult?.success === true ? '✓ SQL' : testResult?.success === false ? '✗ SQL' : '🔌 SQL'}
        </button>
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
