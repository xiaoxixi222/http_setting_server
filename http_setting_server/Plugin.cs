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
using System;

namespace http_setting_server;

[PluginEntrance]
public class Plugin : PluginBase
{
    private HttpServer? _httpServer;
    private PluginSettings? _settings;
    private const string SettingsFileName = "settings.json";
    private ILogger<Plugin>? _logger;

    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        // 将 Plugin 实例注册为服务，供设置页面使用
        services.AddSingleton<Plugin>(this);
        
        // 注册设置页面
        services.AddSettingsPage<Views.SettingsPages.HttpServerSettingsPage>();

        AppBase.Current.AppStarted += async (_, _) =>
        {
            // 获取日志记录器
            _logger = IAppHost.GetService<ILogger<Plugin>>();
            _logger.LogInformation("=== HTTP 设置服务器插件初始化开始 ===");
            _logger.LogInformation("插件配置文件夹: {PluginConfigFolder}", PluginConfigFolder);

            // 加载设置
            _settings = LoadSettings();
            _logger.LogInformation("设置加载完成 - 启用: {IsEnabled}, 端口: {Port}, 显示通知: {ShowNotification}", 
                _settings.IsServerEnabled, _settings.Port, _settings.ShowStartupNotification);

            // 显示启动通知（如果启用）
            if (_settings.ShowStartupNotification && _settings.IsServerEnabled)
            {
                _logger.LogInformation("显示启动通知对话框");
                await CommonTaskDialogs.ShowDialog(
                    "HTTP 设置服务器",
                    $"插件已成功启动！\n\nHTTP 服务器监听端口：{_settings.Port}\n\n你现在可以通过 HTTP API 远程修改组件设置。\n\n详细使用方法请查看插件说明。"
                );
            }

            // 获取组件服务和日志记录器
            var componentsService = IAppHost.GetService<IComponentsService>();
            var logger = IAppHost.GetService<ILogger<HttpServer>>();

            _logger.LogInformation("创建 HTTP 服务器实例 - 端口: {Port}, 启用鉴权: {EnableAuth}", _settings.Port, _settings.EnableAuthentication);
            _httpServer = new HttpServer(componentsService, logger, _settings, _settings.Port);

            // 如果启用服务器，则启动
            if (_settings.IsServerEnabled)
            {
                _logger.LogInformation("启动 HTTP 服务器");
                _ = Task.Run(() => _httpServer.StartAsync());
            }
            else
            {
                _logger.LogWarning("HTTP 服务器未启用（用户配置）");
            }

            _logger.LogInformation("=== HTTP 设置服务器插件初始化完成 ===");
        };
    }

    public PluginSettings LoadSettings()
    {
        try
        {
            var settingsPath = Path.Combine(PluginConfigFolder, SettingsFileName);
            _logger?.LogInformation("尝试加载设置文件: {SettingsPath}", settingsPath);

            if (File.Exists(settingsPath))
            {
                _logger?.LogInformation("设置文件存在，开始读取");
                var json = File.ReadAllText(settingsPath);
                _logger?.LogDebug("设置文件内容: {JsonContent}", json);

                var settings = JsonSerializer.Deserialize<PluginSettings>(json);
                if (settings != null)
                {
                    _logger?.LogInformation("设置文件解析成功 - 启用: {IsEnabled}, 端口: {Port}, 显示通知: {ShowNotification}, 启用鉴权: {EnableAuth}", 
                        settings.IsServerEnabled, settings.Port, settings.ShowStartupNotification, settings.EnableAuthentication);

                    // 如果 token 为空，生成新的 token
                    if (string.IsNullOrWhiteSpace(settings.AuthToken))
                    {
                        settings.AuthToken = GenerateToken();
                        _logger?.LogInformation("生成新的鉴权 Token: {Token}", settings.AuthToken);
                        // 自动保存设置以保存新的 token
                        SaveSettings(settings);
                    }
                    else
                    {
                        _logger?.LogInformation("使用已存在的鉴权 Token: {Token}", settings.AuthToken);
                    }

                    return settings;
                }
                else
                {
                    _logger?.LogWarning("设置文件解析结果为 null，将使用默认设置");
                }
            }
            else
            {
                _logger?.LogInformation("设置文件不存在，将使用默认设置");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "加载设置文件失败，将使用默认设置");
        }

        // 返回默认设置
        var defaultSettings = new PluginSettings
        {
            IsServerEnabled = true,
            Port = 9900,
            ShowStartupNotification = false,
            EnableAuthentication = true,
            AuthToken = GenerateToken()
        };
        
        _logger?.LogInformation("使用默认设置 - 启用: {IsEnabled}, 端口: {Port}, 显示通知: {ShowNotification}, 启用鉴权: {EnableAuth}, Token: {Token}", 
            defaultSettings.IsServerEnabled, defaultSettings.Port, defaultSettings.ShowStartupNotification, 
            defaultSettings.EnableAuthentication, defaultSettings.AuthToken);

        return defaultSettings;
    }

    public void SaveSettings(PluginSettings settings)
    {
        _settings = settings;

        try
        {
            var settingsPath = Path.Combine(PluginConfigFolder, SettingsFileName);
            _logger?.LogInformation("开始保存设置到: {SettingsPath}", settingsPath);
            _logger?.LogDebug("设置内容 - 启用: {IsEnabled}, 端口: {Port}, 显示通知: {ShowNotification}, 启用鉴权: {EnableAuth}, Token: {Token}",
                settings.IsServerEnabled, settings.Port, settings.ShowStartupNotification, 
                settings.EnableAuthentication, settings.AuthToken);

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            _logger?.LogDebug("保存的 JSON 内容: {JsonContent}", json);

            File.WriteAllText(settingsPath, json);
            _logger?.LogInformation("设置保存成功");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "保存设置文件失败");
        }
    }

    public void RestartServer(int port)
    {
        _logger?.LogInformation("开始重启 HTTP 服务器 - 新端口: {Port}", port);
        
        if (_httpServer != null)
        {
            _logger?.LogInformation("停止当前 HTTP 服务器");
            _httpServer.Stop();
        }

        var componentsService = IAppHost.GetService<IComponentsService>();
        var logger = IAppHost.GetService<ILogger<HttpServer>>();

        _logger?.LogInformation("创建新的 HTTP 服务器实例 - 端口: {Port}, 启用鉴权: {EnableAuth}", port, _settings?.EnableAuthentication);
        _httpServer = new HttpServer(componentsService, logger, _settings!, port);
        
        _logger?.LogInformation("启动 HTTP 服务器");
        _ = Task.Run(() => _httpServer.StartAsync());
    }
    
    public void StartServer(int port)
    {
        _logger?.LogInformation("手动启动 HTTP 服务器 - 端口: {Port}", port);
        
        if (_httpServer != null && _httpServer.IsRunning)
        {
            _logger?.LogWarning("HTTP 服务器已在运行中");
            return;
        }
        
        var componentsService = IAppHost.GetService<IComponentsService>();
        var logger = IAppHost.GetService<ILogger<HttpServer>>();
        _httpServer = new HttpServer(componentsService, logger, _settings!, port);
        _logger?.LogInformation("启动 HTTP 服务器");
        _ = Task.Run(() => _httpServer.StartAsync());
    }
    
    public void StopServer()
    {
        _logger?.LogInformation("手动停止 HTTP 服务器");
        
        if (_httpServer == null || !_httpServer.IsRunning)
        {
            _logger?.LogWarning("HTTP 服务器未运行");
            return;
        }
        
        _httpServer.Stop();
        _logger?.LogInformation("HTTP 服务器已停止");
    }
    
    public bool IsServerRunning()
    {
        return _httpServer != null && _httpServer.IsRunning;
    }

    private string GenerateToken()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var random = new Random();
        var token = new char[16];
        for (int i = 0; i < token.Length; i++)
        {
            token[i] = chars[random.Next(chars.Length)];
        }
        return new string(token);
    }
}