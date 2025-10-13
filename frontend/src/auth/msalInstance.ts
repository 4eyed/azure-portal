import { PublicClientApplication } from '@azure/msal-browser';
import { msalConfig } from './config';

export const msalInstance = new PublicClientApplication(msalConfig);
