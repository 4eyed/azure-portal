import { useEffect } from 'react';
import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { MsalProvider as BaseMsalProvider } from '@azure/msal-react';
import { PublicClientApplication, EventType } from '@azure/msal-browser';
import { msalConfig } from './auth/config';
import { useAuth } from './auth/useAuth';
import { MenuProvider } from './contexts/MenuContext';
import { Sidebar } from './components/Layout/Sidebar';
import { Header } from './components/Layout/Header';
import { Dashboard } from './pages/Dashboard';
import { PowerBIReport } from './pages/PowerBIReport';
import './App.css';

const msalInstance = new PublicClientApplication(msalConfig);

// Handle redirect promise
msalInstance.initialize().then(() => {
  msalInstance.handleRedirectPromise().catch((error) => {
    console.error('Redirect error:', error);
  });
});

function AppContent() {
  const { isAuthenticated, login } = useAuth();

  if (!isAuthenticated) {
    return (
      <div className="login-page">
        <div className="login-box">
          <h1>JA Portal</h1>
          <p>Please sign in to continue</p>
          <button onClick={login} className="login-btn">
            Sign in with Microsoft
          </button>
        </div>
      </div>
    );
  }

  return (
    <MenuProvider>
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
      </BrowserRouter>
    </BaseMsalProvider>
  );
}

export default App;
