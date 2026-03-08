# HTTP 设置服务器

通过 HTTP API 远程修改 Class Island 组件设置的插件。

## 功能介绍

本插件提供了一个简单的 HTTP 服务器，允许你通过 HTTP API 请求来读取和修改 Class Island 组件的设置。

### 支持的操作

- 获取所有组件列表（包括嵌套组件）
- 获取指定组件的完整设置
- 获取组件的设置对象
- 修改组件设置
- 保存配置
- 获取组件元数据信息

### 新特性

- **唯一组件标识**：使用位置信息精确定位每个组件
- **嵌套组件支持**：支持堆叠组件等容器组件内的子组件
- **组件元数据**：获取组件类型、描述等详细信息
- **详细日志**：使用 ClassIsland 日志系统，完整的请求日志和错误追踪
- **请求超时**：30秒超时保护

## 使用方法

### 启动插件

安装插件后，HTTP 服务器会自动在端口 **9900** 上启动。

插件使用 ClassIsland 的日志系统，所有请求日志会被记录到 ClassIsland 的日志文件中，包括：
- 请求方法、路径和来源
- 请求 ID 用于追踪
- 组件处理状态
- 响应状态和处理时间
- 错误信息和完整堆栈跟踪

日志可以在 ClassIsland 的日志查看器中查看，方便调试和问题排查。

### API 端点

#### 1. 获取所有组件列表

```http
GET /api/components
```

**响应示例：**

```json
[
  {
    "id": "0-0",
    "componentTypeId": "8f3b4a9c-1d2e-4f5a-9b8c-7d6e5f4a3b2c",
    "name": "堆叠组件",
    "lineIndex": 0,
    "componentIndex": 0,
    "parentId": null,
    "path": "",
    "hasChildren": true,
    "childrenCount": 2,
    "isVisible": true,
    "componentMeta": {
      "guid": "8f3b4a9c-1d2e-4f5a-9b8c-7d6e5f4a3b2c",
      "name": "堆叠组件",
      "description": "堆叠多个组件",
      "isComponentContainer": true
    }
  },
  {
    "id": "0-0-0",
    "componentTypeId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "name": "文字组件",
    "lineIndex": 0,
    "componentIndex": 0,
    "parentId": "0-0",
    "path": "-0",
    "hasChildren": false,
    "childrenCount": 0,
    "isVisible": true,
    "componentMeta": {
      "guid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "name": "文字组件",
      "description": "显示文字",
      "isComponentContainer": false
    }
  }
]
```

**组件 ID 格式说明：**
- 顶层组件：`lineIndex-componentIndex`（如 `0-0`）
- 一级嵌套：`lineIndex-componentIndex-childIndex`（如 `0-0-1`）
- 二级嵌套：`lineIndex-componentIndex-childIndex-grandChildIndex`（如 `0-0-1-0`）

#### 2. 获取指定组件的完整设置

```http
GET /api/components/{componentId}
```

**响应示例：**

```json
{
  "id": "0-0",
  "componentTypeId": "8f3b4a9c-1d2e-4f5a-9b8c-7d6e5f4a3b2c",
  "name": "堆叠组件",
  "settings": { ... },
  "isVisible": true,
  "opacity": 1.0,
  "hideOnRule": false,
  "componentMeta": {
    "guid": "8f3b4a9c-1d2e-4f5a-9b8c-7d6e5f4a3b2c",
    "name": "堆叠组件",
    "description": "堆叠多个组件",
    "isComponentContainer": true,
    "settingsType": "StackSettings",
    "componentType": "StackComponent"
  },
  "children": [
    {
      "index": 0,
      "id": "0-0-0",
      "name": "文字组件",
      "componentTypeId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "componentMeta": {
        "guid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
        "name": "文字组件",
        "description": "显示文字",
        "isComponentContainer": false
      }
    }
  ]
}
```

#### 3. 获取组件的设置对象

```http
GET /api/components/{componentId}/settings
```

**响应示例：**

```json
{
  "format": "HH:mm:ss",
  "showSeconds": true,
  ...
}
```

#### 4. 修改组件设置

```http
POST /api/components/{componentId}/settings
Content-Type: application/json

{
  "format": "HH:mm",
  "showSeconds": false
}
```

**成功响应：**

```json
{
  "success": true,
  "updated": 2,
  "failed": 0,
  "requestId": 123
}
```

#### 5. 保存配置

修改设置后，记得调用此端点保存配置：

```http
POST /api/save
```

**成功响应：**

```json
{
  "success": true,
  "requestId": 124
}
```

### 使用示例

#### 使用 curl 获取组件列表

```bash
curl http://localhost:9900/api/components
```

