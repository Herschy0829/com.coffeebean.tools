# Changelog

## [0.8.0] - 2026-09-17

### Added
- **Android Gradle 依赖自动写入**：应用内评价需要的 `com.google.android.play:review` 不再要手工改 gradle。
  - `CAndroidGradleRequirements`：**框架级必需依赖登记表**。模块只登记「需要什么」，
    「谁来写」由环境决定 —— 装了 build 模块就交给它，没装则由 tools 自己兜底。
  - `CAndroidGradleInjector`：tools 自带的幂等注入器（往 `dependencies { }` 插
    `implementation '...'`）。与 build 的 `CGradleFile` 不同，它**不抛异常**而是返回
    `false + error` —— 兜底路径不该因为写不进一个文件就中断整个打包。
  - 新增 Editor 程序集 `CoffeeBean.Tools.Editor` + `CAndroidGradleDependencyFallback`：
    实现 `IPostGenerateGradleAndroidProject`，**仅在 build 模块不在场时**注入。

- **「项目有没有用到」的判定**（`CAndroidGradleRequirements.ResolveForBuild()`），三条来源相加：
  1. **运行期痕迹**：`CAppReview.Request()` 一被调用就记录（持久化到 PlayerPrefs ——
     退出 Play 模式会重载域、静态标记会丢）。放在 `Request` 最前面，所以「编辑器里调用」
     「冷却期调用」这些不真正发起请求的情况同样算数。
  2. **源码扫描**：工程的 `Assets/**/*.cs` 里出现 `CAppReview` 标识。这条是给
     **CI / 新克隆的机器**兜底的 —— 那里从没跑过游戏，只有代码。
     扫描是启发式的（注释里提到也会命中），但误判的代价只是多一个未使用的依赖，
     比漏依赖导致真机功能静默失效小得多；可用 `EnableSourceScan = false` 关掉。
  3. **显式登记**：`CAppReview.MarkUsed()` 或 `CAndroidGradleRequirements.Add(...)`。
     调用点在被扫描范围之外的程序集（如自建包）时补一次即可。

### Changed
- `CAppReview` 的 Android 依赖告警文案更新：现在它表示「自动注入没生效」
  （自定义 Gradle 模板、或导出后手动构建），而不再是「你没手加依赖」。

## [0.7.0] - 2026-09-17

### Added
- **`CAppReview`：原生平台应用内评价（In-App Review）**
  - iOS：`UnityEngine.iOS.Device.RequestStoreReview()`（Unity 对 `SKStoreReviewController` 的封装）
  - Android：Google Play In-App Review —— JNI 调 `com.google.android.play.core.review.ReviewManagerFactory`，
    经 `com.google.android.gms.tasks.OnCompleteListener` 代理拿 `ReviewInfo` 后 `launchReviewFlow`
  - WebGL / 编辑器 / 桌面：返回 `NotSupported`（有明确日志，不静默）
  - 结果用 `CAppReviewResult` 显式区分 **`Unavailable`（平台支持但环境不满足）** 与 `Failed`：
    Android 未接入 Play Core 依赖时给出可操作告警（提示加 `com.google.android.play:review`），
    而不是假装成功
  - **冷却机制**：`CooldownDays`（默认 90 天，存 PlayerPrefs）+ `IsOnCooldown` / `LastRequestUtc` / `ResetCooldown()`。
    两家平台都有弹窗配额且**都不会告知调用方是否真的弹了**，冷却可避免频繁请求被静默丢弃
  - `OpenStorePage()` 确定性兜底：Android `market://details?id=` → `https://play.google.com/...`；
    iOS 配置 `IosAppId` 后直达 `itms-apps://...?action=write-review`

- **`CDeviceLocale`：设备地区 / 语言（原生平台）**
  - `LanguageCode`（ISO 639-1，小写）、`CountryCode`（ISO 3166-1 alpha-2，大写）、
    `LocaleIdentifier`（`zh_CN` 形式，与配置表 / `Resources` 语言目录命名对齐）、
    `LanguageNameEnglish` / `CountryNameEnglish`、`UnityLanguage`、`IsRightToLeft`
  - 取值来源：**Android** JNI 直读 `java.util.Locale.getDefault()`
    （`getLanguage` / `getCountry` / `getDisplayLanguage(Locale.ENGLISH)` 等）；
    **iOS 及其它平台**用 `CultureInfo.CurrentCulture` + `RegionInfo.CurrentRegion`——
    Unity 启动时用系统区域设置（iOS 为 `NSLocale`）初始化它，即系统原生值；
    **兜底**再用 `Application.systemLanguage` 映射
  - 结果缓存 + `Refresh()`；另提供 `CDeviceLocaleSnapshot` 一次取齐（`CDeviceLocale.Current`）
  - 注：Unity 另有 `UnityEngine.Android.AndroidLocale`，但它没有公开构造函数、拿不到实例，故未采用

### Notes
- **Android 应用内评价需要消费工程自己在 Gradle 依赖里加 `com.google.android.play:review`
  （或 `review-ktx`）**。框架不代为分发 Google 的二进制；缺依赖时 `Request` 返回
  `Unavailable` 并打印可操作告警。

### Tests
- 新增 `CAppReviewTests`（18 个用例）：冷却窗口/边界/开关、商店地址拼接（含包名为空不拼出残缺地址）、
  编辑器下降级为 `NotSupported`、不支持平台不写冷却记录（否则真机首次请求会被误判为冷却）、
  PlayerPrefs 往返、枚举数值稳定性
