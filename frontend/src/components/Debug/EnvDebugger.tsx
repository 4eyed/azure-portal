import { useState } from 'react';
import { Box, Card, CardContent, Typography, IconButton, Collapse, Chip } from '@mui/material';
import { BugReport, ExpandMore, ExpandLess } from '@mui/icons-material';

interface EnvVar {
  name: string;
  value: string | undefined;
  required: boolean;
  category: 'Azure AD' | 'API' | 'Power BI' | 'Build';
}

export function EnvDebugger() {
  const [expanded, setExpanded] = useState(false);

  const envVars: EnvVar[] = [
    // Azure AD
    {
      name: 'VITE_AZURE_CLIENT_ID',
      value: import.meta.env.VITE_AZURE_CLIENT_ID,
      required: true,
      category: 'Azure AD',
    },
    {
      name: 'VITE_AZURE_TENANT_ID',
      value: import.meta.env.VITE_AZURE_TENANT_ID,
      required: true,
      category: 'Azure AD',
    },
    {
      name: 'VITE_AZURE_REDIRECT_URI',
      value: import.meta.env.VITE_AZURE_REDIRECT_URI,
      required: true,
      category: 'Azure AD',
    },
    // API
    {
      name: 'VITE_API_URL',
      value: import.meta.env.VITE_API_URL,
      required: false,
      category: 'API',
    },
    // Power BI
    {
      name: 'VITE_POWERBI_WORKSPACE_ID',
      value: import.meta.env.VITE_POWERBI_WORKSPACE_ID,
      required: false,
      category: 'Power BI',
    },
    {
      name: 'VITE_POWERBI_REPORT_ID',
      value: import.meta.env.VITE_POWERBI_REPORT_ID,
      required: false,
      category: 'Power BI',
    },
    {
      name: 'VITE_POWERBI_EMBED_URL',
      value: import.meta.env.VITE_POWERBI_EMBED_URL,
      required: false,
      category: 'Power BI',
    },
    // Build info
    {
      name: 'DEV',
      value: import.meta.env.DEV ? 'true' : 'false',
      required: true,
      category: 'Build',
    },
    {
      name: 'PROD',
      value: import.meta.env.PROD ? 'true' : 'false',
      required: true,
      category: 'Build',
    },
    {
      name: 'MODE',
      value: import.meta.env.MODE,
      required: true,
      category: 'Build',
    },
  ];

  const getStatusColor = (envVar: EnvVar): 'success' | 'warning' | 'error' => {
    if (!envVar.value || envVar.value === 'undefined') {
      return envVar.required ? 'error' : 'warning';
    }
    return 'success';
  };

  const getStatusText = (envVar: EnvVar): string => {
    if (!envVar.value || envVar.value === 'undefined') {
      return envVar.required ? 'MISSING (Required)' : 'Not set (Optional)';
    }
    return 'Set';
  };

  const maskValue = (value: string | undefined): string => {
    if (!value || value === 'undefined') return 'undefined';
    // Mask client IDs and tenant IDs for security
    if (value.length > 20 && value.includes('-')) {
      return value.slice(0, 8) + '...' + value.slice(-4);
    }
    // Show URLs fully
    if (value.startsWith('http')) return value;
    // Show short values fully
    if (value.length <= 20) return value;
    // Mask other long values
    return value.slice(0, 10) + '...' + value.slice(-4);
  };

  const groupedVars = envVars.reduce((acc, envVar) => {
    if (!acc[envVar.category]) {
      acc[envVar.category] = [];
    }
    acc[envVar.category].push(envVar);
    return acc;
  }, {} as Record<string, EnvVar[]>);

  const hasErrors = envVars.some(
    (v) => v.required && (!v.value || v.value === 'undefined')
  );

  // Only show in development by default, or if there are errors
  if (!import.meta.env.DEV && !hasErrors && !expanded) {
    return null;
  }

  return (
    <Box
      sx={{
        position: 'fixed',
        bottom: 16,
        right: 16,
        zIndex: 9999,
        maxWidth: expanded ? 600 : 'auto',
      }}
    >
      {!expanded ? (
        <IconButton
          onClick={() => setExpanded(true)}
          sx={{
            bgcolor: hasErrors ? 'error.main' : 'primary.main',
            color: 'white',
            '&:hover': {
              bgcolor: hasErrors ? 'error.dark' : 'primary.dark',
            },
            boxShadow: 3,
          }}
        >
          <BugReport />
        </IconButton>
      ) : (
        <Card sx={{ boxShadow: 4 }}>
          <CardContent>
            <Box display="flex" alignItems="center" justifyContent="space-between" mb={2}>
              <Box display="flex" alignItems="center" gap={1}>
                <BugReport />
                <Typography variant="h6">Environment Variables</Typography>
                {hasErrors && (
                  <Chip label="ERRORS" color="error" size="small" />
                )}
              </Box>
              <IconButton onClick={() => setExpanded(false)} size="small">
                <ExpandLess />
              </IconButton>
            </Box>

            {Object.entries(groupedVars).map(([category, vars]) => (
              <Box key={category} mb={2}>
                <Typography variant="subtitle2" color="text.secondary" gutterBottom>
                  {category}
                </Typography>
                {vars.map((envVar) => (
                  <Box
                    key={envVar.name}
                    sx={{
                      display: 'flex',
                      justifyContent: 'space-between',
                      alignItems: 'center',
                      py: 0.5,
                      borderBottom: '1px solid',
                      borderColor: 'divider',
                    }}
                  >
                    <Box flex={1}>
                      <Typography variant="body2" fontFamily="monospace" fontWeight="bold">
                        {envVar.name}
                      </Typography>
                      <Typography variant="caption" color="text.secondary" fontFamily="monospace">
                        {maskValue(envVar.value)}
                      </Typography>
                    </Box>
                    <Chip
                      label={getStatusText(envVar)}
                      color={getStatusColor(envVar)}
                      size="small"
                      sx={{ ml: 1 }}
                    />
                  </Box>
                ))}
              </Box>
            ))}

            <Typography variant="caption" color="text.secondary" display="block" mt={2}>
              Build Mode: {import.meta.env.MODE} |
              {import.meta.env.DEV ? ' Development' : ' Production'}
            </Typography>

            {hasErrors && (
              <Typography variant="caption" color="error" display="block" mt={1}>
                ⚠️ Missing required environment variables. Check configuration.
              </Typography>
            )}
          </CardContent>
        </Card>
      )}
    </Box>
  );
}
