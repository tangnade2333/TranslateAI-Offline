# translateAI

[![Release](https://img.shields.io/github/v/release/tangnade2333/TranslateAI-Offline?label=release)](https://github.com/tangnade2333/TranslateAI-Offline/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/tangnade2333/TranslateAI-Offline/total)](https://github.com/tangnade2333/TranslateAI-Offline/releases)
[![Platform](https://img.shields.io/badge/platform-Windows%20x64-blue)](#下载--download)
[![Offline](https://img.shields.io/badge/inference-100%25%20local-success)](#特性--features)

本地 AI 翻译工具：选中文字按热键，鼠标旁弹出译文。全部在本机推理，不联网。
A local-first AI translator for Windows: select text, press the hotkey, get a popup translation — fully offline.

```
选中文字  →  按 Ctrl+P  →  鼠标旁弹出译文
Select text  →  press Ctrl+P  →  popup appears next to the cursor
```

## 下载 / Download

到 **[Releases](https://github.com/tangnade2333/TranslateAI-Offline/releases/latest)** 页面下载，
解压即用，**无需安装、无需 .NET 运行时**。

| 包 | 大小 | 说明 |
|---|---|---|
| `translateAI-win64-v1.0.0.zip` | ~1.05 GB | ✅ **内置模型**，解压双击就能翻译（推荐） |
| `translateAI-win64-v1.0.0-lite.zip` | ~36 MB | 不含模型，自己把 `.gguf` 放进 `models\` |

要求：Windows 10 1903+ / Windows 11，x64，内存 8 GB+。显卡可选（有 Vulkan 支持的
NVIDIA / AMD / Intel 会快 5–8 倍，没有就自动用 CPU）。

下载后建议校验 SHA256（release 页面附 `.sha256` 文件）：

```powershell
Get-FileHash .\translateAI-win64-v1.0.0.zip -Algorithm SHA256
```

## 特性 / Features

- **本地推理**：模型跑在 llama.cpp 上，断网可用，数据不出本机
- **划词翻译**：全局热键取选中文字，悬浮窗显示译文，`Esc` 或点外面关闭；译文可选中、可 `Ctrl+C` 复制
- **主窗口**：长文本翻译、语言选择、反转、多行保留换行
- **模型可换**：把任意 `.gguf` 丢进 `models\`，设置里切换即可
- **热键可改**：设置里点一下直接按组合键
- **中英双语界面**：右上角一键切换
- **托盘常驻**：关窗口不退出，托盘图标恢复

## 运行要求 / Requirements

- Windows 10 1903+ / Windows 11（.NET Framework 4.8 系统自带，无需安装）
- 一个 GGUF 模型（见下方）
- 运行时目录 `runtime\` 或 `runtime-vulkan\`（llama.cpp 官方构建）

## 快速开始 / Quick start

1. 下载 llama.cpp 的 Windows 构建，解压到 `runtime-vulkan\`：
   <https://github.com/ggml-org/llama.cpp/releases>
   （Vulkan 版同时支持 NVIDIA / AMD / Intel；没有 Vulkan 时自动退回 CPU）
2. 把 `.gguf` 模型放进 `models\`
3. 双击 `translateAI.exe`
4. 在任意程序里选中文字，按 `Ctrl+P`

## 从源码构建 / Build

```powershell
# 编译
powershell -ExecutionPolicy Bypass -File scripts\build.ps1

# 产物
src\translateAI\bin\Release\translateAI.exe
```

需要 Visual Studio 2017+ 的 MSBuild（`.NET 桌面开发` 工作负载）。

> 脚本编码：所有 `.ps1` 必须存为 **UTF-8 with BOM**。Windows PowerShell 5.1 会把无 BOM 的
> UTF-8 当 GBK 解析，中文字符串会直接变成语法错误。
> All `.ps1` files must be saved as **UTF-8 with BOM** for Windows PowerShell 5.1.

## 打包 / Package

```powershell
powershell -ExecutionPolicy Bypass -File scripts\package.ps1 -Zip
```

产出 `dist\translateAI-win64\`：exe + runtime + 空 models + 默认 config，约 90 MB（不含模型）。

> 发布到 GitHub Releases 的完整流程（打 tag、上传 asset、校验文件）见
> [docs/PUBLISHING.md](docs/PUBLISHING.md)。

## 自检 / Self-checks

```powershell
translateAI.exe --selftest         # 启动引擎 + 翻译三句，写 selftest.log
translateAI.exe --tray-check       # 关窗口是否隐藏到托盘
translateAI.exe --popup-check      # 悬浮窗尺寸/置顶
translateAI.exe --settings-check   # 设置面板能扫到模型、配置能往返
```

## 配置 / config.json

| 字段 | 说明 |
|---|---|
| `model` | 相对根目录的模型路径，留空则用 `models\` 里第一个 |
| `port` | 本地服务端口，默认 8787 |
| `ctx` | 上下文长度，默认 4096 |
| `threads` | CPU 线程数，0 = 自动 |
| `hotkey` | 划词热键，如 `Ctrl+P`、`Ctrl+Shift+T` |
| `lang` | 界面语言，`zh` 或 `en` |

## 目录结构 / Layout

```
translateAI\
├─ src\translateAI\      C# 项目（.NET Framework 4.8 / WinForms）
│  └─ resources\         app.ico（多尺寸图标）
├─ docs\                 审计与规划文档
├─ scripts\              build / package / start / verify / make-icon
├─ runtime\              运行时（CPU，需自行下载）
├─ runtime-vulkan\       运行时（Vulkan，需自行下载）
├─ models\               模型（需自行下载）
└─ config.json
```

图标由 `scripts\make-icon.ps1` 从源图生成方形多尺寸 `app.ico`（16/32/48/64/128/256），
同时用于 exe 文件图标（`<ApplicationIcon>`）和运行时窗口/托盘图标（嵌入资源）。
换图标时重跑一次该脚本即可。

`runtime\`、`models\`、`*.gguf` 都不进版本库（见 `.gitignore`）。

## 许可证 / License

待定 / TBD
