using ModelContextProtocol.Server;
using System.ComponentModel;

/// <summary>
/// Mathematical operation tools for the MCP server.
/// Provides basic arithmetic operations with proper error handling.
/// </summary>
[McpServerToolType]
public static class MathTools
{
    [McpServerTool, Description("Adds two numbers together")]
    public static double Add(
        [Description("The first number")] double a, 
        [Description("The second number")] double b)
    {
        return a + b;
    }

    [McpServerTool, Description("Subtracts the second number from the first")]
    public static double Subtract(
        [Description("The first number")] double a, 
        [Description("The second number")] double b)
    {
        return a - b;
    }

    [McpServerTool, Description("Multiplies two numbers together")]
    public static double Multiply(
        [Description("The first number")] double a, 
        [Description("The second number")] double b)
    {
        return a * b;
    }

    [McpServerTool, Description("Divides the first number by the second")]
    public static double Divide(
        [Description("The dividend")] double a, 
        [Description("The divisor")] double b)
    {
        if (b == 0)
            throw new ArgumentException("Division by zero is not allowed");
        
        return a / b;
    }
}