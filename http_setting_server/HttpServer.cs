using System.Net;
using System.Text;
using System.Text.Json;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Models.Components;
using ClassIsland.Shared;
using Microsoft.Extensions.Logging;

namespace http_setting_server;

public class HttpServer
{
    private readonly HttpListener _listener;
    private readonly IComponentsService _componentsService;
    private readonly ILogger<HttpServer> _logger;
    private readonly int _port;
    private readonly PluginSettings _settings;
    private bool _isRunning;
    private CancellationTokenSource? _cancellationTokenSource;
    private static long _requestIdCounter = 0;
    private const int RequestTimeoutMs = 30000; // 30秒超时

    public bool IsRunning => _isRunning;

    public HttpServer(IComponentsService componentsService, ILogger<HttpServer> logger, PluginSettings settings, int port = 9900)
    {
        _componentsService = componentsService;
        _logger = logger;
        _settings = settings;
        _port = port;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{_port}/");
    }

    private void LogInfo(string message)
    {
        _logger.LogInformation(message);
    }

    private void LogError(string message, Exception? ex = null)
    {
        if (ex != null)
        {
            _logger.LogError(ex, message);
        }
        else
        {
            _logger.LogError(message);
        }
    }

    private void LogWarning(string message)
    {
        _logger.LogWarning(message);
    }

