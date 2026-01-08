using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using RemoteMcpKsef.Consts;

namespace Services;

/// <summary>
/// Cross-platform service management for Remote MCP Server.
/// Handles service installation, status checking, and lifecycle management.
/// </summary>
public class ServiceManager
{
    private readonly int _defaultPort = 3001;

    /// <summary>
    /// Shows the current status of the MCP server service.
    /// </summary>
    public async Task ShowStatusAsync()
    {
        try
        {
            Console.WriteLine("Remote MCP KSeF Server Status");
            Console.WriteLine("========================");
            
            // Check if process is running
            bool isRunning = await IsServiceRunningAsync();
            Console.WriteLine($"Service Status: {(isRunning ? "✅ Running" : "❌ Stopped")}");
            
            // Check if port is accessible
            if (isRunning)
            {
                bool portAccessible = await IsPortAccessibleAsync(_defaultPort);
                Console.WriteLine($"Port {_defaultPort}: {(portAccessible ? "✅ Accessible" : "❌ Not responding")}");
                
                if (portAccessible)
                {
                    var serverInfo = await GetServerInfoAsync();
                    if (serverInfo.HasValue)
                    {
                        var info = serverInfo.Value;
                        if (info.TryGetProperty("version", out var version))
                        {
                            Console.WriteLine($"Server Version: {version.GetString()}");
                        }
                        if (info.TryGetProperty("transport", out var transport))
                        {
                            Console.WriteLine($"Transport: {transport.GetString()}");
                        }
                    }
                }
            }
            
            // Show system information
            Console.WriteLine($"Platform: {Environment.OSVersion.Platform}");
            Console.WriteLine($"Process ID: {(isRunning ? GetServiceProcessId() : "N/A")}");
            Console.WriteLine($"Working Directory: {Environment.CurrentDirectory}");
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error checking status: {ex.Message}");
        }
    }

