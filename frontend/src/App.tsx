import { useEffect } from 'react';
import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { MsalProvider as BaseMsalProvider, useMsal } from '@azure/msal-react';
import { msalInstance } from './auth/msalInstance';
import { useSwaAuth } from './auth/useSwaAuth';
import { loginRequest } from './auth/config';
import { MenuProvider } from './contexts/MenuContext';
import { Sidebar } from './components/Layout/Sidebar';
import { Header } from './components/Layout/Header';
import { Dashboard } from './pages/Dashboard';
import { PowerBIReport } from './pages/PowerBIReport';
import { EnvDebugger } from './components/Debug/EnvDebugger';
import { DevAuthBanner } from './components/Debug/DevAuthBanner';
import { useDevAuthUpdater } from './auth/useDevAuthUpdater';
import './App.css';

// Handle MSAL redirect promise (still needed for token acquisition)
msalInstance.initialize().then(() => {
  msalInstance.handleRedirectPromise().catch((error) => {
    console.error('MSAL redirect error:', error);
  });
});

function AppContent() {
  const { isAuthenticated, userInfo, loading } = useSwaAuth();
  const { instance } = useMsal();
  useDevAuthUpdater();

  useEffect(() => {
    // In production, redirect to SWA login if not authenticated
    if (!loading && !isAuthenticated && !import.meta.env.DEV) {
      console.log('Not authenticated, redirecting to SWA login...');
      const returnUrl = encodeURIComponent(window.location.pathname + window.location.search);
      window.location.href = `/.auth/login/aad?post_login_redirect_uri=${returnUrl}`;
    }
  }, [isAuthenticated, loading]);

  // Show loading state while checking authentication
  if (loading) {
    return (
      <div className="login-page">
        <div className="login-box">
          <h1>JA Portal</h1>
          <p>Loading...</p>
        </div>
      </div>
    );
  }

  // In local dev, show login prompt if not authenticated
  if (!isAuthenticated && import.meta.env.DEV) {
    const handleLogin = () => {
      console.log('🔐 Starting MSAL login redirect...');
      instance.loginRedirect(loginRequest);
    };

    return (
      <div className="login-page">
        <div className="login-box">
          <h1>JA Portal - Local Dev</h1>
          <p>Please sign in with your Azure AD account</p>
          <button
            onClick={handleLogin}
            style={{
              marginTop: '20px',
              padding: '10px 20px',
              fontSize: '16px',
              backgroundColor: '#0078d4',
              color: 'white',
              border: 'none',
              borderRadius: '4px',
              cursor: 'pointer'
            }}
          >
            Sign in with Microsoft
          </button>
          <p style={{ fontSize: '0.9em', color: '#666', marginTop: '20px' }}>
            You will be redirected to Microsoft login
          </p>
        </div>
      </div>
    );
  }

  return (
    <MenuProvider>
      <DevAuthBanner />
      <div className="app">
        <Sidebar />
        <div className="main-content">
          <Header />
          <main className="content">
            <Routes>
              <Route path="/" element={<Dashboard />} />
              <Route path="/dashboard" element={<Dashboard />} />
              <Route path="/powerbi/:reportId" element={<PowerBIReport />} />
              <Route path="*" element={<Dashboard />} />
            </Routes>
          </main>
        </div>
      </div>
    </MenuProvider>
  );
}

function App() {
  return (
    <BaseMsalProvider instance={msalInstance}>
      <BrowserRouter>
        <AppContent />
        <EnvDebugger />
      </BrowserRouter>
    </BaseMsalProvider>
  );
}

export default App;
