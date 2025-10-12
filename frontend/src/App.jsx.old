import { useState, useEffect } from 'react'
import './App.css'

const API_URL = import.meta.env.VITE_API_URL || 'https://func-menu-app-18436.azurewebsites.net/api'

function App() {
  const [user, setUser] = useState('alice')
  const [menuItems, setMenuItems] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)

  useEffect(() => {
    loadMenu(user)
  }, [user])

  const loadMenu = async (selectedUser) => {
    setLoading(true)
    setError(null)

    try {
      const response = await fetch(`${API_URL}/menu?user=${selectedUser}`)

      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`)
      }

      const data = await response.json()
      setMenuItems(data.menuItems)
    } catch (err) {
      setError(`Error loading menu: ${err.message}. Make sure the API is running on ${API_URL}`)
      console.error('Error:', err)
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="container">
      <h1>Menu Access Demo</h1>
      <div className="user-info">
        Logged in as: <strong>{user}</strong>
      </div>

      <div className="user-selector">
        <label htmlFor="userSelect">Login as:</label>
        <select
          id="userSelect"
          value={user}
          onChange={(e) => setUser(e.target.value)}
        >
          <option value="alice">Alice (Admin)</option>
          <option value="bob">Bob (Viewer)</option>
          <option value="charlie">Charlie (Editor)</option>
        </select>
      </div>

      {error && <div className="error">{error}</div>}

      {loading && <div className="loading">Loading menu items...</div>}

      {!loading && !error && (
        <ul className="menu-items">
          {menuItems.length === 0 ? (
            <div className="empty">No menu items available</div>
          ) : (
            menuItems.map((item) => (
              <li
                key={item.Id}
                className="menu-item"
                onClick={() => alert(`Navigating to ${item.Name}...`)}
              >
                <span className="menu-item-icon">{item.Icon}</span>
                <span className="menu-item-name">{item.Name}</span>
              </li>
            ))
          )}
        </ul>
      )}
    </div>
  )
}

export default App
