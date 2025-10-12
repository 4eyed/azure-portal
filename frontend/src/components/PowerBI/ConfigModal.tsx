import { useState, useEffect } from 'react';
import { Dialog, DialogTitle, DialogContent, DialogActions, Button, TextField, Select, MenuItem, FormControl, InputLabel, Checkbox, FormControlLabel } from '@mui/material';
import { powerBIClient } from '../../services/powerbi/client';
import { useAuth } from '../../auth/useAuth';
import './ConfigModal.css';

interface Workspace {
  id: string;
  name: string;
}

interface Report {
  id: string;
  name: string;
  embedUrl: string;
}

interface ConfigModalProps {
  open: boolean;
  onClose: () => void;
  onSave: (config: PowerBIConfigData) => void;
}

export interface PowerBIConfigData {
  workspaceId: string;
  reportId: string;
  embedUrl: string;
  autoRefreshInterval: number;
  defaultZoom: string;
  showFilterPanel: boolean;
  showFilterPanelExpanded: boolean;
}

export function ConfigModal({ open, onClose, onSave }: ConfigModalProps) {
  const { getPowerBIToken, user } = useAuth();
  const [workspaces, setWorkspaces] = useState<Workspace[]>([]);
  const [reports, setReports] = useState<Report[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [config, setConfig] = useState<PowerBIConfigData>({
    workspaceId: '',
    reportId: '',
    embedUrl: '',
    autoRefreshInterval: 0,
    defaultZoom: '',
    showFilterPanel: true,
    showFilterPanelExpanded: false,
  });

  useEffect(() => {
    if (open) {
      loadWorkspaces();
    }
  }, [open]);

  useEffect(() => {
    if (config.workspaceId) {
      loadReports(config.workspaceId);
    }
  }, [config.workspaceId]);

  const loadWorkspaces = async () => {
    setLoading(true);
    setError(null);
    try {
      console.log('Acquiring Power BI token...');
      const token = await getPowerBIToken();
      console.log('Power BI token acquired, length:', token?.length);
      console.log('Token preview:', token?.substring(0, 50) + '...');

      // Check if mock mode is enabled via environment variable
      if (import.meta.env.VITE_POWERBI_MOCK_MODE === 'true') {
        console.log('Using mock Power BI data');
        setWorkspaces([
          { id: 'mock-workspace-1', name: 'Sales Analytics (Mock)' },
          { id: 'mock-workspace-2', name: 'Marketing Dashboard (Mock)' },
          { id: 'mock-workspace-3', name: 'Finance Reports (Mock)' },
        ]);
        return;
      }

      const data = await powerBIClient.getWorkspaces(token);
      setWorkspaces(data);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to load workspaces';
      setError(message + '\n\nTip: Set VITE_POWERBI_MOCK_MODE=true in .env to use mock data for development.');
      console.error('Error loading workspaces:', err);
    } finally {
      setLoading(false);
    }
  };

  const loadReports = async (workspaceId: string) => {
    setLoading(true);
    setError(null);
    try {
      // Check if mock mode is enabled
      if (import.meta.env.VITE_POWERBI_MOCK_MODE === 'true') {
        console.log('Using mock Power BI reports');
        setReports([
          { id: 'mock-report-1', name: 'Q4 Sales Report (Mock)', embedUrl: 'https://mock-embed-url.com/report1' },
          { id: 'mock-report-2', name: 'Customer Analytics (Mock)', embedUrl: 'https://mock-embed-url.com/report2' },
          { id: 'mock-report-3', name: 'Revenue Dashboard (Mock)', embedUrl: 'https://mock-embed-url.com/report3' },
        ]);
        return;
      }

      const token = await getPowerBIToken();
      const data = await powerBIClient.getReports(workspaceId, token);
      setReports(data);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to load reports';
      setError(message);
      console.error('Error loading reports:', err);
    } finally {
      setLoading(false);
    }
  };

  const handleWorkspaceChange = (workspaceId: string) => {
    setConfig({ ...config, workspaceId, reportId: '', embedUrl: '' });
  };

  const handleReportChange = (reportId: string) => {
    const report = reports.find(r => r.id === reportId);
    setConfig({
      ...config,
      reportId,
      embedUrl: report?.embedUrl || '',
    });
  };

  const handleSave = () => {
    if (!config.workspaceId || !config.reportId) {
      throw new Error('Workspace and Report must be selected');
    }
    onSave(config);
    onClose();
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Page Set Up</DialogTitle>
      <DialogContent>
        {error && (
          <div style={{ color: 'red', marginBottom: '16px', padding: '8px', backgroundColor: '#ffebee', borderRadius: '4px' }}>
            {error}
          </div>
        )}
        {loading && (
          <div style={{ textAlign: 'center', padding: '16px' }}>
            Loading...
          </div>
        )}
        <FormControl fullWidth margin="normal">
          <InputLabel>Select Workspace</InputLabel>
          <Select
            value={config.workspaceId}
            onChange={(e) => handleWorkspaceChange(e.target.value)}
            label="Select Workspace"
            disabled={loading}
          >
            {workspaces.map((ws) => (
              <MenuItem key={ws.id} value={ws.id}>
                {ws.name}
              </MenuItem>
            ))}
          </Select>
        </FormControl>

        <FormControl fullWidth margin="normal">
          <InputLabel>Select Report</InputLabel>
          <Select
            value={config.reportId}
            onChange={(e) => handleReportChange(e.target.value)}
            label="Select Report"
            disabled={!config.workspaceId || loading}
          >
            {reports.map((report) => (
              <MenuItem key={report.id} value={report.id}>
                {report.name}
              </MenuItem>
            ))}
          </Select>
        </FormControl>

        <TextField
          fullWidth
          margin="normal"
          label="Auto Refresh Interval (sec)"
          type="number"
          value={config.autoRefreshInterval}
          onChange={(e) => setConfig({ ...config, autoRefreshInterval: parseInt(e.target.value) || 0 })}
        />

        <FormControl fullWidth margin="normal">
          <InputLabel>Zoom by default</InputLabel>
          <Select
            value={config.defaultZoom}
            onChange={(e) => setConfig({ ...config, defaultZoom: e.target.value })}
            label="Zoom by default"
          >
            <MenuItem value="">Select</MenuItem>
            <MenuItem value="fitToWidth">Fit to Width</MenuItem>
            <MenuItem value="fitToPage">Fit to Page</MenuItem>
            <MenuItem value="actualSize">Actual Size</MenuItem>
          </Select>
        </FormControl>

        <FormControlLabel
          control={
            <Checkbox
              checked={config.showFilterPanel}
              onChange={(e) => setConfig({ ...config, showFilterPanel: e.target.checked })}
            />
          }
          label="Filter panel"
        />

        <FormControlLabel
          control={
            <Checkbox
              checked={config.showFilterPanelExpanded}
              onChange={(e) => setConfig({ ...config, showFilterPanelExpanded: e.target.checked })}
              disabled={!config.showFilterPanel}
            />
          }
          label="Expanded"
        />

        {config.embedUrl && (
          <TextField
            fullWidth
            margin="normal"
            label="Generated Route"
            value={config.embedUrl}
            InputProps={{ readOnly: true }}
            multiline
          />
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancel</Button>
        <Button onClick={handleSave} variant="contained" color="primary">
          Save
        </Button>
      </DialogActions>
    </Dialog>
  );
}
