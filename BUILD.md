# Codex 额度仪表盘

## 构建

项目使用 .NET 8 WPF，目标为 Windows x64。

```powershell
dotnet build .\CodexQuotaDashboard\CodexQuotaDashboard.csproj -c Release
dotnet publish .\CodexQuotaDashboard\CodexQuotaDashboard.csproj -c Release -r win-x64 --self-contained true
```

## 数据来源

- 主路径：启动 Codex 官方 `app-server`，调用 `account/rateLimits/read`。
- 活动状态：只读监测 `%USERPROFILE%\.codex\sessions` 下的会话 JSONL 事件。
- 不读取 `auth.json`，不发起模型推理请求。

## 便携数据

运行后在程序旁创建 `data\settings.json` 与 `data\quota-cache.json`。升级时保留整个
`data` 文件夹即可。
