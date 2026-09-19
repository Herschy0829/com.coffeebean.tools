# com.coffeebean.tools

CoffeeBean 工具模块（**独立模块**，不依赖任何 CoffeeBean 模块，其他模块可依赖它）。

| 工具 | 用途 |
|------|------|
| `CSingleton<T>` | 纯 C# 线程安全单例（懒加载） |
| `CSingletonMono<T>` | MonoBehaviour 单例（自动创建 / 跨场景常驻 / 多实例清理 / 退出与域重载保护） |
| `MainThreadDispatcher` | 主线程调度器——任意线程投递 Action 到主线程执行（网络/广告回调） |
| `ThreadUtil` | 带并发上限的后台任务（SemaphoreSlim 限流） |
| `CSafeInvoke` | 安全调用：异常隔离，单个回调出错不中断调用链 |
| `CReflection` | 按名称解析类型（完整名 / 短名 / 简单名，带缓存） |
| `CPrefs` | PlayerPrefs 强类型封装（int/float/string/bool/枚举/JSON + 键前缀） |
| `CGameObject` | GameObject 便捷工具（GetOrAddComponent / 建子物体 / 批量销毁） |
| `CTime` | 时间戳与时长格式化（倒计时 / 在线时长显示） |
| `CString` | 字符串工具（数字提取 / 容错解析 / 十六进制 / 首字母大写） |
| `CCollectionUtils` | 集合工具（判空 / 安全索引 / 随机 / 交换 / 去重合并） |
| `CJson` | JSON 工具（容错读写 / 覆盖反序列化 / 合法性检查） |
| `CLog` | 统一日志门面（[Tag] 格式 / 按级别开关） |
| `CRandom` | 随机工具（权重随机 / 洗牌 / 带种子控制） |
| `CEnum` | 枚举工具（安全解析 / 类型化取值 / 数量统计） |
| `CMath` | 数学工具（重映射 / 范围 / 进度 / 回绕 / 泛型钳制） |
| `CFile` | 文件工具（文本 / 字节 / JSON 磁盘读写） |

> 命名约定：`C` 前缀 = CoffeeBean 框架自有类型（后续框架类型命名沿用）。

## 目录结构

```
Runtime/
├── Core/          CSafeInvoke（安全调用）
├── Singleton/     CSingleton / CSingletonMono
├── Threading/     MainThreadDispatcher / ThreadUtil
├── Text/          CString
├── Collections/   CCollectionUtils
├── Json/          CJson
├── Log/           CLog
├── Reflection/    CReflection
├── Persistence/   CPrefs
├── Time/          CTime
├── UnityObject/   CGameObject
├── Random/        CRandom
├── Enum/          CEnum
├── Math/          CMath
├── IO/            CFile
├── Platform/      CAppReview（原生应用内评价）/ CDeviceLocale（原生地区与语言）/ Android Gradle 依赖
├── Loading/       CLoading + UILoading（通用 Loading 框）
└── Bridge/        与 Core 的可选集成（安装 Core 时编译）

Editor/
└── CAndroidGradleDependencyFallback   没装 build 模块时的 Gradle 依赖兜底
```

## 安装

```json
{
  "dependencies": {
    "com.coffeebean.tools": "https://github.com/Herschy0829/com.coffeebean.tools.git#v0.13.0"
  }
}
```

## 用法

```csharp
using CoffeeBean.Tools;

// 纯 C# 单例
public sealed class GameConfig : CSingleton<GameConfig> { }

// MonoBehaviour 单例
public sealed class AudioManager : CSingletonMono<AudioManager> { }
var audio = AudioManager.Instance;

// 主线程调度（任意线程）
MainThreadDispatcher.Post(() => text.text = "主线程更新");   // 投递到主线程
MainThreadDispatcher.RunOnMainThread(() => ...);             // 已主线程则立即执行
MainThreadDispatcher.PostDelayed(() => ..., 1f);             // 延迟 1 秒

// 后台任务（并发受限）
ThreadUtil.RunAsync(() => { /* 耗时计算 */ });

// 安全调用（SDK / 事件回调）
CSafeInvoke.Invoke(onAdCallback, "广告回调");

// 类型解析（字符串配置）
Type t = CReflection.GetType("MyGame.Config.ItemConfig");

// PlayerPrefs 强类型
var prefs = new CPrefs("MyGame");
prefs.SetInt("Level", 3);
prefs.SetJson("Save", mySaveData);

// GameObject 便捷
var collider = CGameObject.GetOrAddComponent<BoxCollider>(go);
CGameObject.DestroyChildren(poolRoot, immediate: true);

// 时间格式化
string countdown = CTime.FormatClock(90);   // "01:30"
```

## 第三方依赖：UniRx + UniTask 是固定依赖，不用自己装

`package.json` 里写死了 `com.cysharp.unitask: 2.5.11` 与 `com.neuecc.unirx: 7.1.0` ——
装了 tools 的工程必然有它们（tools 是绝大多数模块的依赖，等于整个框架统一了异步与响应式的地基）。
这两个包不在任何 registry 里、UPM 自己解析不到，所以 Core 的 registry 给 tools / asset 条目登记了
`externalDependencies`（完整 `?path=` UPM 地址 + 锁定修订）："一键安装 / 装依赖"会把它们排在
**同一批 UPM 请求的最前面**，一次解析就全部满足。

- **不需要任何菜单或开关**：v0.13.0 起移除了原先的「第三方依赖一键集成」（Hub 内嵌面板 +
  `Tools/CoffeeBean/第三方依赖/*` 菜单 + `CThirdPartyIntegration` / `CThirdPartyCatalog`）。
  第三方依赖现在只有 registry 一个来源 —— 少一处"两份地址可能漂移"的维护点。
- **修订锁定**（同一个依赖只能有一个来源）：UniTask 锁 tag `2.5.11`；UniRx 锁 commit `c244f9a…`
  （上游最后的 tag `7.1.0` 早于给该子目录补 `package.json` 的提交，用 tag 装机时 UPM 会报
  `Repository does not contain a package manifest`）。Core 侧有测试钉死这两个地址：
  `BuiltInRegistry_ThirdPartyUrlsArePinnedAndConsistent`。
- 工程里由**别的来源**（`file:` 本地路径 / registry 版本 / 别的 git 地址）提供同名包也可以，
  只要**包名一致**（`com.cysharp.unitask` / `com.neuecc.unirx`）—— 各模块的 asmdef 是按
  程序集名（`UniTask` / `UniRx`）引用的，不看来源。想换来源就直接改 `Packages/manifest.json`
  里那一行，框架不会再覆盖它。

## 与 Core 集成

安装 Core 时自动注册 `MainThreadDispatcher` 进服务注册表（`Services.Get<MainThreadDispatcher>()`）；不装 Core 完全独立可用。

## License

[MIT](LICENSE.md)
