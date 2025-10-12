import { useState, useEffect } from 'react';
import { TextField, Button, Box } from '@mui/material';
import { TypeSelector, MenuItemTypeEnum } from './TypeSelector';
import { ConfigModal, PowerBIConfigData } from '../PowerBI/ConfigModal';
import './MenuItemForm.css';

export interface MenuItemFormData {
  name: string;
  icon: string;
  url: string;
  description: string;
  type: MenuItemTypeEnum;
  menuGroupId?: number;
  powerBIConfig?: PowerBIConfigData;
}

interface MenuItemFormProps {
  groupId: number;
  initialData?: MenuItemFormData;
  onSubmit: (data: MenuItemFormData) => void;
  onCancel: () => void;
}

export function MenuItemForm({ groupId, initialData, onSubmit, onCancel }: MenuItemFormProps) {
  const [formData, setFormData] = useState<MenuItemFormData>({
    name: '',
    icon: '',
    url: '',
    description: '',
    type: MenuItemTypeEnum.AppComponent,
    menuGroupId: groupId,
    powerBIConfig: undefined,
  });

  const [showConfigModal, setShowConfigModal] = useState(false);

  useEffect(() => {
    if (initialData) {
      setFormData({
        ...initialData,
        menuGroupId: groupId,
      });
    }
  }, [initialData, groupId]);

  const handleTypeChange = (type: MenuItemTypeEnum) => {
    setFormData({ ...formData, type });
    if (type === MenuItemTypeEnum.PowerBIReport) {
      setShowConfigModal(true);
    }
  };

  const handlePowerBIConfig = (config: PowerBIConfigData) => {
    // Generate proper URL for Power BI report
    const url = `/powerbi/${config.reportId}`;
    setFormData({
      ...formData,
      url,
      powerBIConfig: config,
    });
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!formData.name || !formData.url) {
      throw new Error('Name and URL are required');
    }

    // Only include menuGroupId if we're creating a new item (groupId > 0)
    // When editing, don't change the menuGroupId
    const submitData = { ...formData };
    if (groupId === 0) {
      delete submitData.menuGroupId;
    }

    onSubmit(submitData);
  };

  return (
    <Box component="form" onSubmit={handleSubmit} className="menu-item-form">
      <TextField
        fullWidth
        margin="normal"
        label="Type Name..."
        value={formData.name}
        onChange={(e) => setFormData({ ...formData, name: e.target.value })}
        required
      />

      <TypeSelector
        value={formData.type}
        onChange={handleTypeChange}
      />

      <TextField
        fullWidth
        margin="normal"
        label="Icon (emoji)"
        value={formData.icon}
        onChange={(e) => setFormData({ ...formData, icon: e.target.value })}
      />

      <TextField
        fullWidth
        margin="normal"
        label="URL"
        value={formData.url}
        onChange={(e) => setFormData({ ...formData, url: e.target.value })}
        required
      />

      <TextField
        fullWidth
        margin="normal"
        label="Description"
        value={formData.description}
        onChange={(e) => setFormData({ ...formData, description: e.target.value })}
        multiline
        rows={2}
      />

      <Box className="form-actions">
        <Button onClick={onCancel}>Cancel</Button>
        <Button type="submit" variant="contained" color="primary">
          Save
        </Button>
      </Box>

      <ConfigModal
        open={showConfigModal}
        onClose={() => setShowConfigModal(false)}
        onSave={handlePowerBIConfig}
      />
    </Box>
  );
}
