# HTTP 设置服务器 API 测试脚本
# 使用方法：
# 1. 修改下面的 $baseUrl 和 $token 变量为你的实际值
# 2. 运行：powershell -ExecutionPolicy Bypass -File test-api.ps1

# 配置
#$baseUrl = "http://localhost:9900"
#$token = "AbCdEf1234567890"  # 替换为你的实际 Token

# 创建请求头
$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "HTTP 设置服务器 API 测试" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# 测试 1: 获取所有组件列表
Write-Host "[测试 1] 获取所有组件列表" -ForegroundColor Yellow
Write-Host "GET $baseUrl/api/components" -ForegroundColor Gray
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/components" -Method GET -Headers $headers
    Write-Host "成功！找到 $($response.Count) 个组件" -ForegroundColor Green
    foreach ($comp in $response) {
        Write-Host "  - ID: $($comp.id), 名称: $($comp.name), 类型: $($comp.componentMeta.name)" -ForegroundColor White
    }
} catch {
    Write-Host "失败！错误: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# 测试 2: 获取指定组件的完整设置
Write-Host "[测试 2] 获取指定组件的完整设置" -ForegroundColor Yellow
$componentId = "0-0"  # 顶层组件 ID
Write-Host "GET $baseUrl/api/components/$componentId" -ForegroundColor Gray
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/components/$componentId" -Method GET -Headers $headers
    Write-Host "成功！组件名称: $($response.name)" -ForegroundColor Green
    Write-Host "  组件类型: $($response.componentMeta.name)" -ForegroundColor White
    Write-Host "  是否可见: $($response.isVisible)" -ForegroundColor White
    Write-Host "  透明度: $($response.opacity)" -ForegroundColor White
} catch {
    Write-Host "失败！错误: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# 测试 3: 获取组件的设置对象
Write-Host "[测试 3] 获取组件的设置对象" -ForegroundColor Yellow
Write-Host "GET $baseUrl/api/components/$componentId/settings" -ForegroundColor Gray
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/components/$componentId/settings" -Method GET -Headers $headers
    Write-Host "成功！设置内容:" -ForegroundColor Green
    $response | ConvertTo-Json -Depth 10 | ForEach-Object { Write-Host "  $_" -ForegroundColor White }
} catch {
    Write-Host "失败！错误: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# 测试 4: 修改组件设置
Write-Host "[测试 4] 修改组件设置" -ForegroundColor Yellow
Write-Host "POST $baseUrl/api/components/$componentId/settings" -ForegroundColor Gray
$settingsData = @{
    # 根据你的组件类型修改这些属性
    # 这里只是一个示例
    "format" = "HH:mm"
    "showSeconds" = $false
} | ConvertTo-Json
Write-Host "请求数据: $settingsData" -ForegroundColor Gray
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/components/$componentId/settings" -Method POST -Headers $headers -Body $settingsData
    Write-Host "成功！更新属性: $($response.updated), 失败: $($response.failed)" -ForegroundColor Green
} catch {
    Write-Host "失败！错误: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# 测试 5: 保存配置
Write-Host "[测试 5] 保存配置" -ForegroundColor Yellow
Write-Host "POST $baseUrl/api/save" -ForegroundColor Gray
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/save" -Method POST -Headers $headers
    Write-Host "成功！配置已保存" -ForegroundColor Green
} catch {
    Write-Host "失败！错误: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# 测试 6: 测试鉴权（无效 Token）
Write-Host "[测试 6] 测试鉴权（无效 Token）" -ForegroundColor Yellow
$invalidHeaders = @{
    "Authorization" = "Bearer InvalidToken123"
    "Content-Type" = "application/json"
}
Write-Host "GET $baseUrl/api/components (使用无效 Token)" -ForegroundColor Gray
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/components" -Method GET -Headers $invalidHeaders
    Write-Host "失败！应该返回 401 错误" -ForegroundColor Red
} catch {
    if ($_.Exception.Response.StatusCode -eq 401) {
        Write-Host "成功！正确返回 401 Unauthorized" -ForegroundColor Green
    } else {
        Write-Host "失败！错误: $($_.Exception.Message)" -ForegroundColor Red
    }
}
Write-Host ""

# 测试 7: 测试鉴权（无 Token）
Write-Host "[测试 7] 测试鉴权（无 Token）" -ForegroundColor Yellow
Write-Host "GET $baseUrl/api/components (无 Token)" -ForegroundColor Gray
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/components" -Method GET
    Write-Host "失败！应该返回 401 错误" -ForegroundColor Red
} catch {
    if ($_.Exception.Response.StatusCode -eq 401) {
        Write-Host "成功！正确返回 401 Unauthorized" -ForegroundColor Green
    } else {
        Write-Host "失败！错误: $($_.Exception.Message)" -ForegroundColor Red
    }
}
Write-Host ""

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "测试完成！" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan