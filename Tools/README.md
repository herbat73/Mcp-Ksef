# MCP Tools Organization

This directory contains all MCP tool implementations organized following SOLID principles for maintainability and scalability.

## File Organization

### Utility Tools (`UtilityTools.cs`)  
**Purpose**: General-purpose utility functions  
**Tools**: Echo, GetCurrentTime, GenerateRandomNumber  
**Focus**: System utilities and helper functions

### Reflection Tools (`ReflectionTools.cs`)
**Purpose**: Runtime introspection and tool discovery  
**Tools**: ListAllTools, GetToolInfo, ListToolsByCategory, SearchTools, GetServerMetadata  
**Focus**: Self-documentation and dynamic tool analysis

## SOLID Principles Applied

### Single Responsibility Principle ✅
- Each file contains tools for **one specific category**
- Each tool method has **one clear purpose**
- No mixing of concerns across categories

### Open/Closed Principle ✅
- **Open for extension**: Add new tool categories by creating new files
- **Closed for modification**: Adding tools doesn't require changing existing files
- New tools are automatically discovered via assembly scanning

### Dependency Inversion ✅
- Tools depend on MCP abstractions (`[McpServerTool]` attributes)
- No direct dependencies between tool categories
- Framework handles tool registration and discovery

## Adding New Tool Categories

1. **Create new file**: `Tools/YourCategoryTools.cs`
2. **Follow the template**:
```csharp
using ModelContextProtocol.Server;
using System.ComponentModel;

[McpServerToolType]
public static class YourCategoryTools
{
    [McpServerTool, Description("What your tool does")]
    public static ReturnType MethodName(
        [Description("Parameter description")] ParamType param)
    {
        // Implementation with error handling
        return result;
    }
}
```
3. **Automatic discovery**: Tools are found via assembly scanning
4. **Verification**: Use `ListAllTools()` to confirm registration

## Design Patterns

### Attribute-Based Discovery
All tools use MCP attributes for automatic registration:
- `[McpServerToolType]` - Marks tool class for discovery
- `[McpServerTool]` - Marks individual tool methods
- `[Description]` - Provides AI-readable documentation

### Error Handling Strategy
All tools implement comprehensive error handling:
- Input validation with meaningful exceptions
- Graceful error messages for AI consumption
- No silent failures or null returns

### Performance Optimization
- Static classes and methods for efficient execution
- Minimal reflection overhead through LINQ operations
- JSON-serializable responses for MCP transport

## Testing Your Tools

### Quick Verification
```csharp
// Use reflection to verify your tools are registered
ListAllTools()              // Should show your new category
GetToolInfo("YourMethod")   // Should return detailed info
ListToolsByCategory("YourCategory") // Should list your tools
```

### Integration Testing
1. Build and run the server: `dotnet run`
2. Test health: `curl http://localhost:3001/health`
3. Test MCP connection: `claude mcp list`
4. Test tools in your MCP client

## Architecture Benefits

This organization provides:
- **Maintainability**: Easy to locate and modify specific tool categories
- **Scalability**: Add new categories without touching existing code
- **Testability**: Test individual tool categories in isolation
- **Readability**: Clear separation of concerns and focused responsibilities
- **Extensibility**: Framework automatically discovers new tools