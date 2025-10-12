import { useState } from 'react';
import { TextField, Button, Box, Dialog, DialogTitle, DialogContent, DialogActions } from '@mui/material';
import './GroupForm.css';

export interface GroupFormData {
  name: string;
  icon: string;
  displayOrder: number;
}

interface GroupFormProps {
  open: boolean;
  onSubmit: (data: GroupFormData) => void;
  onClose: () => void;
}

export function GroupForm({ open, onSubmit, onClose }: GroupFormProps) {
  const [formData, setFormData] = useState<GroupFormData>({
    name: '',
    icon: '📁',
    displayOrder: 0,
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!formData.name) {
      alert('Group name is required');
      return;
    }
    onSubmit(formData);
    setFormData({ name: '', icon: '📁', displayOrder: 0 });
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>New Menu Group</DialogTitle>
      <DialogContent>
        <Box component="form" onSubmit={handleSubmit}>
          <TextField
            fullWidth
            margin="normal"
            label="Group Name"
            value={formData.name}
            onChange={(e) => setFormData({ ...formData, name: e.target.value })}
            required
            autoFocus
          />
          <TextField
            fullWidth
            margin="normal"
            label="Icon (emoji)"
            value={formData.icon}
            onChange={(e) => setFormData({ ...formData, icon: e.target.value })}
            placeholder="📁"
          />
          <TextField
            fullWidth
            margin="normal"
            label="Display Order"
            type="number"
            value={formData.displayOrder}
            onChange={(e) => setFormData({ ...formData, displayOrder: parseInt(e.target.value) || 0 })}
          />
        </Box>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancel</Button>
        <Button onClick={handleSubmit} variant="contained" color="primary">
          Create Group
        </Button>
      </DialogActions>
    </Dialog>
  );
}
