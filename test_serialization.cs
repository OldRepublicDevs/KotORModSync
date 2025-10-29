using System;
using KOTORModSync.Core;

class Program
{
static void Main()
{
try
{
// Create a test component
var component = new ModComponent
{
Guid = Guid.NewGuid(),
Name = "Test Component",
Author = "Test Author",
Description = "Test Description"
};

// Test the migrated SerializeComponent method
string serialized = component.SerializeComponent();
Console.WriteLine("Serialization successful!");
Console.WriteLine($"Serialized length: {serialized.Length} characters");
Console.WriteLine($"First 200 chars: {serialized.Substring(0, Math.Min(200, serialized.Length))}...");

// Test deserialization
var deserialized = ModComponent.DeserializeTomlComponent(serialized);
if (deserialized != null)
{
Console.WriteLine("Deserialization successful!");
Console.WriteLine($"Deserialized name: {deserialized.Name}");
Console.WriteLine($"Deserialized author: {deserialized.Author}");
}
else
{
Console.WriteLine("Deserialization failed!");
}
}
catch (Exception ex)
{
Console.WriteLine($"Error: {ex.Message}");
Console.WriteLine($"Stack trace: {ex.StackTrace}");
}
}
}
