using Configuration;
using RemoteMcpKsef.Helpers;

// Handle CLI commands for service management
if (args.Length > 0)
{
    await ArgHelper.Resolve(args);
}

var builder = BuilderHelper.Setup(args);
var app = AppHelper.Setup(builder);

// Start server - use configuration from appsettings.json
var serverConfig = app.Configuration.GetSection(ServerConfiguration.SectionName).Get<ServerConfiguration>() ?? new ServerConfiguration();
var serverUrl = serverConfig.GetUrl();
app.Run(serverUrl);

// Make Program class accessible for testing  
public partial class Program { }