    public async Task StartAsync()
    {
        if (_isRunning)
        {
            LogWarning("HTTP Server is already running");
            return;
        }

        try
        {
            _listener.Start();
            _isRunning = true;
            _cancellationTokenSource = new CancellationTokenSource();

            LogInfo($"HTTP Server started on port {_port}");
            LogInfo($"Listening on http://localhost:{_port}/");

            try
            {
                await ListenAsync(_cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                LogInfo("HTTP Server stopped normally");
            }
        }
        catch (Exception ex)
        {
            LogError("Failed to start HTTP Server", ex);
            throw;
        }
        finally
        {
            Stop();
        }
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        LogInfo("Listening for incoming requests...");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                var requestId = Interlocked.Increment(ref _requestIdCounter);

                // 使用 Task.Run 处理请求，并捕获异常
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await HandleRequestAsync(context, requestId);
                    }
                    catch (Exception ex)
                    {
                        LogError($"Unhandled exception in request handler (Request ID: {requestId})", ex);
                        try
                        {
                            context.Response.StatusCode = 500;
                            var error = Encoding.UTF8.GetBytes($"{{\"error\": \"Internal server error\", \"requestId\": {requestId}}}");
                            context.Response.ContentLength64 = error.Length;
                            await context.Response.OutputStream.WriteAsync(error, 0, error.Length);
                        }
                        catch
                        {
                            // 忽略响应写入错误
                        }
                        finally
                        {
                            try
                            {
                                context.Response.Close();
                            }
                            catch
                            {
                                // 忽略关闭错误
                            }
                        }
                    }
                }, cancellationToken);
            }
            catch (HttpListenerException ex)
            {
                if (_isRunning)
                {
                    LogError($"HttpListenerException while waiting for request", ex);
                }
                break;
            }
            catch (Exception ex)
            {
                LogError("Unexpected error in ListenAsync", ex);
                if (!_isRunning) break;
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context, long requestId)
    {
        var request = context.Request;
        var response = context.Response;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var path = request.Url?.AbsolutePath ?? "";
            var method = request.HttpMethod;
            var remoteEndPoint = request.RemoteEndPoint?.ToString() ?? "unknown";

            LogInfo($"[Request #{requestId}] {method} {path} from {remoteEndPoint}");

            // 验证 token
            if (!AuthenticateRequest(request, requestId))
            {
                response.StatusCode = 401;
                response.ContentType = "application/json; charset=utf-8";
                var error = Encoding.UTF8.GetBytes($"{{\"error\": \"Unauthorized: Invalid or missing authentication token\", \"requestId\": {requestId}}}");
                response.ContentLength64 = error.Length;
                await response.OutputStream.WriteAsync(error, 0, error.Length);
                stopwatch.Stop();
                LogWarning($"[Request #{requestId}] Authentication failed - Status: 401, Time: {stopwatch.ElapsedMilliseconds}ms");
                return;
            }

            // 设置请求超时
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource?.Token ?? default);
            cts.CancelAfter(RequestTimeoutMs);

            var (statusCode, content) = method switch
            {
                "GET" => await HandleGetRequest(path, requestId),
                "POST" => await HandlePostRequest(path, request, requestId),
                _ => (405, $"{{\"error\": \"Method not allowed: {method}\", \"requestId\": {requestId}}}")
            };

            response.StatusCode = statusCode;
            response.ContentType = "application/json; charset=utf-8";
            var buffer = Encoding.UTF8.GetBytes(content);
            response.ContentLength64 = buffer.Length;

            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length, cts.Token);

            stopwatch.Stop();
            LogInfo($"[Request #{requestId}] Completed - Status: {statusCode}, Time: {stopwatch.ElapsedMilliseconds}ms");
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            LogError($"[Request #{requestId}] Request timeout after {stopwatch.ElapsedMilliseconds}ms");
            response.StatusCode = 408;
            var error = Encoding.UTF8.GetBytes($"{{\"error\": \"Request timeout\", \"requestId\": {requestId}}}");
            response.ContentLength64 = error.Length;
            await response.OutputStream.WriteAsync(error, 0, error.Length);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LogError($"[Request #{requestId}] Error handling request after {stopwatch.ElapsedMilliseconds}ms", ex);
            response.StatusCode = 500;
            var error = Encoding.UTF8.GetBytes($"{{\"error\": \"{EscapeJsonString(ex.Message)}\", \"requestId\": {requestId}}}");
            response.ContentLength64 = error.Length;
            try
            {
                await response.OutputStream.WriteAsync(error, 0, error.Length);
            }
            catch
            {
                // 忽略响应写入错误
            }
        }
        finally
        {
            try
            {
                response.Close();
            }
            catch (Exception ex)
            {
                LogWarning($"[Request #{requestId}] Error closing response: {ex.Message}");
            }
        }
    }

    private string EscapeJsonString(string str)
    {
        return str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
    }

    private bool AuthenticateRequest(HttpListenerRequest request, long requestId)
    {
        // 如果未启用鉴权，允许所有请求
        if (!_settings.EnableAuthentication)
        {
            LogInfo($"[Request #{requestId}] 鉴权未启用，允许访问");
            return true;
        }

        // 从 Authorization Header 中提取 token
        var authHeader = request.Headers["Authorization"];
        if (!string.IsNullOrEmpty(authHeader))
        {
            // 格式应该是 "Bearer {token}"
            if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                var token = authHeader.Substring(7).Trim();
                if (token == _settings.AuthToken)
                {
                    LogInfo($"[Request #{requestId}] Token 验证成功");
                    return true;
                }
                else
                {
                    LogWarning($"[Request #{requestId}] Token 验证失败");
                    return false;
                }
            }
        }

        LogWarning($"[Request #{requestId}] 未提供有效的 Authorization Header");
        return false;
    }

    private async Task<(int, string)> HandleGetRequest(string path, long requestId)
    {
        try
        {
            if (path == "/api/components")
            {
                LogInfo($"[Request #{requestId}] Getting all components");
                return await GetAllComponents(requestId);
            }
            else if (path.StartsWith("/api/components/"))
            {
                var parts = path.Split('/');
                // /api/components/{componentId} -> ["", "api", "components", "{componentId}"] -> length 4
                // /api/components/{componentId}/settings -> ["", "api", "components", "{componentId}", "settings"] -> length 5
                if (parts.Length >= 4)
                {
                    var componentId = parts[3];
                    if (parts.Length == 4)
                    {
                        LogInfo($"[Request #{requestId}] Getting component by ID: {componentId}");
                        return await GetComponentById(componentId, requestId);
                    }
                    else if (parts.Length == 5 && parts[4] == "settings")
                    {
                        LogInfo($"[Request #{requestId}] Getting settings for component: {componentId}");
                        return await GetComponentSettings(componentId, requestId);
                    }
                }
            }

            LogWarning($"[Request #{requestId}] Unknown GET path: {path}");
            return (404, $"{{\"error\": \"Not found\", \"path\": \"{EscapeJsonString(path)}\", \"requestId\": {requestId}}}");
        }
        catch (Exception ex)
        {
            LogError($"[Request #{requestId}] Error in HandleGetRequest for path: {path}", ex);
            return (500, $"{{\"error\": \"{EscapeJsonString(ex.Message)}\", \"requestId\": {requestId}}}");
        }
    }

    private async Task<(int, string)> HandlePostRequest(string path, HttpListenerRequest request, long requestId)
    {
        try
        {
            if (path.StartsWith("/api/components/"))
            {
                var parts = path.Split('/');
                // /api/components/{componentId}/settings -> ["", "api", "components", "{componentId}", "settings"] -> length 5
                if (parts.Length == 5 && parts[4] == "settings")
                {
                    var componentId = parts[3];
                    var body = await ReadRequestBodyAsync(request, requestId);
                    LogInfo($"[Request #{requestId}] Updating settings for component: {componentId}");
                    return await UpdateComponentSettings(componentId, body, requestId);
                }
            }
            else if (path == "/api/save")
            {
                LogInfo($"[Request #{requestId}] Saving configuration");
                _componentsService.SaveConfig();
                return (200, $"{{\"success\": true, \"requestId\": {requestId}}}");
            }

            LogWarning($"[Request #{requestId}] Unknown POST path: {path}");
            return (404, $"{{\"error\": \"Not found\", \"path\": \"{EscapeJsonString(path)}\", \"requestId\": {requestId}}}");
        }
        catch (Exception ex)
        {
            LogError($"[Request #{requestId}] Error in HandlePostRequest for path: {path}", ex);
            return (500, $"{{\"error\": \"{EscapeJsonString(ex.Message)}\", \"requestId\": {requestId}}}");
        }
    }

    private async Task<string> ReadRequestBodyAsync(HttpListenerRequest request, long requestId)
    {
        try
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            var body = await reader.ReadToEndAsync();
            LogInfo($"[Request #{requestId}] Request body length: {body.Length} bytes");
            if (body.Length > 0 && body.Length <= 500)
            {
                LogInfo($"[Request #{requestId}] Request body: {body}");
            }
            return body;
        }
        catch (Exception ex)
        {
            LogError($"[Request #{requestId}] Error reading request body", ex);
            throw;
        }
    }

    private async Task<string> ReadRequestBodyAsync(HttpListenerRequest request)
    {
        using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
        return await reader.ReadToEndAsync();
    }

    private Task<(int, string)> GetAllComponents(long requestId)
    {
        try
        {
            var components = new List<object>();
            var profile = _componentsService.CurrentComponents;

            if (profile == null)
            {
                LogError($"[Request #{requestId}] CurrentComponents profile is null");
                return Task.FromResult((500, $"{{\"error\": \"Components profile is null\", \"requestId\": {requestId}}}"));
            }

            if (profile.Lines == null)
            {
                LogWarning($"[Request #{requestId}] Profile has no lines");
                return Task.FromResult((200, "[]"));
            }

            LogInfo($"[Request #{requestId}] Processing {profile.Lines.Count} lines with recursive component search");

            for (int lineIndex = 0; lineIndex < profile.Lines.Count; lineIndex++)
            {
                var line = profile.Lines[lineIndex];
                if (line == null || line.Children == null) continue;

                for (int componentIndex = 0; componentIndex < line.Children.Count; componentIndex++)
                {
                    var component = line.Children[componentIndex];
                    if (component != null)
                    {
                        // 递归处理组件及其子组件
                        ProcessComponentRecursively(component, components, lineIndex, componentIndex, "");
                    }
                }
            }

            LogInfo($"[Request #{requestId}] Found {components.Count} components (including nested components)");
            return Task.FromResult((200, JsonSerializer.Serialize(components)));
        }
        catch (Exception ex)
        {
            LogError($"[Request #{requestId}] Error in GetAllComponents", ex);
            return Task.FromResult((500, $"{{\"error\": \"{EscapeJsonString(ex.Message)}\", \"requestId\": {requestId}}}"));
        }
    }

    /// <summary>
    /// 递归处理组件及其子组件
    /// </summary>
    private void ProcessComponentRecursively(ComponentSettings component, List<object> components, int lineIndex, int componentIndex, string parentPath)
    {
        try
        {
            // 生成唯一标识符：lineIndex-componentIndex[ -childIndex...]
            var uniqueId = $"{lineIndex}-{componentIndex}{parentPath}";

            // 获取组件元数据信息
            var componentInfo = component.AssociatedComponentInfo;
            var componentMeta = new
            {
                guid = componentInfo?.Guid.ToString(),
                name = componentInfo?.Name ?? component.NameCache,
                description = componentInfo?.Description ?? "",
                isComponentContainer = componentInfo?.IsComponentContainer ?? false
            };

            // 获取子组件数量
            var childrenCount = component.Children?.Count ?? 0;

            components.Add(new
            {
                id = uniqueId,  // 使用唯一的位置标识符
                componentTypeId = component.Id,  // 添加组件类型 ID 供参考
                name = component.NameCache,
                lineIndex = lineIndex,
                componentIndex = componentIndex,
                parentId = parentPath.Length > 0 ? $"{lineIndex}-{parentPath}" : null,  // 父组件 ID
                path = parentPath,  // 在容器中的路径
                hasChildren = childrenCount > 0,  // 是否有子组件
                childrenCount = childrenCount,  // 子组件数量
                isVisible = component.IsVisible,
                componentMeta = componentMeta  // 添加组件元数据
            });

            // 如果是容器组件，递归处理子组件
            if (component.Children != null && component.Children.Count > 0)
            {
                LogInfo($"Found container component {uniqueId} with {component.Children.Count} children");
                for (int childIndex = 0; childIndex < component.Children.Count; childIndex++)
                {
                    var child = component.Children[childIndex];
                    if (child != null)
                    {
                        var childPath = parentPath.Length > 0 ? $"{parentPath}-{childIndex}" : $"-{childIndex}";
                        ProcessComponentRecursively(child, components, lineIndex, componentIndex, childPath);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LogError($"Error processing component at line {lineIndex}, index {componentIndex}", ex);
        }
    }

    private Task<(int, string)> GetComponentById(string componentId, long requestId)
    {
        try
        {
            var component = FindComponent(componentId);
            if (component == null)
            {
                LogWarning($"[Request #{requestId}] Component not found: {componentId}");
                return Task.FromResult((404, $"{{\"error\": \"Component not found\", \"componentId\": \"{EscapeJsonString(componentId)}\", \"requestId\": {requestId}}}"));
            }

            // 获取组件元数据信息
            var componentInfo = component.AssociatedComponentInfo;
            var componentMeta = new
            {
                guid = componentInfo?.Guid.ToString(),
                name = componentInfo?.Name ?? component.NameCache,
                description = componentInfo?.Description ?? "",
                isComponentContainer = componentInfo?.IsComponentContainer ?? false,
                settingsType = componentInfo?.SettingsType?.FullName ?? "",
                componentType = componentInfo?.ComponentType?.FullName ?? ""
            };

            // 获取子组件信息
            var childrenInfo = new List<object>();
            if (component.Children != null)
            {
                for (int i = 0; i < component.Children.Count; i++)
                {
                    var child = component.Children[i];
                    if (child != null)
                    {
                        var childInfo = child.AssociatedComponentInfo;
                        childrenInfo.Add(new
                        {
                            index = i,
                            id = $"{componentId}-{i}",
                            name = child.NameCache,
                            componentTypeId = child.Id,
                            componentMeta = new
                            {
                                guid = childInfo?.Guid.ToString(),
                                name = childInfo?.Name ?? child.NameCache,
                                description = childInfo?.Description ?? "",
                                isComponentContainer = childInfo?.IsComponentContainer ?? false
                            }
                        });
                    }
                }
            }

            var result = new
            {
                id = componentId,  // 返回唯一的位置标识符
                componentTypeId = component.Id,  // 添加组件类型 ID
                name = component.NameCache,
                settings = component.Settings,
                isVisible = component.IsVisible,
                opacity = component.Opacity,
                hideOnRule = component.HideOnRule,
                componentMeta = componentMeta,  // 添加组件元数据
                children = childrenInfo  // 添加子组件信息
            };

            LogInfo($"[Request #{requestId}] Successfully retrieved component: {componentId}");
            return Task.FromResult((200, JsonSerializer.Serialize(result)));
        }
        catch (Exception ex)
        {
            LogError($"[Request #{requestId}] Error in GetComponentById for: {componentId}", ex);
            return Task.FromResult((500, $"{{\"error\": \"{EscapeJsonString(ex.Message)}\", \"requestId\": {requestId}}}"));
        }
    }

    private Task<(int, string)> GetComponentSettings(string componentId, long requestId)
    {
        try
        {
            var component = FindComponent(componentId);
            if (component == null)
            {
                LogWarning($"[Request #{requestId}] Component not found: {componentId}");
                return Task.FromResult((404, $"{{\"error\": \"Component not found\", \"componentId\": \"{EscapeJsonString(componentId)}\", \"requestId\": {requestId}}}"));
            }

            if (component.Settings == null)
            {
                LogInfo($"[Request #{requestId}] Component settings is null for: {componentId}");
                return Task.FromResult((200, "{}"));
            }

            LogInfo($"[Request #{requestId}] Successfully retrieved settings for component: {componentId}");
            return Task.FromResult((200, JsonSerializer.Serialize(component.Settings)));
        }
        catch (Exception ex)
        {
            LogError($"[Request #{requestId}] Error in GetComponentSettings for: {componentId}", ex);
            return Task.FromResult((500, $"{{\"error\": \"{EscapeJsonString(ex.Message)}\", \"requestId\": {requestId}}}"));
        }
    }

    private Task<(int, string)> UpdateComponentSettings(string componentId, string body, long requestId)
    {
        try
        {
            var component = FindComponent(componentId);
            if (component == null)
            {
                LogWarning($"[Request #{requestId}] Component not found: {componentId}");
                return Task.FromResult((404, $"{{\"error\": \"Component not found\", \"componentId\": \"{EscapeJsonString(componentId)}\", \"requestId\": {requestId}}}"));
            }

            try
            {
                var settingsDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(body);
                if (settingsDict == null)
                {
                    LogError($"[Request #{requestId}] Failed to deserialize settings JSON for component: {componentId}");
                    return Task.FromResult((400, $"{{\"error\": \"Invalid JSON\", \"requestId\": {requestId}}}"));
                }

                // 使用反射来更新 Settings 对象的属性
                var settings = component.Settings;
                if (settings == null)
                {
                    LogError($"[Request #{requestId}] Component settings is null for: {componentId}");
                    return Task.FromResult((400, $"{{\"error\": \"Component settings is null\", \"requestId\": {requestId}}}"));
                }

                var settingsType = settings.GetType();
                var properties = settingsType.GetProperties();
                var updatedProperties = new List<string>();
                var failedProperties = new List<string>();

                LogInfo($"[Request #{requestId}] Updating {settingsDict.Count} properties for component: {componentId}");

                foreach (var kvp in settingsDict)
                {
                    var property = properties.FirstOrDefault(p =>
                        string.Equals(p.Name, kvp.Key, StringComparison.OrdinalIgnoreCase));

                    if (property != null && property.CanWrite)
                    {
                        try
                        {
                            var value = ConvertJsonValue(kvp.Value, property.PropertyType);
                            property.SetValue(settings, value);
                            updatedProperties.Add(kvp.Key);
                            LogInfo($"[Request #{requestId}] Successfully updated property: {kvp.Key} = {value}");
                        }
                        catch (Exception ex)
                        {
                            var errorMsg = $"Error setting property {kvp.Key}: {ex.Message}";
                            LogWarning($"[Request #{requestId}] {errorMsg}");
                            failedProperties.Add(kvp.Key);
                        }
                    }
                    else
                    {
                        LogWarning($"[Request #{requestId}] Property not found or not writable: {kvp.Key}");
                    }
                }

                LogInfo($"[Request #{requestId}] Update completed - Success: {updatedProperties.Count}, Failed: {failedProperties.Count}");

                if (updatedProperties.Count == 0 && failedProperties.Count > 0)
                {
                    return Task.FromResult((400, $"{{\"error\": \"No properties were updated\", \"requestId\": {requestId}}}"));
                }

                return Task.FromResult((200, $"{{\"success\": true, \"updated\": {updatedProperties.Count}, \"failed\": {failedProperties.Count}, \"requestId\": {requestId}}}"));
            }
            catch (JsonException ex)
            {
                LogError($"[Request #{requestId}] JSON parsing error for component: {componentId}", ex);
                return Task.FromResult((400, $"{{\"error\": \"Invalid JSON: {EscapeJsonString(ex.Message)}\", \"requestId\": {requestId}}}"));
            }
        }
        catch (Exception ex)
        {
            LogError($"[Request #{requestId}] Error in UpdateComponentSettings for: {componentId}", ex);
            return Task.FromResult((500, $"{{\"error\": \"{EscapeJsonString(ex.Message)}\", \"requestId\": {requestId}}}"));
        }
    }

    private object? ConvertJsonValue(JsonElement jsonElement, Type targetType)
    {
        if (jsonElement.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (targetType == typeof(string))
        {
            return jsonElement.GetString();
        }
        else if (targetType == typeof(int) || targetType == typeof(int?))
        {
            return jsonElement.GetInt32();
        }
        else if (targetType == typeof(double) || targetType == typeof(double?))
        {
            return jsonElement.GetDouble();
        }
        else if (targetType == typeof(float) || targetType == typeof(float?))
        {
            return jsonElement.GetSingle();
        }
        else if (targetType == typeof(bool) || targetType == typeof(bool?))
        {
            return jsonElement.GetBoolean();
        }
        else if (targetType == typeof(decimal) || targetType == typeof(decimal?))
        {
            return jsonElement.GetDecimal();
        }
        else if (targetType == typeof(long) || targetType == typeof(long?))
        {
            return jsonElement.GetInt64();
        }
        else if (targetType.IsEnum)
        {
            if (jsonElement.ValueKind == JsonValueKind.String)
            {
                var enumValue = Enum.Parse(targetType, jsonElement.GetString()!);
                return enumValue;
            }
            else
            {
                var underlyingType = Enum.GetUnderlyingType(targetType);
                var value = Convert.ChangeType(jsonElement.GetInt64(), underlyingType);
                return Enum.ToObject(targetType, value);
            }
        }
        else
        {
            // 对于复杂类型，尝试反序列化
            return JsonSerializer.Deserialize(jsonElement.GetRawText(), targetType);
        }
    }

    private ComponentSettings? FindComponent(string componentId)
    {
        try
        {
            var profile = _componentsService.CurrentComponents;
            if (profile == null || profile.Lines == null) return null;

            // 解析唯一标识符：lineIndex-componentIndex[-childIndex...]
            var parts = componentId.Split('-');
            if (parts.Length < 2)
            {
                // 如果不是新的格式，尝试旧的方式（向后兼容）
                LogWarning($"Invalid component ID format: {componentId}, trying legacy search");
                return FindComponentLegacy(componentId);
            }

            if (!int.TryParse(parts[0], out int lineIndex) || !int.TryParse(parts[1], out int componentIndex))
            {
                LogError($"Failed to parse component ID: {componentId}");
                return null;
            }

            // 检查行索引是否有效
            if (lineIndex < 0 || lineIndex >= profile.Lines.Count)
            {
                LogError($"Line index out of range: {lineIndex}");
                return null;
            }

            var line = profile.Lines[lineIndex];
            if (line == null || line.Children == null)
            {
                LogError($"Line {lineIndex} is null or has no children");
                return null;
            }

            // 检查组件索引是否有效
            if (componentIndex < 0 || componentIndex >= line.Children.Count)
            {
                LogError($"Component index out of range: {componentIndex}");
                return null;
            }

            var component = line.Children[componentIndex];

            // 如果有子组件路径，递归查找
            if (parts.Length > 2)
            {
                component = FindNestedComponent(component, parts, 2);
            }

            return component;
        }
        catch (Exception ex)
        {
            LogError($"Error in FindComponent for ID: {componentId}", ex);
            return null;
        }
    }

    /// <summary>
    /// 递归查找嵌套组件
    /// </summary>
    private ComponentSettings? FindNestedComponent(ComponentSettings? parent, string[] pathParts, int currentIndex)
    {
        if (parent == null || parent.Children == null || currentIndex >= pathParts.Length)
        {
            return null;
        }

        if (!int.TryParse(pathParts[currentIndex], out int childIndex))
        {
            LogError($"Failed to parse child index at position {currentIndex}: {pathParts[currentIndex]}");
            return null;
        }

        if (childIndex < 0 || childIndex >= parent.Children.Count)
        {
            LogError($"Child index out of range: {childIndex}");
            return null;
        }

        var child = parent.Children[childIndex];

        // 如果还有更多路径部分，继续递归
        if (currentIndex + 1 < pathParts.Length)
        {
            return FindNestedComponent(child, pathParts, currentIndex + 1);
        }

        return child;
    }

    // 旧版本的查找方法（向后兼容）
    private ComponentSettings? FindComponentLegacy(string componentId)
    {
        var profile = _componentsService.CurrentComponents;
        if (profile == null || profile.Lines == null) return null;

        foreach (var line in profile.Lines)
        {
            if (line?.Children == null) continue;
            foreach (var component in line.Children)
            {
                if (component?.Id == componentId)
                {
                    LogWarning($"Using legacy search for component ID: {componentId}, found component but this is not unique");
                    return component;
                }
            }
        }
        return null;
    }

    public void Stop()
    {
        if (!_isRunning)
        {
            LogWarning("HTTP Server is not running");
            return;
        }

        LogInfo("Stopping HTTP Server...");

        _isRunning = false;

        try
        {
            _cancellationTokenSource?.Cancel();
            LogInfo("Cancellation requested");
        }
        catch (Exception ex)
        {
            LogError("Error cancelling tasks", ex);
        }

        try
        {
            _listener.Stop();
            LogInfo("HttpListener stopped");
        }
        catch (Exception ex)
        {
            LogError("Error stopping HttpListener", ex);
        }

        try
        {
            _listener.Close();
            LogInfo("HttpListener closed");
        }
        catch (Exception ex)
        {
            LogError("Error closing HttpListener", ex);
        }

        try
        {
            _cancellationTokenSource?.Dispose();
            LogInfo("CancellationTokenSource disposed");
        }
        catch (Exception ex)
        {
            LogError("Error disposing CancellationTokenSource", ex);
        }

        LogInfo("HTTP Server stopped successfully");
    }
}
