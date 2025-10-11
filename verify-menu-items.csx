#!/usr/bin/env dotnet script

#r "nuget: Microsoft.Data.SqlClient, 5.1.1"

using Microsoft.Data.SqlClient;

var connectionString = "Server=sqlsrv-menu-app-24259.database.windows.net;Database=db-menu-app;User Id=sqladmin;Password=P@ssw0rd1760128283!;Encrypt=true;TrustServerCertificate=false;";

Console.WriteLine("Connecting to Azure SQL Database...");
Console.WriteLine();

using var connection = new SqlConnection(connectionString);
connection.Open();

Console.WriteLine("✅ Connected successfully!");
Console.WriteLine();

// Check if table exists
var checkTableQuery = "SELECT OBJECT_ID('MenuItems', 'U')";
using var checkCmd = new SqlCommand(checkTableQuery, connection);
var tableExists = checkCmd.ExecuteScalar() != DBNull.Value;

if (!tableExists)
{
    Console.WriteLine("❌ MenuItems table does not exist!");
    return;
}

Console.WriteLine("✅ MenuItems table exists");
Console.WriteLine();

// Query menu items
var query = "SELECT Id, Name, Icon, Url, Description FROM MenuItems ORDER BY Id";
using var command = new SqlCommand(query, connection);
using var reader = command.ExecuteReader();

Console.WriteLine("📋 Menu Items in Database:");
Console.WriteLine("─────────────────────────────────────────────────────────────");

int count = 0;
while (reader.Read())
{
    count++;
    Console.WriteLine($"ID: {reader["Id"]}");
    Console.WriteLine($"Name: {reader["Name"]}");
    Console.WriteLine($"Icon: {reader["Icon"]}");
    Console.WriteLine($"URL: {reader["Url"]}");
    Console.WriteLine($"Description: {reader["Description"]}");
    Console.WriteLine("─────────────────────────────────────────────────────────────");
}

Console.WriteLine();
Console.WriteLine($"✅ Total menu items: {count}");