- 新增 `CDeviceLocaleTests`（17 个用例）：语言/地区码归一化（`zh-Hans` → `zh`、大小写、空值）、
  `SystemLanguage` → ISO 639-1 全枚举扫描防漂移、RTL 判定、快照自洽性与缓存稳定性
- 工具模块 **111/111 通过**

## [0.6.0] - 2026-09-03

### Added
- **`CGameBuild` 构建模式门面**（Beta/Release，见 docs/design-build-modes.md）：
  `IsEditor` / `IsDevelopmentBuild` / `HasDevTools`（COFFEEBEAN_DEV_TOOLS）/ `HasLogging`
  （Editor 恒 true，Beta 包 true，Release 包 false）/ `DevOnly(action)`（Release no-op）

### Changed
- **`CLog.Info/Warn` 按构建模式编译剥离**：方法体改为 `#if UNITY_EDITOR || COFFEEBEAN_LOG`——
  Editor 下（无论 Beta/Release 模式）恒有日志；Beta 包（定义 COFFEEBEAN_LOG）有日志；
  Release 包方法体为空（日志剥离）。`Error` 无条件保留。
  ⚠️ 若在 Release 语义下构建且未定义 `COFFEEBEAN_LOG`，Info/Warn 不再输出（预期行为）；
  编辑器/开发构建不受影响（UNITY_EDITOR 分支）。

## [0.5.0] - 2025-xx-xx

### Changed
- **统一命名空间**：全部类型迁移到 `CoffeeBean` 根命名空间（业务只需 `using CoffeeBean;` 即可使用所有模块主类型），模块内部辅助 / 测试 / 示例保留 `CoffeeBean.X` 子命名空间（父命名空间自动可见）
- **破坏性变更**：旧 `using CoffeeBean.X;` 需移除（类型已上移到根命名空间）

# Changelog

## [0.4.1] - 2025-xx-xx

### Added
- `MainThreadDispatcher.PostDelayedUnscaled`：基于 `Time.unscaledTime` 的延迟执行，不受 timeScale 影响
  （暂停菜单 / 慢动作等 timeScale 被修改场景下的倒计时、隐藏提示）

### Fixed
- `CSingletonMono` 多实例清理：播放模式改用 `Destroy`（帧末销毁，符合 Unity 生命周期约定），
  编辑器 / 测试环境仍用 `DestroyImmediate` 立即清理

### Changed
- 示例同步：ToolsDemo 增加 PostDelayedUnscaled 演示按钮

## [0.4.0] - 2025-xx-xx

### Added
- `CRandom`：随机工具（权重随机 / Fisher-Yates 洗牌 / 带种子控制）
- `CEnum`：枚举工具（安全解析 / 类型化取值缓存 / 数量统计）
- `CMath`：数学工具（值重映射 / 范围判断 / 进度 / 回绕 / 泛型钳制）
- `CFile`：文件工具（文本 / 字节 / JSON 磁盘读写，配合 CJson）
- 上述工具对应 EditMode 测试（按类别目录组织）

## [0.3.0] - 2025-xx-xx

### Added
- `CString`：字符串工具（数字提取 / 容错解析 / 十六进制转换 / 首字母大写）
- `CCollectionUtils`：集合工具（判空 / 安全索引 / 随机 / 交换 / 去重合并）
- `CJson`：JSON 工具（容错读写 / 覆盖反序列化 / 合法性检查）
- `CLog`：统一日志门面（[Tag] 格式 / 按级别开关）
- 上述工具对应 EditMode 测试

### Changed
- **目录结构按类别分组**：Runtime 与 Tests 拆分为 Core / Singleton / Threading / Text /
  Collections / Json / Log / Reflection / Persistence / Time / UnityObject 等子目录
  （单程序集不变，仅文件组织优化，便于查找）

## [0.2.0] - 2025-xx-xx

### Added
- `CSafeInvoke`：安全调用（异常隔离，SDK/事件回调不被打断；带参与返回值重载）
- `CReflection`：按名称解析类型（完整名 / 短名 / 简单名，结果缓存）
- `CPrefs`：PlayerPrefs 强类型封装（基础类型 / 枚举 / JSON，可带键前缀）
- `CGameObject`：GameObject 便捷工具（GetOrAddComponent / 建子物体 / 批量销毁）
- `CTime`：Unix 时间戳与时长格式化（倒计时 / 在线时长）
- 上述工具对应 EditMode 测试

## [0.1.0] - 2025-xx-xx

### Added
- `CSingleton<T>`：纯 C# 线程安全单例（Lazy 懒加载）
- `CSingletonMono<T>`：MonoBehaviour 单例（自动创建 / DontDestroyOnLoad / 多实例清理 / 退出保护 / 域重载复位）
- `MainThreadDispatcher`：主线程调度器（ConcurrentQueue 无锁入队、RunOnMainThread 立即执行优化、
  延迟执行、异常按条隔离、内部执行方法可测试）
- `ThreadUtil.RunAsync`：带并发上限的后台任务（SemaphoreSlim 限流，替代忙等）
- 与 Core 的可选集成桥（versionDefines：`COFFEEBEAN_CORE`）
- **ToolsDemo 示例**（Samples~/ToolsDemo）：单例、后台线程→主线程、延迟执行、线程池
- EditMode 测试：单例线程安全 / 队列顺序 / 延迟 / 异常隔离 / 后台线程投递

### Notes
- 命名约定：`C` 前缀 = CoffeeBean 框架自有类型
