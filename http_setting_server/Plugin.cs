using ClassIsland.Core;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Controls;
using ClassIsland.Core.Extensions.Registry;
using ClassIsland.Shared;
using http_setting_server.Views.SettingsPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace http_setting_server;

[PluginEntrance]
public class Plugin : PluginBase
{
    private HttpServer? _httpServer;
    private PluginSettings? _settings;
    private const string SettingsFileName = "settings.json";

    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        // 注册设置页面
        services.AddSettingsPage<Views.SettingsPages.HttpServerSettingsPage>();

        AppBase.Current.AppStarted += async (_, _) =>
        {
            // 加载设置
            _settings = LoadSettings();

            // 显示启动通知（如果启用）
            if (_settings.ShowStartupNotification && _settings.IsServerEnabled)
            {
                await CommonTaskDialogs.ShowDialog(
                    "HTTP 设置服务器",
                    $"插件已成功启动！\n\nHTTP 服务器监听端口：{_settings.Port}\n\n你现在可以通过 HTTP API 远程修改组件设置。\n\n详细使用方法请查看插件说明。"
                );
            }

            // 获取组件服务和日志记录器
            var componentsService = IAppHost.GetService<IComponentsService>();
            var logger = IAppHost.GetService<ILogger<HttpServer>>();

            _httpServer = new HttpServer(componentsService, logger, _settings.Port);

            // 如果启用服务器，则启动
            if (_settings.IsServerEnabled)
            {
                _ = Task.Run(() => _httpServer.StartAsync());
            }
        };
    }

    public PluginSettings LoadSettings()
    {
        try
        {
            var settingsPath = Path.Combine(PluginConfigFolder, SettingsFileName);
            if (File.Exists(settingsPath))
            {
                var json = File.ReadAllText(settingsPath);
                var settings = JsonSerializer.Deserialize<PluginSettings>(json);
                if (settings != null)
                {
                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            // 如果加载失败，使用默认设置
            Console.WriteLine($"加载设置失败: {ex.Message}");
        }

        // 返回默认设置
        return new PluginSettings
        {
            IsServerEnabled = true,
            Port = 9900,
            ShowStartupNotification = false
        };
    }

    public void SaveSettings(PluginSettings settings)
    {
        _settings = settings;
        
        try
        {
            var settingsPath = Path.Combine(PluginConfigFolder, SettingsFileName);
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(settingsPath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"保存设置失败: {ex.Message}");
        }
    }

    public void RestartServer(int port)
    {
        if (_httpServer != null)
        {
            _httpServer.Stop();
        }

        var componentsService = IAppHost.GetService<IComponentsService>();
        var logger = IAppHost.GetService<ILogger<HttpServer>>();

        _httpServer = new HttpServer(componentsService, logger, port);
        _ = Task.Run(() => _httpServer.StartAsync());
    }
}