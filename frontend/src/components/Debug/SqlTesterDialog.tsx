import { useState } from 'react';
import { apiGet } from '../../services/apiClient';
import './SqlTesterDialog.css';

interface SqlTesterDialogProps {
  isOpen: boolean;
  onClose: () => void;
}

interface TestResult {
  success: boolean;
  timestamp: string;
  report?: string;
  error?: string;
}

interface HealthCheckResult {
  status: string;
  timestamp: string;
  checks?: {
    api?: any;
    database?: any;
    openfga?: any;
    configuration?: any;
  };
}

export function SqlTesterDialog({ isOpen, onClose }: SqlTesterDialogProps) {
  const [activeTab, setActiveTab] = useState<'quick' | 'detailed' | 'health' | 'config'>('quick');
  const [testing, setTesting] = useState(false);
  const [quickResult, setQuickResult] = useState<any>(null);
  const [detailedResult, setDetailedResult] = useState<TestResult | null>(null);
  const [healthResult, setHealthResult] = useState<HealthCheckResult | null>(null);
  const [configResult, setConfigResult] = useState<TestResult | null>(null);

  if (!isOpen) return null;

  const handleQuickTest = async () => {
    setTesting(true);
    setQuickResult(null);

    try {
      const response = await apiGet('/test-sql');
      const data = await response.json();
      setQuickResult(data);
    } catch (error) {
      setQuickResult({ success: false, error: String(error) });
    } finally {
      setTesting(false);
    }
  };

  const handleDetailedTest = async () => {
    setTesting(true);
    setDetailedResult(null);

    try {
      const response = await apiGet('/debug/sql-test');
      const data = await response.json();
      setDetailedResult(data);
    } catch (error) {
      setDetailedResult({
        success: false,
        timestamp: new Date().toISOString(),
        error: String(error)
      });
    } finally {
      setTesting(false);
    }
  };

  const handleHealthCheck = async () => {
    setTesting(true);
    setHealthResult(null);

    try {
      const response = await apiGet('/health?verbose=true');
      const data = await response.json();
      setHealthResult(data);
    } catch (error) {
      setHealthResult({
        status: 'error',
        timestamp: new Date().toISOString()
      });
    } finally {
      setTesting(false);
    }
  };

  const handleConfigCheck = async () => {
    setTesting(true);
    setConfigResult(null);

    try {
      const response = await apiGet('/debug/config');
      const data = await response.json();
      setConfigResult(data);
    } catch (error) {
      setConfigResult({
        success: false,
        timestamp: new Date().toISOString(),
        error: String(error)
      });
    } finally {
      setTesting(false);
    }
  };

  const renderQuickTest = () => (
    <div className="test-panel">
      <div className="test-header">
        <h3>Quick SQL Test</h3>
        <p>Tests basic database connectivity and shows table counts</p>
      </div>

      <button
        className="test-button primary"
        onClick={handleQuickTest}
        disabled={testing}
      >
        {testing ? '⏳ Testing...' : '▶️ Run Quick Test'}
      </button>

      {quickResult && (
        <div className={`test-result ${quickResult.success ? 'success' : 'error'}`}>
          <div className="result-header">
            {quickResult.success ? '✅ Connection Successful' : '❌ Connection Failed'}
          </div>

          {quickResult.success ? (
            <div className="result-details">
              <div className="result-section">
                <strong>Database Information</strong>
                <ul>
                  <li><strong>Database:</strong> {quickResult.database}</li>
                  <li><strong>Server:</strong> {quickResult.connectionInfo?.server || 'unknown'}</li>
                  <li><strong>Auth Method:</strong> {quickResult.connectionInfo?.authMethod || 'unknown'}</li>
                </ul>
              </div>

              <div className="result-section">
                <strong>SQL User</strong>
                <ul>
                  <li><strong>Current User:</strong> {quickResult.user?.currentUser || 'unknown'}</li>
                  <li><strong>Login:</strong> {quickResult.user?.loginName || 'unknown'}</li>
                </ul>
              </div>

              <div className="result-section">
                <strong>Tables Found</strong>
                <ul>
                  <li><strong>MenuItems:</strong> {quickResult.tablesFound?.menuItems || 0}</li>
                  <li><strong>MenuGroups:</strong> {quickResult.tablesFound?.menuGroups || 0}</li>
                  <li><strong>PowerBI Configs:</strong> {quickResult.tablesFound?.powerBIConfigs || 0}</li>
                </ul>
              </div>

              <div className="result-section">
                <strong>Environment</strong>
                <ul>
                  <li><strong>Platform:</strong> {quickResult.environment?.isAzure ? 'Azure' : 'Local'}</li>
                  {quickResult.environment?.websiteSiteName && (
                    <li><strong>Site:</strong> {quickResult.environment.websiteSiteName}</li>
                  )}
                  {quickResult.environment?.identityEndpoint && (
                    <li><strong>IDENTITY_ENDPOINT:</strong> {quickResult.environment.identityEndpoint}</li>
                  )}
                  {quickResult.environment?.msiEndpoint && (
                    <li><strong>MSI_ENDPOINT:</strong> {quickResult.environment.msiEndpoint}</li>
                  )}
                </ul>
              </div>
            </div>
          ) : (
            <div className="result-details error-details">
              <p><strong>Error:</strong> {quickResult.error}</p>
              {quickResult.errorType && (
                <p><strong>Type:</strong> {quickResult.errorType}</p>
              )}
            </div>
          )}
        </div>
      )}
    </div>
  );

  const renderDetailedTest = () => (
    <div className="test-panel">
        <div className="test-header">
          <h3>Detailed SQL Connectivity Test</h3>
          <p>Runs managed identity diagnostics, EF Core probes, and direct SQL checks</p>
        </div>

      <div style={{ display: 'flex', gap: '12px', flexWrap: 'wrap' }}>
        <button
          className="test-button primary"
          onClick={handleDetailedTest}
          disabled={testing}
        >
          {testing ? '⏳ Testing...' : '▶️ Run Detailed Test'}
        </button>
      </div>

      {detailedResult && (
        <div className="test-result">
          <div className="result-header">
            {detailedResult.success ? '✅ Test Complete' : '❌ Test Failed'}
            <span className="timestamp">{new Date(detailedResult.timestamp).toLocaleString()}</span>
          </div>

          {detailedResult.report ? (
            <pre className="test-report">{detailedResult.report}</pre>
          ) : detailedResult.error ? (
            <div className="error-details">
              <p><strong>Error:</strong> {detailedResult.error}</p>
            </div>
          ) : null}
        </div>
      )}

      <div className="test-info">
        <h4>What This Tests:</h4>
        <ul>
          <li>✅ Managed Identity authentication (Azure production)</li>
          <li>✅ Connection string parsing and sanitization</li>
          <li>✅ Direct SQL query execution</li>
          <li>✅ EF Core DbContext operations</li>
          <li>✅ Managed identity access token acquisition</li>
        </ul>
      </div>
    </div>
  );

  const renderHealthCheck = () => (
    <div className="test-panel">
      <div className="test-header">
        <h3>Health Check (Verbose)</h3>
        <p>Checks all application components and their status</p>
      </div>

      <button
        className="test-button primary"
        onClick={handleHealthCheck}
        disabled={testing}
      >
        {testing ? '⏳ Checking...' : '▶️ Run Health Check'}
      </button>

      {healthResult && (
        <div className={`test-result ${healthResult.status === 'healthy' ? 'success' : 'warning'}`}>
          <div className="result-header">
            {healthResult.status === 'healthy' ? '✅ System Healthy' : '⚠️ System Degraded'}
            <span className="timestamp">{new Date(healthResult.timestamp).toLocaleString()}</span>
          </div>

          {healthResult.checks && (
            <div className="health-checks">
              {/* API Check */}
              {healthResult.checks.api && (
                <div className="health-check-item success">
                  <h4>✅ API</h4>
                  <ul>
                    <li><strong>Status:</strong> {healthResult.checks.api.status}</li>
                    <li><strong>Version:</strong> {healthResult.checks.api.version}</li>
                    <li><strong>Environment:</strong> {healthResult.checks.api.environment}</li>
                  </ul>
                </div>
              )}

              {/* Database Check */}
              {healthResult.checks.database && (
                <div className={`health-check-item ${healthResult.checks.database.status === 'healthy' ? 'success' : 'error'}`}>
                  <h4>{healthResult.checks.database.status === 'healthy' ? '✅' : '❌'} Database</h4>
                  {healthResult.checks.database.status === 'healthy' ? (
                    <ul>
                      <li><strong>Server:</strong> {healthResult.checks.database.server}</li>
                      <li><strong>Database:</strong> {healthResult.checks.database.database}</li>
                      <li><strong>Auth:</strong> {healthResult.checks.database.authenticationMethod}</li>
                      <li><strong>Menu Groups:</strong> {healthResult.checks.database.menuGroups}</li>
                      <li><strong>Response Time:</strong> {healthResult.checks.database.responseTime}</li>
                    </ul>
                  ) : (
                    <ul>
                      <li><strong>Error:</strong> {healthResult.checks.database.message}</li>
                      <li><strong>Type:</strong> {healthResult.checks.database.exceptionType}</li>
                    </ul>
                  )}
                </div>
              )}

              {/* OpenFGA Check */}
              {healthResult.checks.openfga && (
                <div className={`health-check-item ${healthResult.checks.openfga.status === 'healthy' ? 'success' : 'error'}`}>
                  <h4>{healthResult.checks.openfga.status === 'healthy' ? '✅' : '❌'} OpenFGA</h4>
                  {healthResult.checks.openfga.status === 'healthy' ? (
                    <ul>
                      <li><strong>URL:</strong> {healthResult.checks.openfga.url}</li>
                      <li><strong>Store ID:</strong> {healthResult.checks.openfga.storeId}</li>
                      <li><strong>Response Time:</strong> {healthResult.checks.openfga.responseTime}</li>
                    </ul>
                  ) : (
                    <ul>
                      <li><strong>Error:</strong> {healthResult.checks.openfga.message}</li>
                    </ul>
                  )}
                </div>
              )}

              {/* Configuration Check */}
              {healthResult.checks.configuration && (
                <div className={`health-check-item ${healthResult.checks.configuration.status === 'healthy' ? 'success' : 'warning'}`}>
                  <h4>{healthResult.checks.configuration.status === 'healthy' ? '✅' : '⚠️'} Configuration</h4>
                  <ul>
                    <li><strong>Environment:</strong> {healthResult.checks.configuration.environment}</li>
                    {healthResult.checks.configuration.issues && (
                      <li className="error-list">
                        <strong>Issues:</strong>
                        <ul>
                          {healthResult.checks.configuration.issues.map((issue: string, idx: number) => (
                            <li key={idx}>{issue}</li>
                          ))}
                        </ul>
                      </li>
                    )}
                  </ul>
                </div>
              )}
            </div>
          )}
        </div>
      )}
    </div>
  );

  const renderConfigCheck = () => (
    <div className="test-panel">
      <div className="test-header">
        <h3>Configuration Diagnostics</h3>
        <p>View sanitized configuration and environment details</p>
      </div>

      <button
        className="test-button primary"
        onClick={handleConfigCheck}
        disabled={testing}
      >
        {testing ? '⏳ Loading...' : '▶️ View Configuration'}
      </button>

      {configResult && (
        <div className="test-result">
          <div className="result-header">
            {configResult.success ? '✅ Configuration Loaded' : '❌ Failed to Load'}
            <span className="timestamp">{new Date(configResult.timestamp).toLocaleString()}</span>
          </div>

          {configResult.report ? (
            <pre className="test-report">{configResult.report}</pre>
          ) : configResult.error ? (
            <div className="error-details">
              <p><strong>Error:</strong> {configResult.error}</p>
            </div>
          ) : null}
        </div>
      )}

      <div className="test-info">
        <h4>What This Shows:</h4>
        <ul>
          <li>🔍 Environment detection (Azure vs Local)</li>
          <li>🔍 Connection string analysis (sanitized)</li>
          <li>🔍 OpenFGA configuration</li>
          <li>🔍 Azure Functions settings</li>
          <li>🔍 Process information</li>
          <li>🔍 Managed identity environment variables</li>
        </ul>
      </div>
    </div>
  );

  return (
    <div className="sql-tester-overlay">
      <div className="sql-tester-dialog">
        <div className="dialog-header">
          <h2>🔌 SQL Connectivity Tester</h2>
          <button className="close-button" onClick={onClose}>✕</button>
        </div>

        <div className="dialog-tabs">
          <button
            className={`tab ${activeTab === 'quick' ? 'active' : ''}`}
            onClick={() => setActiveTab('quick')}
          >
            Quick Test
          </button>
          <button
            className={`tab ${activeTab === 'detailed' ? 'active' : ''}`}
            onClick={() => setActiveTab('detailed')}
          >
            Detailed Test
          </button>
          <button
            className={`tab ${activeTab === 'health' ? 'active' : ''}`}
            onClick={() => setActiveTab('health')}
          >
            Health Check
          </button>
          <button
            className={`tab ${activeTab === 'config' ? 'active' : ''}`}
            onClick={() => setActiveTab('config')}
          >
            Configuration
          </button>
        </div>

        <div className="dialog-content">
          {activeTab === 'quick' && renderQuickTest()}
          {activeTab === 'detailed' && renderDetailedTest()}
          {activeTab === 'health' && renderHealthCheck()}
          {activeTab === 'config' && renderConfigCheck()}
        </div>

        <div className="dialog-footer">
          <p className="footer-note">
            💡 Tip: Use "Detailed Test" to diagnose connection issues. Results are logged to browser console.
          </p>
        </div>
      </div>
    </div>
  );
}
