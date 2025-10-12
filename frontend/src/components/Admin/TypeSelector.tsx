import { Select, MenuItem, FormControl, InputLabel } from '@mui/material';
import './TypeSelector.css';

export enum MenuItemTypeEnum {
  PowerBIReport = 'PowerBIReport',
  ExternalApp = 'ExternalApp',
  AppComponent = 'AppComponent',
  RemoteModule = 'RemoteModule',
  EmbedHTML = 'EmbedHTML',
}

interface TypeSelectorProps {
  value: MenuItemTypeEnum;
  onChange: (value: MenuItemTypeEnum) => void;
}

export function TypeSelector({ value, onChange }: TypeSelectorProps) {
  return (
    <FormControl fullWidth>
      <InputLabel>Select Type</InputLabel>
      <Select
        value={value}
        onChange={(e) => onChange(e.target.value as MenuItemTypeEnum)}
        label="Select Type"
      >
        <MenuItem value={MenuItemTypeEnum.AppComponent}>App Component</MenuItem>
        <MenuItem value={MenuItemTypeEnum.ExternalApp}>External App</MenuItem>
        <MenuItem value={MenuItemTypeEnum.PowerBIReport}>Power BI Report</MenuItem>
        <MenuItem value={MenuItemTypeEnum.RemoteModule}>Remote Module</MenuItem>
        <MenuItem value={MenuItemTypeEnum.EmbedHTML}>Embed HTML</MenuItem>
      </Select>
    </FormControl>
  );
}
