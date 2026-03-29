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
- **API 鉴权**：通过 Token 保护 API 访问

### 新特性

- **唯一组件标识**：使用位置信息精确定位每个组件
- **嵌套组件支持**：支持堆叠组件等容器组件内的子组件
- **组件元数据**：获取组件类型、描述等详细信息
- **详细日志**：使用 ClassIsland 日志系统，完整的请求日志和错误追踪
- **请求超时**：30秒超时保护
- **API 鉴权**：默认启用 Token 鉴权，保护敏感 API 接口

## 使用方法

### 启动插件

安装插件后，HTTP 服务器会自动在端口 **9900** 上启动。

插件使用 ClassIsland 的日志系统，所有请求日志会被记录到 ClassIsland 的日志文件中，包括：
- 请求方法、路径和来源
- 请求 ID 用于追踪
- 组件处理状态
- 响应状态和处理时间
- 错误信息和完整堆栈跟踪
- **鉴权状态和 Token 验证结果**

日志可以在 ClassIsland 的日志查看器中查看，方便调试和问题排查。

### API 鉴权

插件默认启用 API 鉴权功能，所有 API 请求需要在请求头中提供有效的 Token。

**配置方式：**
- 在 ClassIsland 设置中找到"HTTP 服务器"设置页面
- 启用/禁用"启用 API 鉴权"开关（默认启用）
- 查看或生成新的 Token（16 位随机字符串）

**使用方式：**
在 HTTP 请求头中添加 `Authorization` 字段：

```http
Authorization: Bearer {your-token}
```

**获取 Token：**
- 首次启动插件时自动生成 Token
- 在设置页面可以查看当前 Token
- 点击"生成"按钮可以生成新的 Token

**禁用鉴权：**
在设置页面关闭"启用 API 鉴权"开关即可（不推荐）

### API 端点

**重要提示：** 所有 API 请求需要在请求头中提供 Token（默认启用鉴权）：

```http
Authorization: Bearer {your-token}
```

未提供或提供错误的 Token 将返回 `401 Unauthorized` 错误。

#### 1. 获取所有组件列表

```http
GET /api/components
Authorization: Bearer {your-token}
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
Authorization: Bearer {your-token}
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
Authorization: Bearer {your-token}
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
Authorization: Bearer {your-token}

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
Authorization: Bearer {your-token}
```

**成功响应：**

```json
{
  "success": true,
  "requestId": 124
}
```

### 使用示例

**注意：** 以下示例假设你的 Token 是 `AbCdEf1234567890`，请替换为你实际的 Token。

#### 使用 curl 获取组件列表

```bash
curl -H "Authorization: Bearer AbCdEf1234567890" \
  http://localhost:9900/api/components
```

#### 使用 curl 获取顶层组件设置

```bash
curl -H "Authorization: Bearer AbCdEf1234567890" \
  http://localhost:9900/api/components/0-0
```

#### 使用 curl 获取嵌套组件设置

```bash
# 获取堆叠组件内的第一个子组件
curl -H "Authorization: Bearer AbCdEf1234567890" \
  http://localhost:9900/api/components/0-0-0
```

#### 使用 curl 修改组件设置

```bash
# 修改顶层组件
curl -X POST http://localhost:9900/api/components/0-0/settings \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer AbCdEf1234567890" \
  -d "{\"format\": \"HH:mm\"}"

# 修改嵌套组件
curl -X POST http://localhost:9900/api/components/0-0-0/settings \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer AbCdEf1234567890" \
  -d "{\"text\": \"Hello World\"}"
```

#### 使用 curl 保存配置

```bash
curl -X POST http://localhost:9900/api/save \
  -H "Authorization: Bearer AbCdEf1234567890"
```

#### 使用 Python 获取组件列表

```python
import requests

headers = {
    'Authorization': 'Bearer AbCdEf1234567890'
}

response = requests.get('http://localhost:9900/api/components', headers=headers)
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

headers = {
    'Authorization': 'Bearer AbCdEf1234567890'
}

# 修改堆叠组件（0-0）内的文字组件（0-0-0）
component_id = '0-0-0'
settings = {'text': '新的文字内容'}

response = requests.post(
    f'http://localhost:9900/api/components/{component_id}/settings',
    headers=headers,
    json=settings
)

print(response.json())

# 保存配置
requests.post('http://localhost:9900/api/save', headers=headers)
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
- **API 鉴权**：默认启用 Token 鉴权，所有请求需要在 Header 中提供 `Authorization: Bearer {token}`
- **Token 安全**：请妥善保管你的 Token，不要泄露给他人。建议定期更换 Token

## 故障排除

### 401 Unauthorized

如果收到 `401 Unauthorized` 错误：
1. 检查是否提供了正确的 Token
2. 确认 Token 格式正确：`Authorization: Bearer {token}`
3. 检查鉴权是否已启用（默认启用）
4. 查看 ClassIsland 日志获取详细的 Token 验证结果

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

### 1.1.0.0

- ✅ **API 鉴权**：添加 Token 鉴权机制，默认启用
- ✅ **自动生成 Token**：首次启动自动生成 16 位随机 Token
- ✅ **Token 管理**：设置页面支持查看和生成新 Token
- ✅ **线程安全修复**：修复定时器回调的线程问题
- ✅ **配置持久化**：Token 和鉴权设置自动保存

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
