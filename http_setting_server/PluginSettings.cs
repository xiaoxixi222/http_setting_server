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
}