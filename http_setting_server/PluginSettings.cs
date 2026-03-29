using CommunityToolkit.Mvvm.ComponentModel;

namespace http_setting_server;

public class PluginSettings : ObservableObject
{
    private bool _isServerEnabled = true;
    private int _port = 9900;
    private bool _showStartupNotification = true;

    public bool IsServerEnabled
    {
        get => _isServerEnabled;
        set
        {
            if (value == _isServerEnabled) return;
            _isServerEnabled = value;
            OnPropertyChanged();
        }
    }

    public int Port
    {
        get => _port;
        set
        {
            if (value == _port) return;
            _port = value;
            OnPropertyChanged();
        }
    }

    public bool ShowStartupNotification
    {
        get => _showStartupNotification;
        set
        {
            if (value == _showStartupNotification) return;
            _showStartupNotification = value;
            OnPropertyChanged();
        }
    }

    private bool _enableAuthentication = true;
    private string _authToken = string.Empty;

    public bool EnableAuthentication
    {
        get => _enableAuthentication;
        set
        {
            if (value == _enableAuthentication) return;
            _enableAuthentication = value;
            OnPropertyChanged();
        }
    }

    public string AuthToken
    {
        get => _authToken;
        set
        {
            if (value == _authToken) return;
            _authToken = value;
            OnPropertyChanged();
        }
    }
}