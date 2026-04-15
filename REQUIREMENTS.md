# ProtectEye 护眼卫士 · 技术需求规格说明书

> 版本：v0.3.0 | 最后更新：2026-04-15 | 技术栈：.NET 8 / WPF / Win32 API

---

## 目录

1. [项目概述](#1-项目概述)
2. [技术栈与运行环境](#2-技术栈与运行环境)
3. [模块架构](#3-模块架构)
4. [核心功能实现规格](#4-核心功能实现规格)
   - 4.1 [应用程序单例保护](#41-应用程序单例保护)
   - 4.2 [状态机与计时器（TimerService）](#42-状态机与计时器timerservice)
   - 4.3 [闲置检测（Win32 GetLastInputInfo）](#43-闲置检测win32-getlastinputinfo)
   - 4.4 [全屏检测与免打扰（DND）](#44-全屏检测与免打扰dnd)
   - 4.5 [20-20-20 微休息逻辑](#45-20-20-20-微休息逻辑)
   - 4.6 [全局快捷键（Win32 RegisterHotKey）](#46-全局快捷键win32-registerhotkey)
   - 4.7 [开机自启（Registry）](#47-开机自启registry)
5. [窗口系统规格](#5-窗口系统规格)
   - 5.1 [主设置窗口（MainWindow）](#51-主设置窗口mainwindow)
   - 5.2 [预警浮窗（WarningWindow）](#52-预警浮窗warningwindow)
   - 5.3 [大休息界面（RestWindow）](#53-大休息界面restwindow)
   - 5.4 [微休息浮窗（MicroBreakWindow）](#54-微休息浮窗microbreakwindow)
6. [数据持久化规格](#6-数据持久化规格)
   - 6.1 [配置文件（config.json）](#61-配置文件configjson)
   - 6.2 [日志文件（logs.json）](#62-日志文件logsjson)
7. [系统托盘规格](#7-系统托盘规格)
8. [主题系统规格](#8-主题系统规格)
9. [数据统计与分析规格](#9-数据统计与分析规格)
10. [配置项完整清单](#10-配置项完整清单)

---

## 1. 项目概述

**ProtectEye 护眼卫士** 是一款运行于 Windows 系统托盘的轻量级护眼提醒工具。核心设计目标是：

- 通过周期性强制休息，降低长时间用眼对视力的损伤。
- 智能感知用户行为（闲置、全屏），避免不必要的打扰。
- 提供精细的统计与日志，辅助用户了解用眼习惯。

应用程序**无主窗口入口**，启动后驻留系统托盘运行。设置界面仅在用户主动双击托盘图标或点击菜单时弹出。

---

## 2. 技术栈与运行环境

| 项目           | 内容                                            |
| -------------- | ----------------------------------------------- |
| **目标框架**   | `.NET 8.0-windows`                              |
| **UI 框架**    | WPF（Windows Presentation Foundation）+ XAML   |
| **系统交互**   | Windows Forms（托盘 NotifyIcon）+ Win32 P/Invoke |
| **数据序列化** | `System.Text.Json`                              |
| **输出类型**   | `WinExe`（无控制台窗口）                         |
| **最低支持OS** | Windows 10（需要 WS_EX_TRANSPARENT 支持）        |
| **有效版本**   | v0.3.0                                          |

---

## 3. 模块架构

```
ProtectEye/
├── App.xaml.cs            # 应用入口，托盘管理，事件总线，主题切换
├── AppVersion.cs          # 版本常量（当前 v0.3.0）
├── MainWindow.xaml(.cs)   # 设置/首页/分析 三 Tab 窗口
│
├── Models/
│   ├── AppConfig.cs       # 配置数据模型（所有可配置字段）
│   └── PoemsData.cs       # 休息界面古诗语料（静态数据）
│
├── Services/
│   ├── TimerService.cs    # 核心状态机 + DispatcherTimer
│   ├── ConfigManager.cs   # config.json 读写，单例缓存
│   ├── LogService.cs      # logs.json 追加写，统计计算
│   ├── StartupService.cs  # 开机自启（注册表 Run 项）
│   └── Win32Api.cs        # P/Invoke 封装（闲置、全屏、热键、透明窗口）
│
├── Themes/
│   ├── ThemeDark.xaml     # 暗夜极客黑（默认）
│   ├── ThemeGray.xaml     # 冷翡高级灰
│   └── ThemeBeige.xaml    # 活力暖阳橙
│
└── Windows/
    ├── RestWindow.xaml(.cs)        # 大休息界面（全屏遮罩）
    ├── WarningWindow.xaml(.cs)     # 预警浮窗（小角落）
    ├── MicroBreakWindow.xaml(.cs)  # 20-20-20 微休息浮窗
    └── LogWindow.xaml(.cs)         # 运行日志明细窗口
```

**事件流向（App.xaml.cs 为总控）：**

```
TimerService ──Tick──────────────────► App.OnTick()
             ──StateChanged──────────► App.OnStateChanged()  ─► 弹出/关闭 Window
             ──MicroBreakTriggered──► App.OnMicroBreakTriggered() ─► 弹出 MicroBreakWindow
```

---

## 4. 核心功能实现规格

### 4.1 应用程序单例保护

**实现位置：** `App.xaml.cs → Application_Startup()`

使用 **命名全局 Mutex** 防止多实例运行：

```
Mutex 名称: "Global\\ProtectEyeApplicationMutex"
```

- 若 `createdNew == false`，则弹出 MessageBox 提示并调用 `Current.Shutdown()` 退出。
- Mutex 实例保持 `static` 引用（`_mutex`），防止 GC 提前回收导致锁失效。

---

### 4.2 状态机与计时器（TimerService）

**状态定义：**

```csharp
public enum AppState
{
    Working,   // 正常工作
    Warning,   // 预警期（即将进入休息的前 N 分钟）
    Resting,   // 休息期
    Paused     // 用户手动暂停
}
```

**状态转移图：**

```
                  IdleReset / SkipRest / RestCompleted
                       ┌─────────────────────────────┐
                       ↓                             │
   StartWorking ──► Working ──(倒计到 Warning 阈值)──► Warning
                                                       │
                                           (倒计到 0 且未全屏)
                                                       ▼
                              Paused ◄──────────── Resting
                               │ Resume()              │
                               └───────────────────────┘
```

**计时器实现：**

- 使用 `DispatcherTimer`，`Interval = 1 second`，在 UI 线程上执行 `OnTick`。
- `_secondsRemaining` 每秒递减，`_phaseStartTime` 记录当前阶段开始时间（用于配置变更时重算剩余时间）。

**每 tick 执行逻辑：**

```
1. 检查闲置时间（仅 Working / Warning 状态）:
   - 若 idleSeconds > IdleResetThresholdMinutes * 60 → 调用 StartWorking() 重置
2. _secondsRemaining--
3. 若在 Working 且开启微休息:
   - _secondsSinceLastMicroBreak++
   - 若达 20*60 且 _secondsRemaining > 120 → 触发 MicroBreakTriggered 事件
4. 若 _secondsRemaining <= 0 → HandlePhaseTransition()
5. 若 Working 且剩余 == WarningDurationMinutes*60 → 切换到 Warning 状态
```

**状态切换方法：**

| 方法               | 说明                                                            |
| ------------------ | --------------------------------------------------------------- |
| `StartWorking()`   | 设置状态为 Working，重载时长，重置 `_secondsSinceLastMicroBreak` |
| `StartResting()`   | 设置状态为 Resting，重载时长                                     |
| `Pause()`          | 停止 Timer，保存 `_stateBeforePause`，记录日志                   |
| `Resume()`         | 恢复到暂停前状态，启动 Timer，记录日志                           |
| `SkipWarning()`    | 仅还原状态为 Working，Timer 继续运行（等自然倒计至 0）           |
| `SkipRest()`       | 调用 StartWorking() 强制结束休息，记录日志                       |
| `ForceRestNow()`   | 调用 StartResting()，记录日志（附注"用户手动触发"）             |
| `ExtendRest(min)`  | 在 Resting 状态下增加 `_secondsRemaining`                       |
| `UpdateFromConfig()` | 保持 `_phaseStartTime` 不变，重算剩余时间（用于保存配置后同步）|

**HandlePhaseTransition() 逻辑：**

```
Working/Warning → 检测 DND:
    - 若 EnableDND && IsForegroundFullScreen() → SetState(Working, 5*60)  // 推迟5分钟
    - 否则 → StartResting()
Resting → StartWorking()  // 休息完成，开始新工作周期
```

---

## 4.3 闲置检测（Win32 GetLastInputInfo）

**Win32 API：**

```csharp
[DllImport("user32.dll")]
public static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);
```

**实现：**

```
idleSeconds = (Environment.TickCount - lastInputInfo.dwTime) / 1000.0
```

**触发条件：**

- 仅在 `AppState.Working` 或 `AppState.Warning` 下检测。
- `idleSeconds > IdleResetThresholdMinutes * 60` → 视为用户已自然休息 → 调用 `StartWorking()` 静默重置。

> **注意：** 此检测在每次 tick（每秒）执行，`Environment.TickCount` 存在约 49.7 天溢出，需注意长时间运行的边界情况。

---

## 4.4 全屏检测与免打扰（DND）

**Win32 API 组合：**

```csharp
GetForegroundWindow()    // 获取当前前景窗口句柄
GetDesktopWindow()       // 获取桌面窗口句柄（排除）
GetShellWindow()         // 获取 Shell 窗口句柄（排除）
GetWindowRect()          // 获取窗口矩形
MonitorFromWindow()      // 获取窗口所在显示器
GetMonitorInfo()         // 获取显示器分辨率（排除任务栏）
```

**判定全屏逻辑：**

```
前景窗口 != 桌面 && != Shell && != IntPtr.Zero
AND
wndRect.left  <= monitor.rcMonitor.left
AND wndRect.top   <= monitor.rcMonitor.top
AND wndRect.right >= monitor.rcMonitor.right
AND wndRect.bottom>= monitor.rcMonitor.bottom
```

> 关键区别：使用 `rcMonitor`（物理屏幕边界），而非 `rcWork`（工作区），确保最大化普通窗口（有任务栏）不误判为全屏。

**触发行为：** 当到达休息时间且检测到全屏，推迟 `5 分钟`（硬编码），重新进入 Working 状态并打印日志"全屏应用检测中，推迟休息 5 分钟"。

---

### 4.5 20-20-20 微休息逻辑

**触发条件（每 tick 检查）：**

```
CurrentState == Working
AND EnableMicroBreak == true
AND _secondsSinceLastMicroBreak >= 20 * 60      // 连续工作达 20 分钟
AND _secondsRemaining > 120                      // 距下一次大休息还有 >2 分钟（避免临近大休息时重复弹窗）
```

**触发动作：**
- 触发 `MicroBreakTriggered` 事件 → `App.OnMicroBreakTriggered()` 创建 `MicroBreakWindow`
- 重置 `_secondsSinceLastMicroBreak = 0`

**MicroBreakWindow 规格：**
- 尺寸：`380 × 65 px`，无标题栏，透明背景，置顶，不抢焦点（`ShowActivated = False`）
- 位置：屏幕右上角（代码中动态计算）
- 倒计时：20 秒，自动关闭；用户可点击 `✕` 提前关闭
- 动画：入场时 Opacity `0→1` + Margin 下移 `0:0:0.5` QuinticEase

---

### 4.6 全局快捷键（Win32 RegisterHotKey）

**Win32 API：**

```csharp
[DllImport("user32.dll")]
public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
[DllImport("user32.dll")]
public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
```

**实现细节：**

- `HOTKEY_ID = 9000`（固定常量）
- 注册目标：`IntPtr.Zero`（全局消息循环）
- 快捷键字符串解析（默认 `"Ctrl+Alt+R"`）：

```
按 "+" 分割，遍历各 Part:
  "CTRL"  → modifiers |= 0x0002
  "ALT"   → modifiers |= 0x0001
  "SHIFT" → modifiers |= 0x0004
  "WIN"   → modifiers |= 0x0008
  其他    → 用 System.Windows.Forms.Keys 枚举解析为 vk
```

- 消息拦截通过 `ComponentDispatcher.ThreadPreprocessMessage` 实现（WM_HOTKEY = 0x0312）
- 每次保存配置后，先 `UnregisterHotKey` 再重新 `RegisterHotKey`

---

### 4.7 开机自启（Registry）

**实现位置：** `Services/StartupService.cs`

```
注册表路径: HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Run
键名: "ProtectEye"
值: 当前 exe 完整路径
```

- 写入 → 开机自启；删除该键 → 取消自启。
- 使用 `HKCU` 不需要管理员权限。

---

## 5. 窗口系统规格

### 5.1 主设置窗口（MainWindow）

- 类型：标准 WPF Window，`SizeToContent="Height"`，固定宽度 `540 px`
- 标题：`"护眼卫士 · ProtectEye"`
- 启动位置：`CenterScreen`
- 生命周期：由 `App.xaml.cs` 管理，关闭后 `_settingsWindow = null`（可再次打开）

**三 Tab 页：**

| Tab           | 内容                                                       |
| ------------- | ---------------------------------------------------------- |
| **首页**      | 今日统计卡片（工作时长、休息时长、休息次数、跳过次数）+ 当前配置速览 + 功能状态标签（免打扰、开机启动）+ 立即休息按钮 |
| **用眼分析** | 今日统计数据卡片 + 趋势图表（Canvas 原生绘制，支持"今日占比"饼图和"本周趋势"柱状图）+ 运行记录列表（滚动）|
| **设置管理** | 专注与休息规则 + 系统增强开关 + 沉浸式背景风格 + 主题选择 + 保存配置按钮 |

---

### 5.2 预警浮窗（WarningWindow）

- 尺寸：`300 × 150 px`
- 特性：`WindowStyle="None"`, `AllowsTransparency="True"`, `Topmost="True"`, `ShowInTaskbar="False"`
- 位置：屏幕右下角（`Loaded` 事件中计算）
- 背景：深色半透明（`#E6` = 约 90% 不透明度）
- **行为模式**：
  - `SkipWarning` 按钮：调用 `TimerService.SkipWarning()`，浮窗 `Hide()`，但倒计时继续（状态回到 Working，等时间自然到 0 再进入 Resting）。
  - `RestNow` 按钮：调用 `TimerService.ForceRestNow()`，立即触发大休息。
- **复用逻辑**：`WarningWindow` 实例被复用（`_warningWindow = new WarningWindow(...)`），通过 `Hide()`/`Show()` 控制显隐，而不是每次重新创建。

---

### 5.3 大休息界面（RestWindow）

- 类型：全屏遮罩窗口，无边框，透明背景
- 特性：`Topmost="True"`, `ShowInTaskbar="False"`, `AllowsTransparency="True"`
- 生命周期：每次进入 Resting 状态创建，休息结束或跳过后 `Close()` 并 `_restWindow = null`

**背景模式（由 `BackgroundStyle` 配置项控制）：**

| 值 | 模式         | 实现方式                                         |
| -- | ------------ | ------------------------------------------------ |
| 0  | 极限护眼黑   | 纯色遮罩（`Theme.RestBg`）                       |
| 1  | 半透明黑     | 纯色遮罩（不同透明度）                            |
| 2  | 桌面毛玻璃   | 截取桌面截图 → `BgImage.Source` → `BlurEffect(Radius=60)` |

**Aurora（极光）动态效果：**
- 两个超大椭圆高斯模糊（`BlurEffect Radius=350/400`），在 `Canvas` 上做 `TranslateTransform` 动画。
- 动画时长 18-25 秒，`AutoReverse="True"`, `RepeatBehavior="Forever"`。

**入场动画：** `Opacity 0→1` + `Scale 0.95→1`，CubicEase，持续 1 秒。

**界面交互按钮：**

| 按钮         | 调用方法                          |
| ------------ | --------------------------------- |
| 跳过         | `TimerService.SkipRest()`         |
| 延长 1 分钟  | `TimerService.ExtendRest(1)`      |
| 暂停/继续    | `TimerService.Pause()` / `Resume()` |

---

### 5.4 微休息浮窗（MicroBreakWindow）

- 尺寸：`380 × 65 px`，圆角（`CornerRadius="32"`）
- 特性：`ShowActivated="False"`（不抢焦点），`Topmost="True"`
- 背景：`#E60A0A0B`（半透明深色）
- 倒计时：20 秒，通过 `DispatcherTimer` 倒计，到 0 自动 `Close()`
- 入场动画：`Opacity 0→1` + `Margin 0,-20,0,0→0,0,0,0`，QuinticEase，0.5 秒

---

## 6. 数据持久化规格

### 6.1 配置文件（config.json）

**路径：** `%APPDATA%\ProtectEye\config.json`

**读取策略：**
- 懒加载（第一次访问 `ConfigManager.Current` 时触发）
- 若文件不存在或反序列化失败 → 使用默认值并立即写入磁盘
- 序列化选项：`WriteIndented = true`（人类可读）

**写入策略：**
- 用户在设置页点击"保存配置"后调用 `ConfigManager.Save()`
- 保存成功后调用 `App.NotifyConfigChanged()` → `TimerService.UpdateFromConfig()`（运行时热更新）

**完整字段速查：** 见[第 10 节](#10-配置项完整清单)

---

### 6.2 日志文件（logs.json）

**路径：** `%APPDATA%\ProtectEye\logs.json`

**格式：** JSON 数组，每条记录为：

```json
{
  "Timestamp": "2026-04-15T10:30:00",
  "EventType": 1,
  "Note": "用户手动触发"
}
```

**日志事件类型枚举（LogEventType）：**

| 枚举值          | 含义     | 显示文本 |
| --------------- | -------- | -------- |
| `AppStarted`    | 程序启动 | 程序启动 |
| `WorkStarted`   | 开始工作 | 开始工作 |
| `WarningShown`  | 预警弹出 | 预警提示 |
| `RestStarted`   | 进入休息 | 进入休息 |
| `RestCompleted` | 休息完成 | 休息完成 |
| `RestSkipped`   | 跳过休息 | 跳过休息 |
| `PausedByUser`  | 用户暂停 | 用户暂停 |
| `ResumedByUser` | 用户继续 | 用户继续 |
| `IdleReset`     | 空闲重置 | 空闲重置 |
| `AppExited`     | 程序退出 | 程序退出 |

**写入策略：**
- 每次记录日志时，`LoadAll()` 读取全部内容 → 追加新条目 → 过期清理 → 全量写回（非追加写）
- 自动清理：删除 `Timestamp.Date < (今日 - MaxLogDays)` 的条目
- 序列化选项：`WriteIndented = false`（节省空间）

**日志内存缓存：** `LogService._cache` 保存当次运行期间写入的条目（非完整历史），仅供快速访问。

---

## 7. 系统托盘规格

**实现：** `System.Windows.Forms.NotifyIcon`（需引入 `UseWindowsForms = true`）

**图标：** 从 `pack://application:,,,/app.ico` 加载资源，失败时回退 `SystemIcons.Shield`

**鼠标事件：**
- **双击** → `ShowSettings(tabIndex: 0)` — 打开首页

**右键菜单（ContextMenuStrip）：**

```
🏠 首页              → ShowSettings(tabIndex: 0)
📊 用眼分析          → ShowSettings(tabIndex: 1)
⚙  设置管理          → ShowSettings(tabIndex: 2)
─────────────────────
☕ 立即休息           → TimerService.ForceRestNow()
⏸ 暂停 / 继续        → TimerService.Pause() 或 Resume()
─────────────────────
ℹ  关于护眼卫士 vX.X.X → MessageBox
─────────────────────
❌ 彻底退出           → Application.Shutdown()
```

**图标文本（Tooltip）动态更新：**

| 状态    | Tooltip 文本                      |
| ------- | ---------------------------------- |
| Working | `护眼卫士 · 工作中 (mm:ss)`        |
| Resting | `护眼卫士 - 正在紧急休息！`         |
| Paused  | `护眼卫士 - 已暂停`                 |

---

## 8. 主题系统规格

**实现机制：** 动态加载 ResourceDictionary，清空并替换 `Application.Current.Resources.MergedDictionaries`

**主题文件：**

| Index | 文件               | 名称          |
| ----- | ------------------ | ------------- |
| 0     | `ThemeDark.xaml`   | 暗夜极客黑（黑/青） |
| 1     | `ThemeGray.xaml`   | 冷翡高级灰（深灰/蓝） |
| 2     | `ThemeBeige.xaml`  | 活力暖阳橙（黄/橙） |

**主题 ResourceKey 规范（各主题文件必须提供以下 Key）：**

```
Theme.Bg            # 主背景色
Theme.PanelBg       # 卡片/面板背景色
Theme.ControlBg     # 输入框/下拉框背景色
Theme.Border        # 边框色
Theme.Sep           # 分隔线色
Theme.Primary       # 主色调（高亮、选中、计时文字）
Theme.PrimaryDark   # 主色暗变体（按压态、极光球）
Theme.TextBase      # 主文字色
Theme.TextMuted     # 次文字色
Theme.TextDesc      # 辅助文字色
Theme.RestBg        # 休息界面遮罩色
Theme.Aurora2       # 极光第二光球颜色
Theme.ShadowColor   # 投影颜色
```

**切换时机：**
- 应用启动时（`Application_Startup`）
- 用户保存配置后（`ConfigManager.Save()` → `App.ChangeTheme()`）

---

## 9. 数据统计与分析规格

### 今日统计（GetTodaySummary）

**实现：** `LogService.GetDaySummary(List<LogEntry>)` — 通过遍历当日日志事件，做时间区间累计计算。

**状态机语义：**

| 事件                          | 行为                                     |
| ----------------------------- | ---------------------------------------- |
| `AppStarted / WorkStarted / ResumedByUser` | 结束当前休息区间（若存在），开始新工作区间 |
| `RestStarted`                 | 结束当前工作区间，`restCount++`，开始休息区间 |
| `RestCompleted`               | 结束休息区间，开始新工作区间              |
| `RestSkipped`                 | `skipCount++`（不影响时间区间）          |
| `PausedByUser`                | 结束当前工作区间                          |

**深度专注计数（`Over1HourCount`）：** 单次连续工作时长 ≥ 1 小时的次数。

**返回值：**
```csharp
(TimeSpan TotalWork, TimeSpan TotalRest, int RestCount, int SkipCount, int Over1HourCount)
```

### 图表系统

- **绘制引擎：** WPF `Canvas`（原生，无第三方图表库）
- **今日占比图：** 饼图（工作/休息时长比例）
- **本周趋势图：** 柱状图（近 7 天每日工作/休息时长对比）
- 使用 `LogService.GetDaySummary()` 按日期批量计算

---

## 10. 配置项完整清单

**数据模型：** `Models/AppConfig.cs`

| 字段名                   | 类型       | 默认值        | 说明                                       |
| ------------------------ | ---------- | ------------- | ------------------------------------------ |
| `WorkIntervalMinutes`    | `int`      | `45`          | 工作周期（分钟）                           |
| `RestDurationMinutes`    | `int`      | `5`           | 大休息时长（分钟）                         |
| `WarningDurationMinutes` | `int`      | `2`           | 提前预警时长（分钟），从 Working 切入 Warning |
| `IdleResetThresholdMinutes` | `int`   | `5`           | 无操作超过该时长则视为自然休息并重置计时   |
| `BackgroundStyle`        | `int`      | `0`           | 休息界面背景：0=纯黑, 1=半透明, 2=毛玻璃  |
| `AppTheme`               | `int`      | `0`           | 主题：0=暗黑, 1=灰色, 2=米黄             |
| `EnableSound`            | `bool`     | `true`        | 提示音开关（预留，当前未实现）             |
| `EnableDND`              | `bool`     | `true`        | 智能免打扰（全屏检测）                     |
| `MaxLogDays`             | `int`      | `30`          | 日志保留天数                               |
| `MotivationalQuotes`     | `string[]` | 7 条古诗词   | 大休息界面随机显示的激励语料               |
| `EnableMicroBreak`       | `bool`     | `true`        | 20-20-20 微休息开关                        |
| `ShowHealthTips`         | `bool`     | `true`        | 大休息界面是否显示健康护眼贴士             |
| `ImmediateRestShortcut`  | `string`   | `"Ctrl+Alt+R"` | 全局立即休息快捷键                        |
| `HealthTips`             | `string[]` | 7 条健康贴士  | 大休息界面随机显示的医学护眼贴士           |

---

*文档由 Antigravity 根据源代码逆向分析生成 · ProtectEye v0.3.0*
