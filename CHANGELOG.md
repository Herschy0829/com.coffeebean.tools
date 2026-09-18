# Changelog

## [0.12.0] - 2026-09-17

### Changed
- **UniRx + UniTask 从"可选"变成"强制依赖"**：`package.json` 的 `dependencies` 里现在写死
  `com.cysharp.unitask: 2.5.11` 与 `com.neuecc.unirx: 7.1.0` —— 装了 tools 的工程必然有它们
  （tools 是绝大多数模块的依赖，等于整个框架统一了异步与响应式的地基）。

  **注意：这两个包不在任何 registry 里，UPM 自己解析不到。** 所以 core 的 registry 里
  tools 条目同时登记了 `externalDependencies`（完整 `?path=` UPM 地址 + 锁定修订）：
  「一键安装 / 装依赖」会在**同一个 UPM 请求**里先把它们加进去，再解析 tools ——
  顺序由 `ModuleDependencyResolver` 保证（第三方在前、目标模块最后）。

  - 声明的版本与 `CThirdPartyCatalog` 里锁定的版本**必须一致**，已加测试锁住
    （否则会出现"package.json 说 2.5.11、一键集成却装别的版本"这种自相矛盾）。
  - `CThirdPartyCatalog` 里 UniRx 锁的是 commit `c244f9a`（上游 tag `7.1.0` 早于给该子目录
    补 `package.json` 的提交，用 tag 装机会报 `Repository does not contain a package manifest`）——
    这个结论没变：package.json 里的 `7.1.0` 是**包版本号**，锁定地址仍走 registry / 一键集成里那个 commit。

### Notes
- 「第三方依赖」一键集成菜单与 Hub 面板**保留**：强制依赖解决"工程里有没有"，
  面板解决"来源是哪一份"（本地 `file:` / 别的 git 地址 → 换成框架锁定的 Git 集成）。
  两者不冲突：依赖已满足时面板会显示"已由本地路径提供"，点一下才替换。

### Tests
- 新增 `PackageJson_DeclaresCatalogEntriesAsHardDependencies`：package.json 必须声明这两个包，
  且版本与清单锁定的版本逐字相等。
- 工具模块 **228 → 229**；全量 EditMode **690 → 699**（另 4 条新增在 asset 模块）。

## [0.11.0] - 2026-09-17

### Added
- **第三方依赖面板进 Hub**（`Window > CoffeeBean`）：与 `Tools/CoffeeBean/第三方依赖/` 的菜单
  **并存**，两个入口共用同一套逻辑（都走 `CThirdPartyIntegration`），改哪边都一样 ——
  读写的都是同一份 `Packages/manifest.json`，不存在两份实现。

  ```
  Window > CoffeeBean → 左侧「工具」组 → Tools · 第三方依赖     ← 新增：内嵌面板
  Tools/CoffeeBean/第三方依赖/集成 UniRx（Git）                 ← 原有菜单，保留
  ```

  面板里每个依赖一块：当前来源 / 框架锁定地址 / 作用说明 + 一个勾选框，
  底部还有「刷新状态」「把状态打到 Console」。操作期间控件置灰（`IsBusy`），
  UPM 完成或取消确认框后都会让 Hub 重画，不会停在过期状态。

- `Editor/CoffeeBeanToolAttribute.cs`：tools 模块自己的那份 attribute 副本
  （Hub 按**全名**反射匹配，所以模块无需编译期引用 core —— 与 excel / purchase / build /
  asset 的副本同一套做法，这次是第 6 份）。

### Changed
- 原来私有的 `Toggle` 提升为公开的 **`CThirdPartyIntegration.ToggleWithConfirmation(package, onCompleted = null)`**：
  确认框 + 三种来源分支只有这一处实现，菜单与 Hub 面板都调它；`onCompleted` 供面板在异步完成后刷新。
- 新增只读属性 `CThirdPartyIntegration.IsBusy`。

