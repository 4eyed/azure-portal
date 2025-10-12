import { useState } from 'react';
import { MenuGroup } from '../Navigation/MenuGroup';
import { GroupForm, GroupFormData } from '../Admin/GroupForm';
import { useAdminMode } from '../../hooks/useAdminMode';
import { useAuth } from '../../auth/useAuth';
import { useMenu } from '../../contexts/MenuContext';
import { menuClient } from '../../services/menu/client';
import './Sidebar.css';

export function Sidebar() {
  const { user } = useAuth();
  const { isAdminMode, toggleAdminMode } = useAdminMode();
  const { menuGroups, reloadMenu } = useMenu();
  const [searchQuery, setSearchQuery] = useState('');
  const [showNewGroup, setShowNewGroup] = useState(false);

  const handleCreateGroup = async (formData: GroupFormData) => {
    if (!user) return;

    try {
      const username = user.username || 'alice';
      await menuClient.createMenuGroup(
        {
          name: formData.name,
          icon: formData.icon,
          displayOrder: formData.displayOrder,
          isVisible: true,
        },
        username
      );
      await reloadMenu();
      setShowNewGroup(false);
    } catch (error) {
      console.error('Error creating menu group:', error);
      alert(error instanceof Error ? error.message : 'Failed to create menu group');
    }
  };

  return (
    <aside className="sidebar">
      <div className="sidebar-header">
        <h1 className="logo">JA PORTAL</h1>
        <button className="fullscreen-toggle">⛶</button>
      </div>

      <div className="sidebar-icons">
        <button className="icon-btn" title="Home">🏠</button>
        <button className="icon-btn" title="Menu">☰</button>
        <button className="icon-btn" title="Settings">⚙️</button>
        <button className="icon-btn" title="Help">?</button>
        <button className="icon-btn" title="Visibility">👁</button>
      </div>

      <div className="search-box">
        <input
          type="text"
          placeholder="Search..."
          value={searchQuery}
          onChange={(e) => setSearchQuery(e.target.value)}
        />
      </div>

      <nav className="menu-groups">
        {isAdminMode && (
          <button
            className="add-menu-group"
            onClick={() => setShowNewGroup(true)}
          >
            + New Group
          </button>
        )}
        {menuGroups.map((group) => (
          <MenuGroup
            key={group.id}
            group={group}
            isAdminMode={isAdminMode}
          />
        ))}
      </nav>

      <div className="sidebar-footer">
        <div className="admin-toggle">
          <label>
            <span>Admin mode</span>
            <input
              type="checkbox"
              checked={isAdminMode}
              onChange={toggleAdminMode}
            />
          </label>
        </div>
        <div className="user-profile">
          <div className="user-avatar">EE</div>
          <div className="user-details">
            <div className="user-name">{user?.name || 'Eric Entenman'}</div>
            <div className="user-email">{user?.username || 'eentenman@jetstream.com'}</div>
          </div>
        </div>
      </div>

      <GroupForm
        open={showNewGroup}
        onSubmit={handleCreateGroup}
        onClose={() => setShowNewGroup(false)}
      />
    </aside>
  );
}
