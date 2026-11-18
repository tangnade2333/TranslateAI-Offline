# 发布流程 / Publishing

GitHub 没有单独的“下载页”——**仓库的 Releases 页面就是下载页**。每次发布挂一个 zip asset，
README 顶部徽章和 `releases/latest` 链接会自动指向最新一版。

## 一次性准备

```powershell
# 确认远端
git remote -v
# origin  https://github.com/tangnade2333/TranslateAI-Offline.git

# 首次推送
git add -A
git commit -m "feat: local AI translator with selection popup"
git push -u origin main
```

## 每次发版

### 1. 改版本号

`src/translateAI/Properties/AssemblyInfo.cs`：

```csharp
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
```

### 2. 打包

```powershell
# 完整版（内置模型，约 1.05 GB）
powershell -ExecutionPolicy Bypass -File scripts\package.ps1 -IncludeModel -Zip -Version 1.0.0

# 精简版（不含模型，约 36 MB）
powershell -ExecutionPolicy Bypass -File scripts\package.ps1 -Zip -Version 1.0.0
```

产出：

```
dist\translateAI-win64-v1.0.0.zip
dist\translateAI-win64-v1.0.0-lite.zip
```

### 3. 生成校验文件

```powershell
Get-FileHash dist\translateAI-win64-v1.0.0.zip -Algorithm SHA256 |
  ForEach-Object { "$($_.Hash)  translateAI-win64-v1.0.0.zip" } |
  Set-Content dist\translateAI-win64-v1.0.0.zip.sha256 -Encoding ASCII
```

### 4. 打 tag 并推送

```powershell
git tag v1.0.0
git push origin main --tags
```

### 5. 建 Release 并上传

**方式 A：gh CLI**

```powershell
winget install --id GitHub.cli -e
gh auth login

gh release create v1.0.0 `
  "dist\translateAI-win64-v1.0.0.zip" `
  "dist\translateAI-win64-v1.0.0-lite.zip" `
  "dist\translateAI-win64-v1.0.0.zip.sha256" `
  --title "translateAI v1.0.0 (Windows x64 portable)" `
  --notes-file docs\RELEASE-NOTES-v1.0.0.md
```

**方式 B：网页**

仓库 → Releases → *Draft a new release* → 选 tag `v1.0.0` → 标题、正文（粘
`docs\RELEASE-NOTES-v1.0.0.md`）→ 把两个 zip 和 `.sha256` 拖进 assets → Publish。

## 发布后检查

- README 顶部徽章显示 `release v1.0.0`（shields.io 有几分钟缓存）
- `https://github.com/tangnade2333/TranslateAI-Offline/releases/latest` 能跳到新版
- 下载 zip → 解压 → 双击 exe → 选中文字按热键，能出译文

## 注意事项

- `dist\` 已在 `.gitignore` 里，**zip 不进仓库**，只作为 release asset 上传
- 单个 asset 上限 2 GB，1.05 GB 没问题
- 模型权重有自己的许可证，随包分发前确认（Hy-MT2 是 Apache-2.0）
- 第三方组件声明见 `docs/THIRD-PARTY-NOTICES.txt`，打包脚本会自动带进包里
