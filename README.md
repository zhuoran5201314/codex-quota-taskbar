# Codex 额度任务栏显示

一个适用于 Windows 10/11 的便携式 Codex 额度与任务状态监控工具。

无需反复打开 Codex：任务栏圆环直接表示剩余额度，任务执行时播放呼吸动画；
鼠标悬浮后展开额度周期、重置时间、当前模型和任务状态等详细信息。

![项目总结海报](poster/Codex额度任务栏显示-项目总结海报-v1.png)

## 主要功能

- **任务栏额度圆环**：圆弧长度直接表示一周额度剩余比例。
- **额度颜色分级**：
  - 75%～100%：绿色
  - 50%～75%：蓝色
  - 25%～50%：橙色
  - 0%～25%：红色
  - 无法读取：灰色
- **任务呼吸状态**：Codex 正在执行任务时，圆环实体部分播放呼吸动画。
- **悬浮信息面板**：展示剩余额度、重置时间、任务状态、模型、推理强度和同步状态。
- **后台设置**：调整圆环粗细、呼吸透明度、面板透明度和显示/隐藏延迟。
- **参数实时预览**：拖动滑块时直接在任务栏圆环或悬浮面板中预览。
- **离线缓存**：接口暂不可用时保留最近一次有效数据。
- **开机启动**：使用当前用户的注册表启动项，不需要管理员权限。
- **便携发布**：自包含 Windows x64 版本，无需单独安装 .NET。

## 数据与隐私

- 优先调用 Codex 官方 `app-server` 的 `account/rateLimits/read` 接口读取订阅额度。
- 读取额度不会调用模型，因此不会消耗 Token。
- 只读监测 `%USERPROFILE%\.codex\sessions` 中的本地会话事件，以判断任务运行状态。
- 不读取、复制或上传 `auth.json` 中的登录凭据。
- 设置与缓存仅保存在程序目录下的 `data` 文件夹。

## 系统要求

- Windows 10 22H2 或 Windows 11
- x64 系统
- 已安装并登录 Codex

## 使用方式

1. 从 Releases 下载 Windows x64 便携包。
2. 解压到任意可写目录。
3. 双击 `Codex额度仪表盘.exe`。
4. 鼠标悬浮任务栏图标查看详情。
5. 双击图标打开设置，右键打开快捷菜单。

若 Windows 将图标收入隐藏托盘，可从任务栏 `^` 菜单中将它拖到固定显示区域。

## 从源代码构建

需要 .NET 8 SDK：

```powershell
dotnet build .\CodexQuotaDashboard\CodexQuotaDashboard.csproj -c Release
dotnet publish .\CodexQuotaDashboard\CodexQuotaDashboard.csproj `
  -c Release -r win-x64 --self-contained true
```

更多信息见 [BUILD.md](BUILD.md)。

## 技术实现

- .NET 8
- WPF
- Windows Forms `NotifyIcon`
- Win32 Acrylic / Window Composition
- Codex App Server
- 本地 JSONL 会话事件监测

## 项目结构

```text
CodexQuotaDashboard/        主程序
CodexQuotaDashboard.Probe/  官方额度接口诊断工具
poster/                     项目海报及可编辑源稿
BUILD.md                    构建与数据来源说明
```

## 作者

卓尔不群

