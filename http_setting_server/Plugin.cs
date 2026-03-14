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

namespace http_setting_server;

[PluginEntrance]
public class Plugin : PluginBase
{
    private HttpServer? _httpServer;
    private PluginSettings? _settings;

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

    private PluginSettings LoadSettings()
    {
        // 这里应该从配置文件加载设置
        // 暂时返回默认设置
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
        // 这里应该保存设置到配置文件
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