### Tests
- tools 226 → 228：`Panel_ExposesHubInlineContract`（static 类 + 同名 attribute + 正确签名，
  三者缺一 Hub 就不会显示它）、`Panel_ReusesTheSameEntryPoints`（确认菜单与面板确实共用
  `ToggleWithConfirmation`，且第二个参数是可省略的完成回调）。
- 工具模块 **228/228**、全量 EditMode **690/690 通过**。

## [0.10.1] - 2026-09-17

### Fixed
- **菜单勾选状态的含义搞反了后果**：勾选状态原本是"工程里有没有这个包（任何来源）"，
  于是**由 `file:` 本地路径提供的 UniRx 会显示成已勾选**，
  而用户点它（本意是"换成从 Git 集成"）得到的却是"确定从工程移除？"的确认框 ——
  与菜单标题「集成 UniRx（Git）」的含义正好相反。

  现在勾选状态问的是 **"工程里的这个包是不是本框架从 Git 集成的那个地址"**：

  | 工程里的来源 | 勾选 | 点击后 |
  |---|---|---|
  | 没有 | ☐ | 直接装上（`Client.Add`，无需确认） |
  | 本框架锁定的 Git 地址 | ☑ | 确认后移除 |
  | `file:` 本地路径 / registry 版本 / 别的 git 地址 | ☐ | 确认后**替换**为框架锁定的 Git 集成 |

  这样每一个"勾"的动作都是**从 Git 集成**（或替换为 Git 集成），与菜单标题一致；
  唯一会移除的是"已经由本框架集成过"的那一项，取消勾选即对称地移除。

  由来：只有实际把自己放进用户的工程语境里才会暴露 —— 用户的工程正好把 UniRx/UniTask
  放在 `OtherAssets/SDK` 下用 `file:` 引用，第一次点这个菜单就会撞上。
  新增 `CThirdPartyIntegration.IsIntegratedFromGit`（与 `IsIntegrated` 并存：
  前者是菜单勾选，后者是"工程里到底有没有"）。

### Tests
- 新增 `IsIntegratedFromGit_TracksGitSourceNotMerePresence`：勾选状态必须与来源判定一致，
  且"在工程里 ≠ 由本框架从 Git 集成"（用真实在场的 tools 包做探针，file:/其它 git 地址都算外来）。
- 工具模块 **226/226**、全量 EditMode **683/683 通过**。

## [0.10.0] - 2026-09-17

