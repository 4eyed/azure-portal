-- Application Database Schema for Menu App
-- This schema is for application data (separate from OpenFGA tables)
-- OpenFGA tables are managed by OpenFGA migrations

-- Create MenuItems table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MenuItems')
BEGIN
    CREATE TABLE MenuItems (
        Id INT PRIMARY KEY IDENTITY(1,1),
        Name VARCHAR(100) NOT NULL,
        Icon VARCHAR(50),
        Url VARCHAR(200) NOT NULL,
        Description VARCHAR(500)
    );

    -- Seed data
    INSERT INTO MenuItems (Name, Icon, Url, Description) VALUES
    ('Dashboard', '📊', '/dashboard', 'View your dashboard'),
    ('Users', '👥', '/users', 'Manage users'),
    ('Settings', '⚙️', '/settings', 'Application settings'),
    ('Reports', '📈', '/reports', 'View and generate reports');

    PRINT 'MenuItems table created and seeded successfully';
END
ELSE
BEGIN
    PRINT 'MenuItems table already exists';
END
GO
