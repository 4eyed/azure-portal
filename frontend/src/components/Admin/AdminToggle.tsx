import { Switch, FormControlLabel } from '@mui/material';
import './AdminToggle.css';

interface AdminToggleProps {
  checked: boolean;
  onChange: () => void;
}

export function AdminToggle({ checked, onChange }: AdminToggleProps) {
  return (
    <FormControlLabel
      control={
        <Switch
          checked={checked}
          onChange={onChange}
          color="success"
        />
      }
      label="Admin mode"
      className="admin-toggle"
    />
  );
}
