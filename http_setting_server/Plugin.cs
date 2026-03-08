using ClassIsland.Core;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Controls;
using ClassIsland.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace http_setting_server;

[PluginEntrance]
public class Plugin : PluginBase
{
    private HttpServer? _httpServer;

    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        AppBase.Current.AppStarted += async (_, _) =>
        {
            // 显示插件启动消息
            await CommonTaskDialogs.ShowDialog(
                "HTTP 设置服务器",
                "插件已成功启动！\n\nHTTP 服务器监听端口：9900\n\n你现在可以通过 HTTP API 远程修改组件设置。\n\n详细使用方法请查看插件说明。"
            );

            // 获取组件服务和日志记录器
            var componentsService = IAppHost.GetService<IComponentsService>();
            var logger = IAppHost.GetService<ILogger<HttpServer>>();

            _httpServer = new HttpServer(componentsService, logger, 9900);

            // 在后台线程启动 HTTP 服务器
            _ = Task.Run(() => _httpServer.StartAsync());
        };
    }
}