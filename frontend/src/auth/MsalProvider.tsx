import { MsalProvider as BaseMsalProvider, MsalProviderProps } from '@azure/msal-react';
import { PublicClientApplication } from '@azure/msal-browser';
import { msalConfig } from './config';

const msalInstance = new PublicClientApplication(msalConfig);

export function MsalProvider({ children }: { children: React.ReactNode }) {
  return (
    <BaseMsalProvider instance={msalInstance}>
      {children}
    </BaseMsalProvider>
  );
}
