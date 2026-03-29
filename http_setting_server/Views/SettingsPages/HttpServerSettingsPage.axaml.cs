using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Shared;
using CommunityToolkit.Mvvm.Input;
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
        
        // 在构造函数中获取 Plugin 实例
        try
        {
            _plugin = IAppHost.GetService<Plugin>();
            if (_plugin != null)
            {
                // 加载当前设置
                if (_viewModel != null)
                {
                    var settings = _plugin.LoadSettings();
                    _viewModel.LoadFromSettings(settings);
                    _viewModel.SetPlugin(_plugin);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"获取 Plugin 实例失败: {ex.Message}");
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
        EnableAuthentication = true;
        AuthToken = string.Empty;

        StartServerCommand = new RelayCommand(StartServer, CanStartServer);
        StopServerCommand = new RelayCommand(StopServer, CanStopServer);
        GenerateTokenCommand = new RelayCommand(GenerateToken);
    }

    public RelayCommand StartServerCommand { get; }
    public RelayCommand StopServerCommand { get; }
    public RelayCommand GenerateTokenCommand { get; }

    public void LoadFromSettings(PluginSettings settings)
    {
        IsServerEnabled = settings.IsServerEnabled;
        Port = settings.Port;
        ShowStartupNotification = settings.ShowStartupNotification;
        EnableAuthentication = settings.EnableAuthentication;
        AuthToken = settings.AuthToken;
        UpdateCommands();
    }

    public void SetPlugin(Plugin plugin)
    {
        _plugin = plugin;
        UpdateCommands();
    }

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs? e)
    {
        base.OnPropertyChanged(e!);

        // 当设置修改时，保存并应用
        if (_plugin != null)
        {
            _plugin.SaveSettings(this);

            // 如果端口改变，重启服务器
            if (e?.PropertyName == nameof(Port))
            {
                _plugin.RestartServer(Port);
            }

            UpdateCommands();
        }
    }

    private void UpdateCommands()
    {
        StartServerCommand.NotifyCanExecuteChanged();
        StopServerCommand.NotifyCanExecuteChanged();
    }

    private bool CanStartServer()
    {
        return _plugin != null && !_plugin.IsServerRunning();
    }

    private bool CanStopServer()
    {
        return _plugin != null && _plugin.IsServerRunning();
    }

    private void StartServer()
    {
        if (_plugin != null)
        {
            IsServerEnabled = true;
            _plugin.SaveSettings(this);
            _plugin.StartServer(Port);
            UpdateCommands();
        }
    }

    private void StopServer()
    {
        if (_plugin != null)
        {
            IsServerEnabled = false;
            _plugin.SaveSettings(this);
            _plugin.StopServer();
            UpdateCommands();
        }
    }

    private void GenerateToken()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var random = new Random();
        var token = new char[16];
        for (int i = 0; i < token.Length; i++)
        {
            token[i] = chars[random.Next(chars.Length)];
        }
        AuthToken = new string(token);

        // 保存设置
        if (_plugin != null)
        {
            _plugin.SaveSettings(this);
        }
    }
}