#### 使用 curl 获取顶层组件设置

```bash
curl http://localhost:9900/api/components/0-0
```

#### 使用 curl 获取嵌套组件设置

```bash
# 获取堆叠组件内的第一个子组件
curl http://localhost:9900/api/components/0-0-0
```

#### 使用 curl 修改组件设置

```bash
# 修改顶层组件
curl -X POST http://localhost:9900/api/components/0-0/settings \
  -H "Content-Type: application/json" \
  -d "{\"format\": \"HH:mm\"}"

# 修改嵌套组件
curl -X POST http://localhost:9900/api/components/0-0-0/settings \
  -H "Content-Type: application/json" \
  -d "{\"text\": \"Hello World\"}"
```

#### 使用 curl 保存配置

```bash
curl -X POST http://localhost:9900/api/save
```

#### 使用 Python 获取组件列表

```python
import requests

response = requests.get('http://localhost:9900/api/components')
components = response.json()

# 遍历所有组件
for component in components:
    print(f"ID: {component['id']}, Name: {component['name']}")
    print(f"  类型: {component['componentMeta']['name']}")
    print(f"  描述: {component['componentMeta']['description']}")
    if component['hasChildren']:
        print(f"  包含 {component['childrenCount']} 个子组件")
```

#### 使用 Python 修改嵌套组件设置

```python
import requests

# 修改堆叠组件（0-0）内的文字组件（0-0-0）
component_id = '0-0-0'
settings = {'text': '新的文字内容'}

response = requests.post(
    f'http://localhost:9900/api/components/{component_id}/settings',
    json=settings
)

print(response.json())

# 保存配置
requests.post('http://localhost:9900/api/save')
```

## 字段说明

### 组件对象字段

| 字段 | 类型 | 说明 |
|------|------|------|
| id | string | 唯一组件标识符（位置格式） |
| componentTypeId | string | 组件类型 GUID |
| name | string | 组件显示名称 |
| lineIndex | int | 所在行索引 |
| componentIndex | int | 在行中的索引 |
| parentId | string | 父组件 ID（嵌套组件才有） |
| path | string | 在容器中的路径 |
| hasChildren | bool | 是否包含子组件 |
| childrenCount | int | 子组件数量 |
| isVisible | bool | 是否可见 |
| componentMeta | object | 组件元数据 |

### 组件元数据字段

| 字段 | 类型 | 说明 |
|------|------|------|
| guid | string | 组件 GUID |
| name | string | 组件类型名称 |
| description | string | 组件描述 |
| isComponentContainer | bool | 是否是容器组件 |
| settingsType | string | 设置类类型名（详情 API） |
| componentType | string | 组件类类型名（详情 API） |

## 注意事项

- **组件唯一标识**：使用位置信息（`lineIndex-componentIndex`）而不是组件类型 ID，确保每个组件都有唯一标识
- **嵌套组件**：支持访问容器组件内的子组件，ID 格式为 `父ID-子索引`
- **保存配置**：修改设置后必须调用 `POST /api/save` 保存配置，否则修改不会持久化
- **请求超时**：每个请求有 30 秒超时限制
- **日志查看**：插件使用 ClassIsland 的日志系统，日志可以在 ClassIsland 的日志查看器中查看
- **服务器地址**：默认监听 `localhost:9900`，只允许本地访问

## 故障排除

### 组件找不到

如果收到 "Component not found" 错误：
1. 确认组件 ID 格式正确（使用 `GET /api/components` 获取正确的 ID）
2. 检查组件是否还存在（可能被删除）
3. 查看 ClassIsland 日志获取详细错误信息

### 设置修改失败

如果收到设置修改失败：
1. 检查请求体是否是有效的 JSON
2. 确认属性名称和值类型正确
3. 查看响应中的错误信息
4. 查看 ClassIsland 日志获取详细错误堆栈

## 版本历史

### 1.0.0.0

- 初始版本
- 支持 GET 和 POST 方法
- 支持读取和修改组件设置
- 支持保存配置

### 更新

- ✅ **组件唯一标识**：使用位置信息替代组件类型 ID，支持精确定位
- ✅ **嵌套组件支持**：递归获取和操作容器组件内的子组件
- ✅ **组件元数据**：提供组件类型、描述等完整信息
- ✅ **详细日志**：使用 ClassIsland 日志系统，包含请求 ID 和完整错误堆栈
- ✅ **请求超时**：30秒超时保护机制
- ✅ **错误处理**：完善的异常捕获和错误响应

## 许可证

MIT License

## 作者

xiaoxixi222

## 链接

- [GitHub](https://github.com/xiaoxixi222/http_setting_server)
- [ClassIsland 官网](http://classisland.tech) 
