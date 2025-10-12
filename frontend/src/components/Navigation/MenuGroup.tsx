import { useState } from 'react';
import { useMsal } from '@azure/msal-react';
import { MenuItem } from './MenuItem';
import { MenuItemForm } from '../Admin/MenuItemForm';
import { useMenu } from '../../contexts/MenuContext';
import { useAuth } from '../../auth/useAuth';
import { menuClient } from '../../services/menu/client';
import './MenuGroup.css';

interface MenuItemData {
  id: number;
  name: string;
  icon: string;
  url: string;
  description: string;
  type: string;
}

interface MenuGroupProps {
  group: {
    id: number;
    name: string;
    icon: string;
    items: MenuItemData[];
  };
  isAdminMode: boolean;
}

export function MenuGroup({ group, isAdminMode }: MenuGroupProps) {
  const [isExpanded, setIsExpanded] = useState(true);
  const [showNewItemForm, setShowNewItemForm] = useState(false);
  const [loading, setLoading] = useState(false);
  const { reloadMenu } = useMenu();
  const { user } = useAuth();
  const { instance } = useMsal();

  const handleNewMenuItem = async (formData: any) => {
    if (!user) return;

    setLoading(true);
    try {
      const username = user.username || 'alice';
      await menuClient.createMenuItem(
        instance,
        {
          name: formData.name,
          icon: formData.icon,
          url: formData.url,
          description: formData.description,
          type: formData.type,
          menuGroupId: group.id,
          displayOrder: group.items.length,
          isVisible: true,
          powerBIConfig: formData.powerBIConfig,
        },
        username
      );
      await reloadMenu();
      setShowNewItemForm(false);
    } catch (error) {
      console.error('Error creating menu item:', error);
      alert(error instanceof Error ? error.message : 'Failed to create menu item');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="menu-group">
      <div className="menu-group-header-container">
        <button
          className="menu-group-header"
          onClick={() => setIsExpanded(!isExpanded)}
        >
          <span className="menu-group-icon">{group.icon}</span>
          <span className="menu-group-name">{group.name}</span>
          <span className="menu-group-toggle">{isExpanded ? '▼' : '▶'}</span>
        </button>
        {isAdminMode && (
          <div className="menu-group-actions">
            <button
              className="action-btn"
              title="Visibility"
              onClick={(e) => {
                e.stopPropagation();
                // TODO: Toggle visibility
              }}
            >
              👁
            </button>
            <button
              className="action-btn"
              title="Edit"
              onClick={(e) => {
                e.stopPropagation();
                // TODO: Edit group
              }}
            >
              ✏️
            </button>
          </div>
        )}
      </div>
      {isExpanded && (
        <div className="menu-group-items">
          {group.items.map((item) => (
            <MenuItem key={item.id} item={item} isAdminMode={isAdminMode} />
          ))}
          {isAdminMode && !showNewItemForm && (
            <button
              className="add-menu-item"
              onClick={() => setShowNewItemForm(true)}
              disabled={loading}
            >
              + New Menu Item
            </button>
          )}
          {isAdminMode && showNewItemForm && (
            <div className="menu-item-form-container">
              <MenuItemForm
                groupId={group.id}
                onSubmit={handleNewMenuItem}
                onCancel={() => setShowNewItemForm(false)}
              />
            </div>
          )}
        </div>
      )}
    </div>
  );
}
