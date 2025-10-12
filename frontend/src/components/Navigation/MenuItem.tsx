import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useMsal } from '@azure/msal-react';
import { MenuItemForm, MenuItemFormData } from '../Admin/MenuItemForm';
import { MenuItemTypeEnum } from '../Admin/TypeSelector';
import { useMenu } from '../../contexts/MenuContext';
import { useAuth } from '../../auth/useAuth';
import { menuClient } from '../../services/menu/client';
import './MenuItem.css';

interface MenuItemProps {
  item: {
    id: number;
    name: string;
    icon: string;
    url: string;
    description: string;
    type: string;
  };
  isAdminMode: boolean;
}

export function MenuItem({ item, isAdminMode }: MenuItemProps) {
  const [showEditForm, setShowEditForm] = useState(false);
  const [loading, setLoading] = useState(false);
  const { reloadMenu } = useMenu();
  const { user } = useAuth();
  const { instance } = useMsal();

  const handleEdit = () => {
    setShowEditForm(true);
  };

  const handleToggleVisibility = async (e: React.MouseEvent) => {
    e.preventDefault();
    if (!user) return;

    setLoading(true);
    try {
      const username = user.username || 'alice';
      // Don't include menuGroupId to avoid changing it
      const updateData: any = {
        name: item.name,
        icon: item.icon,
        url: item.url,
        description: item.description,
        type: item.type,
        displayOrder: 0,
        isVisible: true, // Toggle will be handled by API
      };

      await menuClient.updateMenuItem(instance, item.id, updateData, username);
      await reloadMenu();
    } catch (error) {
      console.error('Error toggling visibility:', error);
      alert(error instanceof Error ? error.message : 'Failed to toggle visibility');
    } finally {
      setLoading(false);
    }
  };

  const handleDelete = async () => {
    if (!user) return;

    if (!confirm(`Are you sure you want to delete "${item.name}"?`)) {
      return;
    }

    setLoading(true);
    try {
      const username = user.username || 'alice';
      await menuClient.deleteMenuItem(instance, item.id, username);
      await reloadMenu();
    } catch (error) {
      console.error('Error deleting menu item:', error);
      alert(error instanceof Error ? error.message : 'Failed to delete menu item');
    } finally {
      setLoading(false);
    }
  };

  const handleUpdate = async (formData: MenuItemFormData) => {
    if (!user) return;

    setLoading(true);
    try {
      const username = user.username || 'alice';
      await menuClient.updateMenuItem(
        item.id,
        {
          name: formData.name,
          icon: formData.icon,
          url: formData.url,
          description: formData.description,
          type: formData.type,
          menuGroupId: formData.menuGroupId,
          displayOrder: 0, // Will be set by backend
          isVisible: true,
          powerBIConfig: formData.powerBIConfig,
        },
        username
      );
      await reloadMenu();
      setShowEditForm(false);
    } catch (error) {
      console.error('Error updating menu item:', error);
      alert(error instanceof Error ? error.message : 'Failed to update menu item');
    } finally {
      setLoading(false);
    }
  };

  if (showEditForm && isAdminMode) {
    const initialData: MenuItemFormData = {
      name: item.name,
      icon: item.icon || '',
      url: item.url,
      description: item.description || '',
      type: item.type as MenuItemTypeEnum,
    };

    return (
      <div className="menu-item-edit-container">
        <MenuItemForm
          groupId={0}
          initialData={initialData}
          onSubmit={handleUpdate}
          onCancel={() => setShowEditForm(false)}
        />
      </div>
    );
  }

  return (
    <div className="menu-item-wrapper">
      <Link to={item.url} className="menu-item">
        <span className="menu-item-icon">{item.icon}</span>
        <span className="menu-item-name">{item.name}</span>
      </Link>
      {isAdminMode && (
        <div className="menu-item-actions">
          <button
            className="action-btn"
            title="Edit"
            onClick={handleEdit}
            disabled={loading}
          >
            ✏️
          </button>
          <button
            className="action-btn"
            title="Delete"
            onClick={handleDelete}
            disabled={loading}
          >
            🗑️
          </button>
        </div>
      )}
    </div>
  );
}
