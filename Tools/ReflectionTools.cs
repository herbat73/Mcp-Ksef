using System.ComponentModel;
using System.Reflection;
using ModelContextProtocol.Server;

namespace RemoteMcpKsef.Tools;

/// <summary>
/// Reflection and introspection tools for the MCP server.
/// Provides runtime tool discovery, analysis, and self-documentation capabilities.
/// Implements MCP's dynamic discovery patterns for enhanced debugging and maintenance.
/// </summary>
[McpServerToolType]
public static class ReflectionTools
{
    [McpServerTool, Description("Lists all available MCP tools with their descriptions and parameters")]
    public static object ListAllTools()
    {
        var toolTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() != null);

        var toolsInfo = new List<object>();

        foreach (var type in toolTypes)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() != null);

            foreach (var method in methods)
            {
                var toolAttr = method.GetCustomAttribute<McpServerToolAttribute>();
                var descAttr = method.GetCustomAttribute<DescriptionAttribute>();
                
                var parameters = method.GetParameters().Select(p => new
                {
                    name = p.Name,
                    type = p.ParameterType.Name,
                    description = p.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "No description",
                    hasDefaultValue = p.HasDefaultValue,
                    defaultValue = p.HasDefaultValue ? p.DefaultValue?.ToString() : null
                }).ToArray();

                toolsInfo.Add(new
                {
                    category = type.Name.Replace("Tools", ""),
                    name = toolAttr?.Name ?? method.Name,
                    description = descAttr?.Description ?? "No description",
                    returnType = method.ReturnType.Name,
                    parameters = parameters,
                    methodInfo = $"{type.Name}.{method.Name}"
                });
            }
        }

        return new { 
            totalTools = toolsInfo.Count,
            categories = toolsInfo.GroupBy(t => ((dynamic)t).category).Select(g => g.Key).ToArray(),
            tools = toolsInfo 
        };
    }

    [McpServerTool, Description("Gets detailed information about a specific tool by name")]
    public static object GetToolInfo([Description("The name of the tool to inspect")] string toolName)
    {
        var toolTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() != null);

        foreach (var type in toolTypes)
        {
            var method = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => 
                {
                    var toolAttr = m.GetCustomAttribute<McpServerToolAttribute>();
                    var methodName = toolAttr?.Name ?? m.Name;
                    return methodName.Equals(toolName, StringComparison.OrdinalIgnoreCase);
                });

            if (method != null)
            {
                var toolAttr = method.GetCustomAttribute<McpServerToolAttribute>();
                var descAttr = method.GetCustomAttribute<DescriptionAttribute>();
                
                var parameters = method.GetParameters().Select(p => new
                {
                    name = p.Name,
                    type = p.ParameterType.Name,
                    fullTypeName = p.ParameterType.FullName,
                    description = p.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "No description",
                    hasDefaultValue = p.HasDefaultValue,
                    defaultValue = p.HasDefaultValue ? p.DefaultValue?.ToString() : null,
                    isOptional = p.IsOptional,
                    position = p.Position
                }).ToArray();

                return new
                {
                    found = true,
                    category = type.Name.Replace("Tools", ""),
                    name = toolAttr?.Name ?? method.Name,
                    description = descAttr?.Description ?? "No description",
                    returnType = method.ReturnType.Name,
                    returnTypeFullName = method.ReturnType.FullName,
                    parameters = parameters,
                    parameterCount = parameters.Length,
                    methodInfo = $"{type.Name}.{method.Name}",
                    declaringType = type.FullName
                };
            }
        }

        return new { found = false, message = $"Tool '{toolName}' not found" };
    }

    [McpServerTool, Description("Lists tools by category")]
    public static object ListToolsByCategory([Description("The category to filter by (Math, Utility, Data, Reflection)")] string category)
    {
        var targetCategory = category.Trim();
        if (!targetCategory.EndsWith("Tools"))
            targetCategory += "Tools";

        var toolType = Assembly.GetExecutingAssembly()
            .GetTypes()
            .FirstOrDefault(t => t.Name.Equals(targetCategory, StringComparison.OrdinalIgnoreCase) && 
                                 t.GetCustomAttribute<McpServerToolTypeAttribute>() != null);

        if (toolType == null)
        {
            var availableCategories = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() != null)
                .Select(t => t.Name.Replace("Tools", ""))
                .ToArray();

            return new 
            { 
                found = false, 
                message = $"Category '{category}' not found",
                availableCategories = availableCategories
            };
        }

        var methods = toolType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() != null);

        var tools = methods.Select(method => 
        {
            var toolAttr = method.GetCustomAttribute<McpServerToolAttribute>();
            var descAttr = method.GetCustomAttribute<DescriptionAttribute>();
            
            return new
            {
                name = toolAttr?.Name ?? method.Name,
                description = descAttr?.Description ?? "No description",
                returnType = method.ReturnType.Name,
                parameterCount = method.GetParameters().Length
            };
        }).ToArray();

        return new
        {
            category = toolType.Name.Replace("Tools", ""),
            toolCount = tools.Length,
            tools = tools
        };
    }

    [McpServerTool, Description("Searches for tools containing specific keywords in their name or description")]
    public static object SearchTools([Description("Keywords to search for in tool names and descriptions")] string keywords)
    {
        var searchTerms = keywords.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var toolTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() != null);

        var matchingTools = new List<object>();

        foreach (var type in toolTypes)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() != null);

            foreach (var method in methods)
            {
                var toolAttr = method.GetCustomAttribute<McpServerToolAttribute>();
                var descAttr = method.GetCustomAttribute<DescriptionAttribute>();
                var toolName = (toolAttr?.Name ?? method.Name).ToLowerInvariant();
                var description = (descAttr?.Description ?? "").ToLowerInvariant();

                var nameMatches = searchTerms.Any(term => toolName.Contains(term));
                var descriptionMatches = searchTerms.Any(term => description.Contains(term));

                if (nameMatches || descriptionMatches)
                {
                    matchingTools.Add(new
                    {
                        category = type.Name.Replace("Tools", ""),
                        name = toolAttr?.Name ?? method.Name,
                        description = descAttr?.Description ?? "No description",
                        matchType = nameMatches ? "name" : "description",
                        returnType = method.ReturnType.Name
                    });
                }
            }
        }

        return new
        {
            searchKeywords = keywords,
            matchCount = matchingTools.Count,
            matches = matchingTools
        };
    }

    [McpServerTool, Description("Gets server metadata and reflection capabilities")]
    public static object GetServerMetadata()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var toolTypeCount = assembly.GetTypes()
            .Count(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() != null);
        
        var totalToolCount = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() != null)
            .Sum(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Count(m => m.GetCustomAttribute<McpServerToolAttribute>() != null));

        return new
        {
            serverName = "Remote MCP Server with Reflection",
            version = "1.0.0",
            transport = "streamable-http",
            assemblyName = assembly.GetName().Name,
            assemblyVersion = assembly.GetName().Version?.ToString(),
            dotnetVersion = Environment.Version.ToString(),
            toolCategories = toolTypeCount,
            totalTools = totalToolCount,
            reflectionCapabilities = new[]
            {
                "Dynamic tool discovery",
                "Runtime tool introspection", 
                "Category-based filtering",
                "Keyword search",
                "Parameter analysis",
                "Type information"
            },
            buildTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC")
        };
    }
}