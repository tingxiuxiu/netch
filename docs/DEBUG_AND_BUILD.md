# Netch 项目分析与 Debug / Build 操作指南

本文只写能从本仓库源码、脚本、解决方案、GitHub Actions 配置以及外部 URL/CI 状态核对出来的内容。  
当前编写环境是 **Linux**，没有 .NET SDK、MSBuild、Visual Studio，因此 **没有在本机完整跑通 Windows 编译**。文中凡是“需要在 Windows 上执行”的命令，都标明了依据文件，并在文末给出可自行核对的检查清单。

- 仓库：`tingxiuxiu/netch`（上游为 [netchx/netch](https://github.com/netchx/netch)）
- 当前 `main` 提交：`9d99eb1`（`Bump microsoft/setup-msbuild from 1.1.3 to 1.3.1 (#1005)`）
- 程序集版本：`Netch/Controllers/UpdateChecker.cs` 中 `AssemblyVersion = "1.9.7"`
- 许可证：GPLv3（`LICENSE`）

---

## 1. 这是什么项目

Netch 是一个 **Windows x64 图形界面代理客户端**（WinForms），README 自称 “A simple proxy client”。

源码里真正存在的工作模式（`Netch/Models/Modes/ModeType.cs` + `Netch/Services/ModeService.cs`）：

| 枚举 | 控制器 | 作用（README / 源码） | 关键原生依赖 |
| --- | --- | --- | --- |
| `ProcessMode` | `NFController` | Netfilter 驱动拦截进程流量 | `Redirector.bin`、`nfapi.dll`、`bin\nfdriver.sys` |
| `TunMode` | `TUNController` | WinTUN 虚拟网卡 | `tun2socks.bin`、`wintun.dll`、`RouteHelper.bin`、`aiodns.bin` |
| `ShareMode` | `PcapController` | 基于 WinPcap/Npcap 共享网络 | `pcap2socks.exe` |

README 还写了 `WebMode`。全仓库搜索只有 README 出现该词，**代码里没有 WebMode 实现**。

支持的服务器类型由 `IServerUtil` 实现类注册（`Netch/Utils/ServerHelper.cs` 反射扫描）：SOCKS、Shadowsocks、ShadowsocksR、VMess、VLESS、Trojan、SSH、WireGuard。

实际启动路径在 `Netch/Controllers/MainController.cs`：

- 无认证的 SOCKS5，或模式声明支持 SOCKS5 认证时：直接把该 SOCKS5 交给模式控制器。
- 其他协议：**固定** `new V2rayController()`，启动 `bin\v2ray-sn.exe`，再把得到的本地 SOCKS5 交给模式控制器。

各 `*Util.GetController()` **没有任何调用点**（全仓库无 `.GetController(`）。因此 `ShadowsocksController` / `ShadowsocksRController` / `TrojanController` 虽然还在工程里、会去找 `Shadowsocks.exe` / `ShadowsocksR.exe` / `Trojan.exe`，但 **当前主流程不会走到它们**。`Other/build.ps1` 也不会生成这三份 exe。

---

## 2. 仓库结构（已核对）

```
Netch.sln                 Visual Studio 2022 解决方案（Format 12.00 / VS 17）
build.ps1                 官方一键打包入口（CI 调用的就是它）
clean.ps1                 清理各项目 bin/obj 以及 Other 产物
common.props              共享 MSBuild 属性：net6.0-windows、win-x64
global.json               只设置 sdk.allowPrerelease = false，没有钉死 SDK 版本
sha256.ps1                CI 缓存 key：对目录内文件 SHA256 再哈希
Netch/                    C# WinForms 主程序（SDK 风格 csproj）
Redirector/               C++ 动态库，输出 Redirector.bin + 拷贝 nfapi.dll
RedirectorTester/         .NET Framework 4.8 控制台，用来单独测 Redirector
RouteHelper/              C++ 动态库，输出 RouteHelper.bin
Other/                    第三方/附属组件的下载与编译脚本
Storage/                  预置资源：模式文件、i18n、驱动、tun2socks.bin 等
Tests/                    MSTest 工程（net5.0，几乎是空测试）
.github/workflows/        build.yml / release.yml / stale.yml
```

解决方案里的项目依赖（`Netch.sln` 的 `ProjectDependencies`）：

- `Netch` 依赖 `Redirector`、`RouteHelper`、`RedirectorTester`
- `Tests` 依赖 `Netch`、`Redirector`、`RouteHelper`
- `RedirectorTester` 依赖 `Redirector`

注意：解决方案级依赖只影响**编译顺序**。某个配置下某项目会不会被编译，看 `Build.0` 行，见第 6 节。

---

## 3. 运行与编译的硬性条件

这些条件来自工程文件和启动代码，不是推测。

| 项 | 依据 | 实际值 |
| --- | --- | --- |
| 操作系统 | `TargetFramework`、`RuntimeIdentifiers` | Windows，`win-x64` |
| 架构 | `common.props`、`Netch.sln` 只有 `x64` | 仅 x64，没有 AnyCPU / x86 |
| .NET | `common.props`、README | `net6.0-windows`，WinForms |
| 管理员 | `Netch/App.manifest` | `requestedExecutionLevel level="requireAdministrator"` |
| 官方支持的最低系统 | `Program.CheckOS()` | `Environment.OSVersion.Version.Build < 17763` 会标 `NoSupport`（约 Windows 10 1809） |
| CLR 主版本 | `Program.CheckClr()` | 与编译目标 Major 不一致时同样标 `NoSupport` |
| C++ 工具集 | `Redirector.vcxproj` / `RouteHelper.vcxproj` | `v143`（Visual Studio 2022）、C++20、`WindowsTargetPlatformVersion=10.0` |
| 解决方案最低 VS | `Netch.sln` | Visual Studio Version 17 |

CI（`.github/workflows/build.yml`）在完整编译 `Other` 时还会安装：

- Go（`actions/setup-go@v3`，`go-version: stable`）
- MSYS2 及一长串 mingw 包（gcc、cmake、openssl、libsodium 等）
- Rust nightly（`actions-rs/toolchain@v1`）
- MSBuild（`microsoft/setup-msbuild@v1.3.1`，`vs-prerelease: true`）

**当前 `Other/` 下仍会被执行的脚本并不调用 `cargo`。** Rust 是 CI 安装了，现役 `Other/*/build.ps1` 没用到。MSYS2 的 gcc 是给 `Other/aiodns/build.ps1` 的 `CGO_ENABLED=1` 用的。

`aiodns` 构建需要：

- Go（`Other/aiodns/go.mod` 写的是 `go 1.17`）
- CGO + Windows 目标（`GOOS=windows GOARCH=amd64`，`-buildmode=c-shared`）

`v2ray-sn` 构建需要：

- Go，`CGO_ENABLED=0`
- 克隆 `https://github.com/SagerNet/v2ray-core.git` 分支/标签 `v5.0.16`
- 能访问 GitHub 和 gist.githubusercontent.com（脚本会下载补丁文件）

`wintun` 脚本调用 **`7z`** 解压，本机 PATH 里需要 7-Zip。`pcap2socks` 使用 PowerShell `Expand-Archive`。

---

## 4. 环境准备（Windows 开发机）

按 CI 和工程文件，准备这些即可覆盖官方构建路径。

1. **Windows 10/11 x64**，建议 Build ≥ 17763。
2. **Visual Studio 2022**（Community 即可），工作负载至少包含：
   - .NET 桌面开发（.NET 6.0 与 Windows Forms）
   - 使用 C++ 的桌面开发（MSVC v143、Windows 10/11 SDK）
3. 安装时勾选 **.NET 6.0 SDK**。`global.json` 没有锁定具体补丁版本，但禁止预览版 SDK。
4. 需要编 `Other` 时再装：
   - Go
   - 7-Zip（`7z` 在 PATH 中，`Other/wintun/build.ps1` 直接调用）
   - 能被 Go CGO 找到的 Windows gcc（CI 用的是 MSYS2 `mingw-w64-x86_64-gcc`）
5. PowerShell 5+ 或 PowerShell 7。根目录脚本是 `.ps1`。
6. 若本机执行策略拦截脚本：

```powershell
Get-ExecutionPolicy
# 如被 Restricted 拦住，当前用户放开即可（这是 PowerShell 本身的策略，不是本仓库脚本）
Set-ExecutionPolicy -Scope CurrentUser RemoteSigned
```

7. 以**管理员**打开 PowerShell / Visual Studio。主程序清单要求管理员；`NFController` 会向 `C:\Windows\System32\drivers\netfilter2.sys` 拷贝驱动，`TUNController` 会向 `C:\Windows\System32\wintun.dll` 拷贝文件。Issue [#1131](https://github.com/netchx/netch/issues/1131) 的日志就是权限不足时的 `UnauthorizedAccessException`。

无法在 Linux / macOS 上作为开发目标：`net6.0-windows` + WinForms + MSVC v143 原生工程。

---

## 5. 官方一键构建：`build.ps1`

这是 CI 真正执行的入口：

```yaml
# .github/workflows/build.yml 与 release.yml 相同
.\build.ps1 -Configuration Release -OutputPath release
```

### 5.1 参数（来自 `build.ps1` 头部，默认值如下）

| 参数 | 允许值 / 类型 | 默认 | 作用 |
| --- | --- | --- | --- |
| `-Configuration` | `Debug` 或 `Release` | `Release` | 传给 `dotnet publish` 和 `msbuild` |
| `-OutputPath` | 字符串 | `release` | 最终目录；若已存在会先整目录删除 |
| `-SelfContained` | `bool` | `$True` | `SelfContained` 与 `IncludeNativeLibrariesForSelfExtract` |
| `-PublishSingleFile` | `bool` | `$True` | 单文件发布 |
| `-PublishReadyToRun` | `bool` | `$False` | 同时控制 `PublishReadyToRun`、`PublishTrimmed`、`PublishReadyToRunShowWarnings` |

PowerShell 布尔参数要传 ` $true ` / ` $false `，例如：

```powershell
.\build.ps1 -Configuration Release -OutputPath release -SelfContained $true -PublishSingleFile $true
```

### 5.2 脚本实际步骤（按源码顺序）

在仓库根目录、管理员 PowerShell 中：

```powershell
cd <仓库根目录>
.\build.ps1
```

脚本会：

1. `Push-Location` 到脚本所在目录（仓库根）。
2. 若 `release`（或你指定的 `-OutputPath`）已存在，**整目录删除**后重建。
3. 创建 `release\bin\`，并从 `Storage\` 拷贝：

| 源 | 目标 |
| --- | --- |
| `Storage\i18n\` | `release\i18n\` |
| `Storage\mode\` | `release\mode\` |
| `Storage\stun.txt` | `release\bin\stun.txt` |
| `Storage\nfdriver.sys` | `release\bin\nfdriver.sys` |
| `Storage\aiodns.conf` | `release\bin\aiodns.conf` |
| 网络下载 Country.mmdb | `release\bin\GeoLite2-Country.mmdb` |
| `Storage\tun2socks.bin` | `release\bin\tun2socks.bin` |
| `Storage\README.md` | `release\bin\README.md` |

下载地址写死在脚本里：

```text
https://raw.githubusercontent.com/Loyalsoldier/geoip/release/Country.mmdb
```

2026-09-05 用 HTTP HEAD 核对过：`200`，`content-length: 7832239`。  
`Storage\` **没有** 预置 `GeoLite2-Country.mmdb`（脚本里本地拷贝那行被注释掉了）。没有网络时这一步会失败。

本环境下按同样规则拷贝 `Storage` 后得到的目录是：

```
release/
  i18n/          fa-IR, ja-JP, zh-TW
  mode/          97 个模式文件（.txt / .json）及子目录 Game、Other、TUNTAP
  bin/
    aiodns.conf
    nfdriver.sys          PE32+ native x86-64（约 89KiB）
    stun.txt              4 条 STUN 域名
    tun2socks.bin         PE32+ DLL x86-64（约 4.5MiB）
    README.md
    GeoLite2-Country.mmdb （需下载，模拟拷贝时未放入）
```

`zh-CN` 不在 `Storage\i18n` 里，而是嵌在 `Netch/Resources/zh-CN`，由 `i18N.cs` 从嵌入资源加载。`en-US` 走代码里的空表。

4. 若 **不存在** `Other\release\`，调用 `.\Other\build.ps1`；失败则 `exit $LASTEXITCODE`。  
   若该目录已存在，**整段跳过**，不会重编第三方组件。
5. 拷贝 `Other\release\*.bin`、`*.dll`、`*.exe` 到 `release\bin\`。
6. 若 **不存在** `Netch\bin\<Configuration>\`（默认即 `Netch\bin\Release`），执行：

```powershell
dotnet publish `
    -c $Configuration `
    -r 'win-x64' `
    -p:Platform='x64' `
    -p:SelfContained=$SelfContained `
    -p:PublishTrimmed=$PublishReadyToRun `
    -p:PublishSingleFile=$PublishSingleFile `
    -p:PublishReadyToRun=$PublishReadyToRun `
    -p:PublishReadyToRunShowWarnings=$PublishReadyToRun `
    -p:IncludeNativeLibrariesForSelfExtract=$SelfContained `
    -o ".\Netch\bin\$Configuration" `
    '.\Netch\Netch.csproj'
```

然后把 `Netch\bin\<Configuration>\Netch.exe` 拷到 `release\`。

7. 若 **不存在** `Redirector\bin\<Configuration>\`，则：

```powershell
msbuild -property:Configuration=$Configuration -property:Platform=x64 .\Redirector\Redirector.vcxproj
```

拷贝 `nfapi.dll`、`Redirector.bin` 到 `release\bin\`。

8. 若 **不存在** `RouteHelper\bin\<Configuration>\`，同样 `msbuild`，拷贝 `RouteHelper.bin`。
9. 仅当 `-Configuration Release` 时删除 `release\*.pdb` 和 `release\*.xml`。

### 5.3 缓存跳过（很容易踩坑）

`build.ps1` 用“目录在不在”决定是否编译，**不检查内容是否过期**：

- 有 `Other\release` → 不跑 `Other\build.ps1`
- 有 `Netch\bin\Release` → 不跑 `dotnet publish`
- 有 `Redirector\bin\Release` → 不跑 Redirector msbuild
- 有 `RouteHelper\bin\Release` → 不跑 RouteHelper msbuild

改完代码若对应 bin 目录还在，脚本会继续用旧产物。要强制全量重编，先跑第 8 节的 `clean.ps1`。

CI 用 `sha256.ps1` + `actions/cache@v3` 做同样的跳过，逻辑一致。

### 5.4 `Other\build.ps1` 会编什么

脚本对 `Other\` **第一层子目录** 查找 `build.ps1` 并执行。当前仓库第一层是：

| 目录 | 脚本做什么 | 产物（相对 `Other\release\`） |
| --- | --- | --- |
| `_Archive` | `Other/_Archive/build.ps1` 内容只有 `exit 0` | 无 |
| `aiodns` | `go build -buildmode=c-shared` | `aiodns.bin` |
| `pcap2socks` | 下载 GitHub Release zip 并解压 | `pcap2socks.exe` |
| `v2ray-sn` | clone v2ray-core `v5.0.16` + 打补丁后 `go build` | `v2ray-sn.exe` |
| `wintun` | 下载 zip，`7z x`，取出 amd64 dll | `wintun.dll` |

下载 URL（2026-09-05 HEAD 均为最终 `200`）：

- WinTUN 0.13：`https://www.wintun.net/builds/wintun-0.13.zip`（约 1.4MiB）
- pcap2socks v0.6.2：`https://github.com/zhxie/pcap2socks/releases/download/v0.6.2/pcap2socks-v0.6.2-windows-amd64.zip`（约 1.0MiB）
- v2ray-core 源码包：`https://github.com/SagerNet/v2ray-core/archive/refs/tags/v5.0.16.tar.gz`（脚本实际用 `git clone -b v5.0.16`）

`Other/_Archive/` 里还有旧的 tun2socks / v2ray-core / cloak 等脚本，**不会被当前 `Other/build.ps1` 递归执行**。现在的 `tun2socks.bin` 来自 `Storage/tun2socks.bin`，不是现编。

编完后脚本会打印 `Other\release` 下每个文件的 SHA256，并删掉各子目录的 `src\`。

单独重编附属组件：

```powershell
$env:http_proxy="socks5://127.0.0.1:10808"
$env:https_proxy="socks5://127.0.0.1:10808"
Set-ExecutionPolicy Bypass -Scope Process
.\Other\clean.ps1
.\Other\build.ps1
```

`Other\deps.ps1` 会找各子目录的 `deps.ps1`。当前只有 `Other/aiodns/deps.ps1`（`go mod init aiodns` + `go mod tidy`）。**`build.ps1` 不会自动调用 `deps.ps1`。**

### 5.5 只编某一个原生工程

需要 MSBuild 已在 PATH（VS 2022 开发人员命令提示符，或 CI 那种 `setup-msbuild`）：

```powershell
msbuild -property:Configuration=Release -property:Platform=x64 .\Redirector\Redirector.vcxproj
msbuild -property:Configuration=Release -property:Platform=x64 .\RouteHelper\RouteHelper.vcxproj
```

Redirector 的 `OutDir` 是 `$(ProjectDir)bin\$(Configuration)\`，即 `Redirector\bin\Release\`（**没有** `x64` 这一层）。  
PostBuild 会把 `Redirector\static\nfapi.dll` 拷到该输出目录。链接依赖 `Redirector\lib\nfapi.lib`。输出扩展名是 `.bin`（`TargetExt=.bin`）。

只编 C# 主程序（不打包 Storage / Other）：

```powershell
dotnet publish -c Release -r win-x64 -p:Platform=x64 -o .\Netch\bin\Release .\Netch\Netch.csproj
```

分析器：`Netch.csproj` 打开了 `EnableNETAnalyzers`、`CodeAnalysisTreatWarningsAsErrors=true`、`EnforceCodeStyleInBuild=true`。本地 `dotnet build` 若有分析器警告会当错误失败。

### 5.6 完成后的运行目录（`release\`）

与 `Program.cs` 的工作目录约定一致：exe 旁边要有 `bin\`、`mode\`、`i18n\`，运行时还会创建 `data\`、`logging\`、`mode\Custom\`。

```
release\
  Netch.exe                 主程序（默认 self-contained + 单文件）
  i18n\
  mode\
  bin\
    GeoLite2-Country.mmdb
    aiodns.bin              来自 Other
    aiodns.conf
    nfapi.dll               来自 Redirector
    nfdriver.sys
    pcap2socks.exe          来自 Other
    Redirector.bin          来自 Redirector
    RouteHelper.bin         来自 RouteHelper
    stun.txt
    tun2socks.bin           来自 Storage
    v2ray-sn.exe            来自 Other
    wintun.dll              来自 Other
    README.md
```

`Program.cs` 会把 `.\bin` 追加到进程 `PATH`，所以 `DllImport("Redirector.bin")` 等能从 `bin\` 里加载。  
`Guard` 启动子进程时，`FileName` 为 `bin\<文件名>`，工作目录为 `<NetchDir>\bin`。V2Ray 配置路径是 `run -c ..\data\last.json`，相对 `bin\` 正好落到 `data\last.json`。

**Release 配置**下 `Program.cs` 会检查 `bin` 是否存在且非空，否则弹窗 “Please extract all files then run the program!” 并以退出码 2 结束。不要只拷贝一个 `Netch.exe` 去跑。

运行：

```powershell
# 仍建议管理员
.\release\Netch.exe
```

命令行参数（`Netch/Constants.cs`）：

- `-forceUpdate`：强制显示新版本提示（`Flags.AlwaysShowNewVersionFound`）
- `-show`：单实例管道消息，用于把已有窗口拉到前台；第二实例启动失败时会发给第一实例

---

## 6. Visual Studio Debug（F5）

仓库 **没有** `launchSettings.json`，也没有 `.csproj.user`。调试配置完全由 `Netch.sln` + 各项目的 `OutputPath` 决定。

### 6.1 解决方案配置矩阵（直接摘自 `Netch.sln`）

只有两种：`Debug|x64`、`Release|x64`。

| 项目 | Debug\|x64 是否编译（有 `Build.0`） | Release\|x64 是否编译 |
| --- | --- | --- |
| Netch | 是 | 是 |
| Tests | **否**（只有 ActiveCfg） | 是 |
| Redirector | **否** | 是 |
| RouteHelper | **否** | 是 |
| RedirectorTester | **否** | 是 |

因此：在 VS 里选 **Debug|x64 然后“生成解决方案”或 F5，只会编 Netch**，不会编 Redirector / RouteHelper。  
Issue [#975](https://github.com/netchx/netch/issues/975) 的复现就是：先跑了 `build.ps1`，再 F5 Debug，启动进程代理后出现 `Unable to load DLL 'Redirector.bin'`。原因见下一小节的路径不一致。

若要在 Debug 配置下也编原生项目：VS 菜单 **生成 → 配置管理器**，给 `Redirector`、`RouteHelper` 勾选“生成”。这是对 sln 里缺失 `Build.0` 的对应操作，不是仓库里现成的脚本。

也可以不改 sln，单独右键这两个 C++ 项目 → 生成（配置选 Debug|x64）。

### 6.2 输出路径不一致（必须知道）

| 方式 | Netch 输出目录 |
| --- | --- |
| VS / `dotnet build -c Debug -p:Platform=x64` | `Netch\bin\x64\Debug\`（`Netch.csproj` 的 `OutputPath`） |
| `build.ps1` 的 `dotnet publish -o .\Netch\bin\$Configuration` | `Netch\bin\Debug` 或 `Netch\bin\Release`（**没有** `x64` 这一层） |

C++：

| 项目 | OutDir |
| --- | --- |
| Redirector | `Redirector\bin\Debug\` 或 `Redirector\bin\Release\` |
| RouteHelper | `RouteHelper\bin\Debug\` 或 `RouteHelper\bin\Release\` |

`Netch.csproj` **没有** 把 `Storage\` 或原生 dll 拷到输出目录的 `CopyToOutputDirectory` / PostBuild。  
运行时工作目录是 `Application.StartupPath`（`Global.NetchDir`），也就是 exe 所在目录。

所以 F5 时，进程看到的是 `Netch\bin\x64\Debug\`，**不会**自动使用 `release\` 或 `Netch\bin\Release\` 里的文件。

### 6.3 Debug 与 Release 在代码里的差异（`Program.cs`）

| | Debug（定义了 `DEBUG`） | 非 Debug / Release |
| --- | --- | --- |
| 空 `bin` 目录 | **不检查**，可以先把窗口拉起来 | 空则退出码 2 |
| 日志最低级别 | Verbose | Debug |
| 控制台窗口 | `AllocConsole` 后保持显示 | `ShowWindow(..., SW_HIDE)` 隐藏 |
| `Netch.csproj` OutputPath | `bin\x64\Debug\`，带 `DEBUG;TRACE` | `bin\x64\Release\`，`DebugType=none` |

主程序仍然会把 `bin` 加进 PATH，并加载 `mode\`。没有拷资源时：窗口可能出来，但模式列表空、一点“启动”就会因缺 dll/exe 失败。

### 6.4 推荐的 Debug 准备步骤

目标：让 `Netch\bin\x64\Debug\` 的布局接近第 5.6 节的 `release\`。

**做法 A — 先完整 `build.ps1`，再拷到 VS 输出目录（最省事）**

```powershell
# 仓库根，管理员
.\build.ps1 -Configuration Release -OutputPath release

$out = '.\Netch\bin\x64\Debug'
New-Item -ItemType Directory -Force -Path $out | Out-Null
Copy-Item -Recurse -Force .\release\i18n $out
Copy-Item -Recurse -Force .\release\mode $out
Copy-Item -Recurse -Force .\release\bin $out
```

然后用 VS 2022 **以管理员身份**打开 `Netch.sln`：

1. 启动项目：Netch
2. 解决方案配置：`Debug`
3. 解决方案平台：`x64`
4. F5

`build.ps1` 默认是单文件 publish，它的 `Netch.exe` 和 VS Debug 编出来的不是同一份；上面只复用 `release\bin`、`i18n`、`mode`。VS 会在 `Netch\bin\x64\Debug\` 写入带 pdb 的调试版 `Netch.exe`。

**做法 B — 不跑完整 Other，只编原生 dll + 拷 Storage**

```powershell
msbuild -property:Configuration=Debug -property:Platform=x64 .\Redirector\Redirector.vcxproj
msbuild -property:Configuration=Debug -property:Platform=x64 .\RouteHelper\RouteHelper.vcxproj

$out = '.\Netch\bin\x64\Debug'
New-Item -ItemType Directory -Force -Path "$out\bin" | Out-Null
Copy-Item -Recurse -Force .\Storage\i18n $out
Copy-Item -Recurse -Force .\Storage\mode $out
Copy-Item -Force .\Storage\stun.txt, .\Storage\nfdriver.sys, .\Storage\aiodns.conf, .\Storage\tun2socks.bin, .\Storage\README.md "$out\bin"
Invoke-WebRequest -Uri 'https://raw.githubusercontent.com/Loyalsoldier/geoip/release/Country.mmdb' -OutFile "$out\bin\GeoLite2-Country.mmdb"
Copy-Item -Force .\Redirector\bin\Debug\Redirector.bin, .\Redirector\bin\Debug\nfapi.dll "$out\bin"
Copy-Item -Force .\RouteHelper\bin\Debug\RouteHelper.bin "$out\bin"
```

进程代理（ProcessMode）调试验到这里通常够用。  
要测 VMess/VLESS/SS（主流程走 v2ray-sn）、TUN、ShareMode，还需要 `Other\release` 里的 `v2ray-sn.exe`、`wintun.dll`、`aiodns.bin`、`pcap2socks.exe`，把它们同样拷进 `$out\bin`。ShareMode 还依赖机器上已安装的 Npcap/WinPcap（代码里设备名是 `\Device\NPF_{适配器GUID}`）。

### 6.5 调试器与管理员

清单是 `requireAdministrator`。用非提权的 VS F5 时，系统会 UAC 拉起提权后的进程，**调试器经常挂不上去**。把 Visual Studio 本身用管理员启动。

原生库调试：VS 项目属性 → 调试 → 启用本机代码调试（具体文案随 VS 版本略有差别）。pdb 在 `Redirector\bin\Debug\`、`RouteHelper\bin\Debug\`。拷到 `Netch\bin\x64\Debug\bin\` 的若是 Release 的 `.bin`，则对不上 Debug 符号。

### 6.6 Debug 时看什么日志

- 控制台：Debug 下可见；Serilog 模板为 `[{Timestamp:yyyy-MM-dd HH:mm:ss}][{Level}] {Message:lj}{NewLine}{Exception}`
- 文件：`<NetchDir>\logging\application.log`（`Constants.LogFile`）
- 子进程：`<NetchDir>\logging\<控制器名>.log`（`Guard.LogPath`）
- 每次启动 `Program.cs` 会清空 `logging\` 下已有文件和子目录
- Issue 模板要求附上 `logging` 目录（`.github/ISSUE_TEMPLATE/bug_report.yml`）

### 6.7 单独调试 RedirectorTester

工程：`RedirectorTester/RedirectorTester.csproj`，.NET Framework 4.8 控制台，同样 `requireAdministrator`。

PostBuild（需要解决方案目录变量 `$(SolutionDir)`，从 `Netch.sln` 编译时才正确）：

```
COPY /Y $(SolutionDir)Redirector\bin\$(Configuration)\nfapi.dll $(TargetDir)
COPY /Y $(SolutionDir)Redirector\bin\$(Configuration)\Redirector.bin $(TargetDir)
COPY /Y $(SolutionDir)Redirector\bin\$(Configuration)\Redirector.pdb $(TargetDir)
```

`RedirectorTester.cs` 会 `aio_dial` 一组硬编码参数（目标 `127.0.0.1:1080`，进程名 Telegram / NatTypeTester），然后 `aio_init()`，等回车后 `aio_free()`。  
Debug|x64 默认不编这个项目，需单独设为启动项目并先编好 Redirector。

---

## 7. 按功能需要的文件

主流程（`MainController`）与各控制器读取的路径：

| 场景 | 必需文件（均相对 exe 目录，原生 dll 在 `bin\` 且已加入 PATH） |
| --- | --- |
| 仅打开 UI | Debug 可不需要 `bin`；Release 需要非空 `bin\`。`mode\` 为空则没有模式 |
| 进程代理 | `bin\Redirector.bin`、`bin\nfapi.dll`、`bin\nfdriver.sys` |
| TUN | `bin\tun2socks.bin`、`bin\wintun.dll`、`bin\RouteHelper.bin`；非自定义 DNS 时还要 `bin\aiodns.bin`、`bin\aiodns.conf` |
| 共享 / pcap | `bin\pcap2socks.exe`，外加系统 Npcap |
| 非“直连 SOCKS5”的服务器 | `bin\v2ray-sn.exe` |
| IP 归属地 | `bin\GeoLite2-Country.mmdb`（`Utils.GetCityCodeAsync`） |
| NAT/STUN | `bin\stun.txt` |

`Netch/Resources/7za.exe` 是嵌入资源，给更新解压用，不必手动拷到 `bin\`。

---

## 8. 清理

```powershell
.\clean.ps1
```

会删除（存在才删）：

`.vs`、`release`、`Netch\bin`、`Netch\obj`、`Tests\bin`、`Tests\obj`、`TestResults`、`Redirector\bin`、`Redirector\obj`、`RedirectorTester\bin`、`RedirectorTester\obj`、`RouteHelper\bin`、`RouteHelper\obj`，以及对应的 `*.csproj.user` / `*.vcxproj.user`，最后调用 `.\Other\clean.ps1`（删 `Other\build`、`Other\release` 和各子目录 `src`）。

`.gitignore` 还忽略 `.idea`、`packages`。各项目自己的 gitignore 忽略 `bin` / `obj`。

---

## 9. 测试

`Tests/Tests.csproj`：

- 目标框架是 **`net5.0`**（主程序是 `net6.0-windows`）
- 没有 `ProjectReference` 指向 Netch
- 只有 `Tests/Global.cs` 两个方法：打印 `BaseDirectory`，以及一段 UUID5 实验代码
- CI **没有** `dotnet test` 步骤
- sln 在 Debug 配置下不编译 Tests

本仓库没有可用的自动化测试套件可以当作构建验收。

---

## 10. GitHub Actions

| 工作流 | 触发 | 做什么 |
| --- | --- | --- |
| `Netch Build CI`（`build.yml`） | `push`、`pull_request` | `windows-2022` 上跑 `.\build.ps1 -Configuration Release -OutputPath release`，再 `actions/upload-artifact@v3` 上传 `release` |
| `Netch Release CI`（`release.yml`） | tag `*.*.*` | 同样 build，然后 `7z a -mx9 Netch.7z release` 并把内部文件夹 `release` 改名为 `Netch`，用 `softprops/action-gh-release@v1` 发布 |
| `stale.yml` | 每天 UTC 0 点 | 关闭长期无活动的 issue/PR，与编译无关 |

2026-09-05 查询上游 `netchx/netch` 的 Build CI：最近能列到的 run **全部失败**，失败发生在 **Set up job**，日志为：

```text
This request has been automatically failed because it uses a deprecated version of `actions/upload-artifact: v3`.
```

也就是说 **当前官方 CI 在检出代码之前就会被 GitHub 拒绝**，不能拿它当“构建仍是绿的”的证据。本地构建仍以 `build.ps1` 为准。若要修 CI，需要把 `actions/upload-artifact@v3` 升到受支持的主版本（这超出本文“如何按现有脚本编译”的范围）。

Release 工作流打出的资产名是 `Netch.7z`。上游 GitHub Releases 最近几条（2021–2022）也是这个文件名。

---

## 11. 常见失败（均有源码或 issue 依据）

| 现象 | 原因 | 处理 |
| --- | --- | --- |
| `Please extract all files then run the program!` | Release 下 `bin` 缺失或空（`Program.cs`） | 不要只复制 exe；用 `release\` 整目录或按第 6.4 节拷贝 |
| `Unable to load DLL 'Redirector.bin'` | exe 旁 PATH 里没有该文件。VS Debug 输出目录和 `build.ps1` 产物不在同一处 | 见第 6.2、6.4 节；上游 issue #975 |
| `bin\v2ray-sn.exe file not found!` | `Guard` 找不到文件 | 先成功跑 `Other\build.ps1`，再拷到输出 `bin\` |
| `Access to the path '...netfilter2.sys' is denied` | 没有管理员权限写系统驱动 | 管理员运行；issue #1123 / #1131 |
| `builtin driver files missing` | 没有 `bin\nfdriver.sys` | 从 `Storage\nfdriver.sys` 拷贝 |
| `build.ps1` 改了代码却还是旧 exe | 对应 `Netch\bin\Release` 等目录已存在被跳过 | `.\clean.ps1` 后再编 |
| GeoLite2 下载失败 | 脚本强制联网拉 mmdb | 保证能访问 raw.githubusercontent.com |
| `wintun` 步骤 `7z` 不是命令 | `Other/wintun/build.ps1` 直接调用 `7z` | 安装 7-Zip 并加入 PATH |
| CGO / aiodns 链接失败 | `CGO_ENABLED=1` 需要 gcc | 按 CI 安装 MSYS2 mingw gcc，或提供 Go CGO 能用的 Windows gcc |
| 分析器错误导致 `dotnet publish` 失败 | `CodeAnalysisTreatWarningsAsErrors=true` | 先看完整构建日志，不要关掉分析器凑合（除非你清楚在改工程文件） |

---

## 12. 运行时数据目录（自动创建）

`Program.cs` 会确保这些目录存在：`mode\Custom`、`data`、`i18n`、`logging`。

| 路径 | 用途 |
| --- | --- |
| `data\settings.json` | 配置（`Configuration.cs`） |
| `data\settings.json.bak` | 配置备份 |
| `data\last.json` | 最近一次核心配置（V2Ray/Trojan 等） |
| `logging\application.log` | 主日志 |
| `mode\` | 模式文件；子目录中若有名为 `disabled` 的文件则跳过该目录（`Constants.DisableModeDirectoryFileName`） |

---

## 13. 模式文件格式（Debug 时加载失败可对照）

`ModeHelper.LoadMode`：

- `.json`：反序列化为带 `type` 的 `ProcessMode` / `TunMode` / `ShareMode`
- `.txt`：首行必须以 `#` 开头，逗号分隔；第二个字段为类型号：`0` 进程，`1`/`2` TUN，`6` Share；其他数字抛 `ArgumentOutOfRangeException`（会被 `ModeService` 当成 `NotSupportedException` 之外的异常打 Warning）

`Storage/mode/Global.json` 是进程模式示例，`type` 为 `ProcessMode`。

---

## 14. 本文验证了什么、没验证什么

### 已做

- 通读 `Netch.sln`、各 `csproj`/`vcxproj`、`build.ps1`、`clean.ps1`、`Other/*.ps1`、CI YAML、启动与控制器源码
- 用 `file(1)` 确认 `Storage/nfdriver.sys`、`Storage/tun2socks.bin`、`Redirector/static/nfapi.dll` 为 Windows x64 PE
- 按 `build.ps1` 规则在 Linux 上模拟拷贝 `Storage`，确认目录树和模式/i18n 数量
- HEAD 核对脚本中的 GeoLite2 / WinTUN / pcap2socks / v2ray-core 下载 URL 返回 200
- 查询上游 Build CI：因 `upload-artifact@v3` 弃用，在 job 启动阶段失败
- 确认 `GetController()` 无引用；确认 README 的 WebMode 无对应代码

### 未做（受环境限制）

- 未执行 `dotnet publish` / `msbuild` / `go build` / 完整 `.\build.ps1`
- 未在 Visual Studio 里按 F5
- 未在真实 Windows 上安装驱动或跑代理

### 在 Windows 上如何自己验收

在满足第 3–4 节的机器上：

```powershell
# 1) 全量
.\clean.ps1
.\build.ps1 -Configuration Release -OutputPath release

# 2) 检查产物
Get-ChildItem .\release
Get-ChildItem .\release\bin
# 至少应看到 Netch.exe，以及 bin 下 Redirector.bin、RouteHelper.bin、nfapi.dll、
# v2ray-sn.exe、wintun.dll、aiodns.bin、pcap2socks.exe、tun2socks.bin、
# nfdriver.sys、GeoLite2-Country.mmdb

# 3) 管理员启动（Release 会隐藏控制台，看 logging\application.log）
.\release\Netch.exe
```

Debug 验收：完成第 6.4 节拷贝后，管理员 VS F5，确认 `Netch\bin\x64\Debug\logging\application.log` 写出，且 `mode` 下拉框非空；再按需要测一种模式。不要假设“能打开窗口”等于“代理可用”。