### Added
- **第三方依赖一键集成（UniRx / UniTask）**：菜单栏里勾选即从 Git 装进工程，取消勾选即移除。
  框架自身不依赖这两个库（纯可选），但工程里几乎总会用到；手工往 `Packages/manifest.json`
  里贴 git 地址既容易写错，又容易漏掉 `?path=` 子目录（仓库根往往没有 package.json）。

  ```
  Tools/CoffeeBean/第三方依赖/集成 UniRx（Git）          ← 勾选 = 工程已集成
  Tools/CoffeeBean/第三方依赖/集成 UniTask（Git）
  Tools/CoffeeBean/第三方依赖/查看第三方依赖状态         ← 打印来源 / tag / 用途
  ```

  - **修订锁定**：UniRx `7.1.0`、UniTask `2.5.11`，地址与修订作为常量写死在包内并有测试锁死
    （不跟默认分支 —— 上游一次不兼容提交就能让所有新工程装不上）。
    - **UniRx 这里锁的是 commit 而不是 tag**（`c244f9a`），因为上游最后一个 release tag
      `7.1.0` **早于**给 `Assets/Plugins/UniRx/Scripts` 补 `package.json` 的提交（2020-04-16，
      提交信息就叫 "Add package.json"）。用 `#7.1.0` 装机时 UPM 直接报
      `Repository does not contain a package manifest` —— 这条是**实测**出来的，不是推测。
      锁定的 commit 与 tag 7.1.0 相比**只多了 package.json 和它的 `.meta`**（源码逐字节一致），
      而 `c244f9a..master` 之间只改过 README，所以它同时等价于上游最新代码。
    - 因此 `CThirdPartyPackage` 把「版本号」与「修订」拆成两个字段：`Version` 只用于展示，
      `Revision` 才是写进 `#` 后面的东西（菜单与日志里 commit 只显示前 7 位）。
  - **来源识别**（`CThirdPartySource`）：manifest 里的取值分成 未集成 / 本框架的 Git 地址 /
    其它 Git 地址 / `file:` 本地路径 / registry 版本 五类。工程已用**别的来源**提供同一个包时，
    点击先弹确认框说清"这一项将被替换成什么"，绝不静默替换；取消勾选时也先确认。
  - **不做静默魔法**：勾选 = `Client.Add("<git 地址>#<tag>")`，取消 = `Client.Remove(包名)`，
    与在 Package Manager 里手动操作完全等价。所有行为只落在 manifest 的 `dependencies` 上。
  - **一次 `Client.AddAndRemove`**：逐个发会触发多次依赖图求解与域重载（就是 core 里修过的
    那个"一键安装卡死"）。变更期间两个勾选项自动置灰，避免并发提交 UPM 请求。
  - **跨域重载不丢结果**：操作意图先写进 `SessionState`，重载后回读 manifest 校验并报告 ——
    即使完成回调随旧域一起消失，也会给出"已生效"或明确的失败结论，不会出现"点了没反应"。
  - 新增 Editor 程序集内的 `AssemblyInfo.cs`（`InternalsVisibleTo`），使意图编解码可被测试覆盖。

### Tests
- tools 测试 170 → 225（新增 55 个用例）：
  - 地址拼接与清单完整性（两个依赖的 URL 逐字符锁死、必须带 `?path=`、必须锁修订、commit 与 tag 的区分）；
  - 来源分类的每条分支与边界（大小写、首尾空白、只差一个字符的近似地址、两个库不互相误判）；
  - manifest 解析（值里同时含 `:` `?` `#` `/`；`testables` 等其它段不混入；空对象 / 缺段 / null）；
  - 只读安全路径（未知包、空包名、空操作必须**同步**返回且不占用"进行中"状态）；
  - 跨域重载意图的编解码（含 URL 原样带回、`reported` 标记、各种畸形输入被拒、有效期判定）；
  - 菜单结构（全部挂在同一个子菜单下、每个路径恰好一个 handler、每个勾选项恰好一个 validate）。
- **端到端实测**（临时探针，跑完即删）：在 dev 工程里真的把 UniRx 从 Git 装上、校验
  manifest 取值与来源判定、再移除、再校验 —— `PASS`。上面那条"tag 里没有 package.json"
  就是这么发现的（第一次实测直接报 `Repository does not contain a package manifest`）；
  也顺带发现"成功变更不一定触发域重载"，于是给遗留意图加了有效期。
- 工具模块 **225/225**、全量 EditMode **677/677 通过**。

## [0.9.0] - 2026-09-17

### Added
- **通用 Loading 框**：`CLoading`（门面）+ `UILoading`（视图）+ 预制体/材质/着色器，
  零配置即可用（预制体放在包内 `Runtime/Resources/CoffeeBean/LoadingCanvas.prefab`）。

  ```csharp
  CLoading.Show();
  CLoading.Hide();

  // 推荐：using 作用域，异常/提前 return 也会自动收起
  using (CLoading.Scope("加载中..."))
  {
      await LoadSomething();
  }
  ```

  - **引用计数**：嵌套 / 并发的 Show 会累加，只有计数归零才真正隐藏，
    避免"后一个操作先 Hide 把前一个的遮罩关掉"。
  - **线程安全**：加载常发生在后台线程，非主线程调用会经 `MainThreadDispatcher` 投递到主线程。
    启动期用 `[RuntimeInitializeOnLoadMethod]` 把调度器准备好 ——
    否则 `IsMainThread` 在调度器初始化前恒为 false，**主线程调用也会被无谓地延后一帧**。
  - **预制体来源可替换**：显式 `Prefab` → `PrefabProvider`（可接 Addressables）→ 包内 Resources。
  - 进度 / 文字为**可选绑定**：预制体没绑就空转。
    （框架不预置文字节点，避免强依赖某个字体资源；需要就自己在预制体里放一个再拖进去。）

