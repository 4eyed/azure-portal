#!/usr/bin/env dotnet script

#r "nuget: Microsoft.Data.SqlClient, 5.1.1"
#r "nuget: Azure.Identity, 1.17.0"

using Azure.Core;
using Azure.Identity;
using Microsoft.Data.SqlClient;

var connectionString = Environment.GetEnvironmentVariable("DOTNET_CONNECTION_STRING")
    ?? Environment.GetEnvironmentVariable("MENUAPP_CONNECTION_STRING");

if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("DOTNET_CONNECTION_STRING (or MENUAPP_CONNECTION_STRING) must be set with Authentication=Active Directory Default.");
    return;
}

Console.WriteLine("Connecting to Azure SQL Database using managed identity...");
Console.WriteLine();

var credential = new DefaultAzureCredential();
var token = credential.GetToken(new TokenRequestContext(new[] { "https://database.windows.net//.default" }));

using var connection = new SqlConnection(connectionString);
connection.AccessToken = token.Token;
connection.Open();

Console.WriteLine("✅ Connected successfully! Token expires at: " + token.ExpiresOn);
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
