import { useMemo, useState } from 'react';
import { Alert, AlertTitle, Box, Button, Collapse, Stack, Typography } from '@mui/material';
import { clearDevAuthOverride, devAuthIsEnabled, setDevAuthRolesOverride, useDevAuthState } from '../../auth/devAuthStore';

function toggleAdminRole(currentRoles: string[]): string[] {
  const hasAdmin = currentRoles.some((role) => role.toLowerCase() === 'admin');
  if (hasAdmin) {
    return currentRoles.filter((role) => role.toLowerCase() !== 'admin');
  }

  return [...currentRoles, 'admin'];
}

export function DevAuthBanner() {
  const [dismissed, setDismissed] = useState(false);
  const devState = useDevAuthState();

  const isActive = useMemo(() => devAuthIsEnabled() && !dismissed, [dismissed]);

  if (!import.meta.env.DEV || !isActive) {
    return null;
  }

  const { header, displayName, userId, roles, baseRoles, isOverrideActive } = devState;

  return (
    <Box
      sx={{
        position: 'fixed',
        top: 16,
        right: 16,
        zIndex: 1300,
        width: 360,
        maxWidth: 'calc(100% - 32px)',
      }}
    >
      <Collapse in={!dismissed}>
        <Alert
          severity={header ? 'info' : 'warning'}
          variant="outlined"
          onClose={() => setDismissed(true)}
          sx={{ boxShadow: 3, bgcolor: 'background.paper' }}
        >
          <AlertTitle>Local Authentication Helper</AlertTitle>
          {header ? (
            <Stack spacing={1}>
              <Typography variant="body2">
                Injecting <code>X-MS-CLIENT-PRINCIPAL</code> header for <strong>{displayName ?? 'Signed-in user'}</strong>.
              </Typography>
              <Typography variant="caption" color="text.secondary">
                Object ID: {userId ?? 'unknown'}
              </Typography>
              <Typography variant="caption" color="text.secondary">
                Roles: {roles.length > 0 ? roles.join(', ') : 'none'}
              </Typography>
              <Stack direction="row" spacing={1} mt={1}>
                <Button
                  size="small"
                  variant={roles.some((role) => role.toLowerCase() === 'admin') ? 'contained' : 'outlined'}
                  onClick={() => {
                    const sourceRoles = isOverrideActive ? roles : baseRoles;
                    setDevAuthRolesOverride(toggleAdminRole(sourceRoles));
                  }}
                >
                  {roles.some((role) => role.toLowerCase() === 'admin') ? 'Remove Admin' : 'Add Admin'}
                </Button>
                <Button
                  size="small"
                  variant={isOverrideActive ? 'outlined' : 'text'}
                  onClick={() => clearDevAuthOverride()}
                  disabled={!isOverrideActive}
                >
                  Reset Roles
                </Button>
              </Stack>
            </Stack>
          ) : (
            <Typography variant="body2">
              Sign in with Entra ID to mirror Static Web Apps authentication locally. Once signed in, API requests will include
              your user identity automatically.
            </Typography>
          )}
        </Alert>
      </Collapse>
    </Box>
  );
}