### Fixed（相对你给的初版）
- **自动隐藏的游离定时器**：初版是 `await Task.Delay(45s); Hide();` ——
  期间再次 `Show()` 会被**上一次的定时器提前关掉**。现在改用协程，每次 Show 重置计时、Hide 取消计时。
- **切场景丢遮罩**：初版没有 `DontDestroyOnLoad`，遮罩会随场景销毁（并且单例变 fake-null）。
- **遮罩层级反了**：初版 `SetAsFirstSibling()` 把遮罩塞到**最底层**，会被其它 UI 盖住；
  现在置顶并把 Canvas `sortingOrder` 拉到 30000（压过常规 UI）。
- **资源路径不对**：初版 `Resources.Load("Loading/LoadingCanvas")` 与实际位置不符，失败还会 NRE。
  现在路径与包内布局一致，且拿不到预制体时只报一次明确错误、不崩。
- **`async void Show()`**：异常会被吞掉；已改为同步 + 协程，不再用 async void。
- **死字段**：初版 `uiLoading` 序列化字段从未被使用，已换成真正用到的
  `spinner` / `canvasGroup` / `progressFill` / `progressText`。
- **非播放模式下的淡入淡出**：淡入淡出依赖帧推进，Editor 工具 / EditMode 下没有帧，
  必须退化成立即显隐 —— 否则 Hide 之后对象永远停在"还可见"（这条是测试抓出来的）。

### Changed
- 着色器 `Custom/Loading` → **`CoffeeBean/Loading`**（无人按名字引用，已核实）：
  - 去掉从未被采样的 `_MainTex`（死属性）
  - 去掉 `#define PI`（避免与 `UnityCG.cginc` 的宏重定义打架）
  - 圆点边缘改为 `smoothstep` 抗锯齿（初版是硬边 `if`，有锯齿且产生分支）
  - 点数 / 尺寸 / 柔边 / 半径 / 速度全部参数化（初版是 `7→2`、`0.01`、`0.5` 这些魔法数字）
  - 叠加改为按 alpha 覆盖（初版 `+=` 累加会让圆点重叠处过曝发白）
  - 补上 UGUI 的 `Stencil` / `ColorMask`，使其在 `Mask` 下也能正确裁剪
  - **`_Color` / `_Speed` / `_Radius` 属性名保持不变，已调好的材质无需改动**
- 预制体：补齐缺失的两级目录 `.meta`；根节点 `m_LocalScale` 由 `(0,0,0)` 改回 `(1,1,1)`；
  给根节点加 `CanvasGroup`（淡入淡出用）；指示器 `m_RaycastTarget` 改为 0（遮挡交给 Shadow 层）。
- `package.json` 声明 `com.unity.ugui` 依赖（loading 用到 UGUI）；
  描述与 keywords 补上 loading。

### Tests
- 新增 `CLoadingTests`（27 个用例）：引用计数（嵌套 / 超量 Hide / 强制 HideAll）、
  实例复用与销毁后重建、进度与文字的截断和记忆、`Scope` 幂等与嵌套、
  预制体来源优先级（显式 > Provider > Resources）、Provider 抛异常时回退、
  缺预制体时不崩且状态正确、以及**资产完整性回归锁**：
  直接加载包内预制体，断言脚本引用与 spinner / CanvasGroup 绑定、Canvas 排序值 ——
  手工把预制体 YAML 改坏会立刻红。
- 工具模块 **170/170**、全量 EditMode **598/598 通过**。

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