    /// <summary>
    /// Stops the running MCP server service.
    /// </summary>
    public async Task StopServiceAsync()
    {
        try
        {
            Console.WriteLine("Stopping Remote MCP Server...");
            
            bool wasRunning = await IsServiceRunningAsync();
            if (!wasRunning)
            {
                Console.WriteLine("✅ Service is not running");
                return;
            }
            
            // Try to stop gracefully first
            bool stopped = await StopServiceGracefullyAsync();
            
            if (!stopped)
            {
                Console.WriteLine("⚠️  Graceful shutdown failed, using forceful stop...");
                stopped = await StopServiceForcefullyAsync();
            }
            
            if (stopped)
            {
                Console.WriteLine("✅ Remote MCP Server stopped successfully");
            }
            else
            {
                Console.WriteLine("❌ Failed to stop Remote MCP Server");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error stopping service: {ex.Message}");
        }
    }

    /// <summary>
    /// Installs the MCP server as a system service.
    /// </summary>
    public async Task InstallServiceAsync()
    {
        try
        {
            Console.WriteLine("Installing Remote MCP KSeF Server as system service...");
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                await InstallWindowsServiceAsync();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                await InstallSystemdServiceAsync();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                await InstallLaunchdServiceAsync();
            }
            else
            {
                Console.WriteLine("❌ Service installation not supported on this platform");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error installing service: {ex.Message}");
        }
    }

    /// <summary>
    /// Uninstalls the MCP server system service.
    /// </summary>
    public async Task UninstallServiceAsync()
    {
        try
        {
            Console.WriteLine("Uninstalling Remote MCP KSeF Server system service...");
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                await UninstallWindowsServiceAsync();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                await UninstallSystemdServiceAsync();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                await UninstallLaunchdServiceAsync();
            }
            else
            {
                Console.WriteLine("❌ Service uninstall not supported on this platform");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error uninstalling service: {ex.Message}");
        }
    }

    // Private helper methods
    
    private async Task<bool> IsServiceRunningAsync()
    {
        try
        {
            return await IsPortAccessibleAsync(_defaultPort);
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> IsPortAccessibleAsync(int port)
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(5);
            
            var healthUrl = $"http://localhost:{port}/health";
            var response = await httpClient.GetAsync(healthUrl);
            
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task<JsonElement?> GetServerInfoAsync()
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(5);
            
            var infoUrl = $"http://localhost:{_defaultPort}/info";
            var response = await httpClient.GetStringAsync(infoUrl);
            
            return JsonSerializer.Deserialize<JsonElement>(response);
        }
        catch
        {
            return null;
        }
    }

    private string GetServiceProcessId()
    {
        try
        {
            var processes = Process.GetProcessesByName("remote-mcp");
            return processes.Length > 0 ? processes[0].Id.ToString() : "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }

    private async Task<bool> StopServiceGracefullyAsync()
    {
        try
        {
            // Send graceful shutdown signal if supported
            var processes = Process.GetProcessesByName("remote-mcp");
            
            foreach (var process in processes)
            {
                if (!process.HasExited)
                {
                    process.CloseMainWindow();
                    bool exited = process.WaitForExit(10000); // Wait up to 10 seconds
                    
                    if (exited)
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> StopServiceForcefullyAsync()
    {
        try
        {
            var processes = Process.GetProcessesByName("remote-mcp-ksef");
            
            foreach (var process in processes)
            {
                if (!process.HasExited)
                {
                    process.Kill(true); // Kill the process tree
                    bool exited = process.WaitForExit(5000); // Wait up to 5 seconds
                    
                    if (exited)
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }
        catch
        {
            return false;
        }
    }

    // Platform-specific service installation methods

    private async Task InstallWindowsServiceAsync()
    {
        var executablePath = Environment.ProcessPath;
        var arguments = "--daemon";
        
        var installCommand = $"sc create \"{AppConsts.ServiceName}\" binPath= \"\\\"{executablePath}\\\" {arguments}\" DisplayName= \"{AppConsts.AppName}\" start= auto";
        
        var result = await RunCommandAsync("cmd", $"/c {installCommand}");
        
        if (result.ExitCode == 0)
        {
            Console.WriteLine("✅ Windows service installed successfully");
            Console.WriteLine($"   Use: sc start {AppConsts.ServiceName}");
            Console.WriteLine($"   Use: sc stop {AppConsts.ServiceName}");
        }
        else
        {
            Console.WriteLine($"❌ Failed to install Windows service: {result.Error}");
        }
    }

    private async Task UninstallWindowsServiceAsync()
    {
        var result = await RunCommandAsync("cmd", $"/c sc delete {AppConsts.ServiceName}");
        
        if (result.ExitCode == 0)
        {
            Console.WriteLine("✅ Windows service uninstalled successfully");
        }
        else
        {
            Console.WriteLine($"❌ Failed to uninstall Windows service: {result.Error}");
        }
    }

    private async Task InstallSystemdServiceAsync()
    {
        var executablePath = Environment.ProcessPath;
        var serviceContent = $@"[Unit]
Description={AppConsts.AppName}
After=network.target

[Service]
Type=notify
ExecStart={executablePath} --daemon
Restart=always
RestartSec=5
KillSignal=SIGINT
SyslogIdentifier=remote-mcp
User=nobody
Group=nogroup

[Install]
WantedBy=multi-user.target
";

        var serviceFile = $"/etc/systemd/system/{AppConsts.ServiceName}.service";
        await File.WriteAllTextAsync(serviceFile, serviceContent);
        
        await RunCommandAsync("systemctl", "daemon-reload");
        await RunCommandAsync("systemctl", $"enable {AppConsts.ServiceName}");
        
        Console.WriteLine("✅ systemd service installed successfully");
        Console.WriteLine($"   Use: sudo systemctl start {AppConsts.ServiceName}");
        Console.WriteLine($"   Use: sudo systemctl stop {AppConsts.ServiceName}");
    }

    private async Task UninstallSystemdServiceAsync()
    {
        await RunCommandAsync("systemctl", $"stop {AppConsts.ServiceName}");
        await RunCommandAsync("systemctl", $"disable {AppConsts.ServiceName}");
        
        var serviceFile = $"/etc/systemd/system/{AppConsts.ServiceName}.service";
        if (File.Exists(serviceFile))
        {
            File.Delete(serviceFile);
        }
        
        await RunCommandAsync("systemctl", "daemon-reload");
        
        Console.WriteLine("✅ systemd service uninstalled successfully");
    }

    private async Task InstallLaunchdServiceAsync()
    {
        var executablePath = Environment.ProcessPath;
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var plistPath = Path.Combine(homeDir, "Library", "LaunchAgents", $"com.{AppConsts.ServiceName}.plist");
        
        var plistContent = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
<dict>
    <key>Label</key>
    <string>com.{AppConsts.ServiceName}</string>
    <key>ProgramArguments</key>
    <array>
        <string>{executablePath}</string>
        <string>--daemon</string>
    </array>
    <key>WorkingDirectory</key>
    <string>{Environment.CurrentDirectory}</string>
    <key>StandardOutPath</key>
    <string>/tmp/{AppConsts.ServiceName}-stdout.log</string>
    <key>StandardErrorPath</key>
    <string>/tmp/{AppConsts.ServiceName}-stderr.log</string>
    <key>KeepAlive</key>
    <true/>
    <key>RunAtLoad</key>
    <false/>
</dict>
</plist>";

        Directory.CreateDirectory(Path.GetDirectoryName(plistPath)!);
        await File.WriteAllTextAsync(plistPath, plistContent);
        
        Console.WriteLine("✅ launchd service installed successfully");
        Console.WriteLine($"   Use: launchctl load {plistPath}");
        Console.WriteLine($"   Use: launchctl unload {plistPath}");
    }

    private async Task UninstallLaunchdServiceAsync()
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var plistPath = Path.Combine(homeDir, "Library", "LaunchAgents", $"com.{AppConsts.ServiceName}.plist");
        
        if (File.Exists(plistPath))
        {
            await RunCommandAsync("launchctl", $"unload {plistPath}");
            File.Delete(plistPath);
        }
        
        Console.WriteLine("✅ launchd service uninstalled successfully");
    }

    private async Task<(int ExitCode, string Output, string Error)> RunCommandAsync(string command, string arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        
        await process.WaitForExitAsync();
        
        var output = await outputTask;
        var error = await errorTask;
        
        return (process.ExitCode, output, error);
    }
}