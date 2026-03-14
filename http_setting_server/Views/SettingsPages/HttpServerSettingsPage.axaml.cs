using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using http_setting_server;

namespace http_setting_server.Views.SettingsPages;

[SettingsPageInfo("http_setting_server.settings", "HTTP 服务器")]
public partial class HttpServerSettingsPage : SettingsPageBase
{
    private HttpServerSettingsViewModel? _viewModel;
    private Plugin? _plugin;

    public HttpServerSettingsPage()
    {
        InitializeComponent();
        _viewModel = new HttpServerSettingsViewModel();
        DataContext = _viewModel;
    }

    public void SetPlugin(Plugin plugin)
    {
        _plugin = plugin;
        
        // 加载当前设置
        if (_viewModel != null)
        {
            var settings = plugin.LoadSettings();
            _viewModel.LoadFromSettings(settings);
            _viewModel.SetPlugin(plugin);
        }
    }
}

public class HttpServerSettingsViewModel : PluginSettings
{
    private Plugin? _plugin;

    public HttpServerSettingsViewModel()
    {
        // 初始化设置
        IsServerEnabled = true;
        Port = 9900;
        ShowStartupNotification = false;
        ServerStatus = "已停止";
    }

    public string ServerStatus { get; private set; }

    public void LoadFromSettings(PluginSettings settings)
    {
        IsServerEnabled = settings.IsServerEnabled;
        Port = settings.Port;
        ShowStartupNotification = settings.ShowStartupNotification;
        UpdateServerStatus();
    }

    public void SetPlugin(Plugin plugin)
    {
        _plugin = plugin;
        UpdateServerStatus();
    }

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs? e)
    {
        base.OnPropertyChanged(e);

        // 当设置改变时，保存并应用
        if (_plugin != null)
        {
            _plugin.SaveSettings(this);

            // 如果端口改变，重启服务器
            if (e != null && e.PropertyName == nameof(Port))
            {
                _plugin.RestartServer(Port);
            }

            UpdateServerStatus();
        }
    }

    private void UpdateServerStatus()
    {
        ServerStatus = IsServerEnabled 
            ? $"运行中 (端口 {Port})" 
            : "已停止";
    }
}