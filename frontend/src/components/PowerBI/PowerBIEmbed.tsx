import { useState, useEffect } from 'react';
import { PowerBIEmbed as PowerBIEmbedComponent } from 'powerbi-client-react';
import { models } from 'powerbi-client';
import { useAuth } from '../../auth/useAuth';
import { useMenu } from '../../contexts/MenuContext';
import './PowerBIEmbed.css';

interface PowerBIEmbedProps {
  reportId: string;
}

export function PowerBIEmbed({ reportId }: PowerBIEmbedProps) {
  const { isAuthenticated, getPowerBIToken } = useAuth();
  const { menuGroups } = useMenu();
  const [embedConfig, setEmbedConfig] = useState<models.IReportEmbedConfiguration | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    loadReport();
  }, [reportId, isAuthenticated, menuGroups]);

  const loadReport = async () => {
    if (!isAuthenticated) {
      setError('User not authenticated');
      return;
    }

    // Wait for menu groups to load before attempting to find config
    if (menuGroups.length === 0) {
      console.log('Menu groups not yet loaded, waiting...');
      setError(null); // Clear any previous errors
      setEmbedConfig(null); // Clear any previous config
      return;
    }

    try {
      console.log('Looking for reportId:', reportId, 'in menu groups:', menuGroups);

      // Find the menu item with this reportId to get the Power BI config
      let powerBIConfig = null;
      for (const group of menuGroups) {
        const item = group.items.find((i: any) => i.powerBIConfig?.reportId === reportId);
        if (item?.powerBIConfig) {
          powerBIConfig = item.powerBIConfig;
          console.log('Found Power BI config:', powerBIConfig);
          break;
        }
      }

      if (!powerBIConfig) {
        throw new Error(`No Power BI configuration found for report ${reportId}`);
      }

      const { workspaceId, reportId: actualReportId, embedUrl } = powerBIConfig;

      if (!workspaceId || !actualReportId) {
        throw new Error('Power BI workspace and report IDs must be configured');
      }

      // Acquire Azure AD token with Power BI scope (User Owns Data pattern)
      console.log('Acquiring Power BI token for embed...');
      const accessToken = await getPowerBIToken();
      console.log('Token acquired, configuring embed with:', {
        reportId: actualReportId,
        workspaceId,
        embedUrl
      });

      setEmbedConfig({
        type: 'report',
        id: actualReportId,
        embedUrl: embedUrl,
        accessToken: accessToken,
        tokenType: models.TokenType.Aad, // Use Azure AD token (User Owns Data)
        settings: {
          panes: {
            filters: {
              expanded: powerBIConfig.showFilterPanelExpanded,
              visible: powerBIConfig.showFilterPanel,
            },
          },
          background: models.BackgroundType.Transparent,
        },
      });

      console.log('Power BI embed configured successfully');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load report');
    }
  };

  if (error) {
    return <div className="powerbi-error">{error}</div>;
  }

  if (!embedConfig) {
    return <div className="powerbi-loading">Loading report...</div>;
  }

  return (
    <div className="powerbi-container">
      <PowerBIEmbedComponent
        embedConfig={embedConfig}
        cssClassName="powerbi-frame"
      />
    </div>
  );
}
