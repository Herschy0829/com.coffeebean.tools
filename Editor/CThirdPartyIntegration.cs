using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace CoffeeBean.EditorTools
{
    /// <summary>
    /// 把可选的第三方依赖（UniRx / UniTask）一键接进工程。
    ///
    /// **为什么放在 tools 模块**：这两个库框架自身都不依赖（纯可选），但工程里几乎总会用到，
    /// 手工往 <c>manifest.json</c> 里贴 git 地址既容易写错、又容易忘了 <c>?path=</c> 子目录。
    /// tools 是零依赖的通用工具模块，放这里不会给任何人加依赖；没用到的工程直接忽略菜单即可。
    ///
    /// 菜单（<b>勾选 = 工程里的这个包就是本框架从 Git 集成的那个地址</b>）：
    /// <code>
    /// Tools/CoffeeBean/第三方依赖/集成 UniRx（Git）
    /// Tools/CoffeeBean/第三方依赖/集成 UniTask（Git）
    /// Tools/CoffeeBean/第三方依赖/查看第三方依赖状态
    /// </code>
    ///
    /// 语义刻意对齐 UPM 本身，不做任何"魔法"：
    /// · 勾选（工程里没有 / 由别的来源提供）→ <c>Client.Add("&lt;git 地址&gt;#&lt;锁定修订&gt;")</c>，
    ///   写进 manifest 的 dependencies；
    /// · 取消（已由本框架从 Git 集成）→ <c>Client.Remove(包名)</c>，把这一项从 manifest 去掉；
    /// · 工程已由**别的来源**提供同一个包（<c>file:</c> 本地路径 / registry 版本 / 别的 git 地址）时，
    ///   菜单**不勾选**（因为"从 Git 集成"这句话还不成立），点击会先弹确认框，
    ///   把"这一项将被替换成什么"讲清楚 —— UPM 的 Add 本身就是替换语义，不做静默替换。
    ///   （这一点是实测出来的：一开始把勾选状态定义成"工程里有没有这个包"，
    ///   结果由 <c>file:</c> 提供的 UniRx 显示成已勾选，点一下反而变成"移除"。）
    ///
    /// 变更只用**一次** <c>Client.AddAndRemove</c>（理由见 core 的 ModuleInstaller）：
    /// 逐个发会触发多次依赖图求解与域重载，慢且容易把完成回调链断掉。
    /// 域重载会丢掉本域的静态状态，所以**意图**先写进 <see cref="SessionState"/>，
    /// 重载后由 <see cref="ResumeAfterReload"/> 回读 manifest 校验并报告结果 ——
    /// 不会出现"点了没反应、也不知道成没成"。
    /// </summary>
    public static class CThirdPartyIntegration
    {
        private const string LogTag = "CoffeeBean.Tools";

        /// <summary>工程 manifest（相对工程根，即 Unity 的当前工作目录）。</summary>
        public const string ManifestPath = "Packages/manifest.json";

        /// <summary>菜单根路径（供文档与测试引用）。</summary>
        public const string MenuRoot = "Tools/CoffeeBean/第三方依赖/";

        /// <summary>进行中的操作意图，跨域重载保留。</summary>
        private const string PendingKey = "CoffeeBean.Tools.ThirdParty.Pending";

        /// <summary>上面那条意图的写入时刻（ticks）。</summary>
        private const string PendingStampKey = "CoffeeBean.Tools.ThirdParty.PendingStamp";

        /// <summary>
        /// 遗留意图的有效期。超过它就静默丢弃 —— 否则一次"成功但没触发域重载"的操作，
        /// 会把记录一直留到很久以后某次无关的重载上，届时 manifest 早已被手动改过，
        /// 于是报出一条莫名其妙的"未生效"（实测：batchmode 下成功变更确实可以不触发重载）。
        /// </summary>
        internal const double PendingValidMinutes = 5;

        /// <summary>一次只允许一个 UPM 变更在飞行中（UPM 本身也不允许并发请求）。</summary>
        private static bool _busy;

        // ==================== 读工程状态 ====================

        /// <summary>
        /// 只解析 <c>dependencies</c> 段，返回 包名 → 取值。
        /// 之所以自己解析而不是用 <c>PackageInfo</c>：这里要的是 **manifest 里的原始字符串**
        /// （<c>file:...</c> / <c>https://...git?path=...#tag</c> / <c>7.1.0</c>），
        /// 用来判断来源、并和框架锁定的地址比对 —— <c>PackageInfo</c> 只给解析后的结果。
        /// </summary>
        public static Dictionary<string, string> ParseDependencies(string manifestJson)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(manifestJson)) return result;

            // [^{}]* 足够：dependencies 的值全是字符串，不会嵌套对象
            Match block = Regex.Match(manifestJson, "\"dependencies\"\\s*:\\s*\\{(?<body>[^{}]*)\\}",
                RegexOptions.Singleline);
            if (!block.Success) return result;

            foreach (Match entry in Regex.Matches(block.Groups["body"].Value, "\"(?<key>[^\"]+)\"\\s*:\\s*\"(?<value>[^\"]*)\""))
            {
                result[entry.Groups["key"].Value] = entry.Groups["value"].Value;
            }
            return result;
        }

        /// <summary>读工程 manifest 里某个包的原始取值；不存在（或读不到 manifest）返回 null。</summary>
        public static string ReadManifestEntry(string packageId)
        {
            if (string.IsNullOrEmpty(packageId)) return null;
            try
            {
                if (!File.Exists(ManifestPath)) return null;
                string value;
                return ParseDependencies(File.ReadAllText(ManifestPath)).TryGetValue(packageId, out value) ? value : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[{LogTag}] 读取 {ManifestPath} 失败：{e.Message}");
                return null;
            }
        }

        /// <summary>该包在工程里的来源。</summary>
        public static CThirdPartySource GetSource(CThirdPartyPackage package)
        {
            return CThirdPartyCatalog.Classify(package, ReadManifestEntry(package == null ? null : package.Id));
        }

        /// <summary>工程是否**已经**集成了这个包（任何来源都算）。</summary>
        public static bool IsIntegrated(string packageId)
        {
            return !string.IsNullOrEmpty(ReadManifestEntry(packageId));
        }

        /// <summary>
        /// 工程里这个包是不是**由本框架从 Git 集成的**（即 manifest 里的取值就是锁定地址）。
        /// **菜单的勾选状态用它**，而不是"工程里有没有这个包"：
        /// 菜单标题写的是「集成 UniRx（Git）」，那么"勾上"就只能意味着"这个 Git 集成生效了"。
        /// 若用"有就算勾上"，一个由 <c>file:</c> 本地路径提供的 UniRx 会显示成已勾选，
        /// 点一下反而变成"移除" —— 与用户点这个菜单的意图（换成 Git 集成）正好相反。
        /// </summary>
        public static bool IsIntegratedFromGit(CThirdPartyPackage package)
        {
            return GetSource(package) == CThirdPartySource.ManagedGit;
        }

        // ==================== 变更 ====================

        /// <summary>
        /// 集成（<paramref name="integrated"/> = true）或移除（false）。
        /// 只提交 UPM 请求，不做确认框 —— 确认交给菜单层，好让代码调用时（如批量初始化脚本）不弹窗。
        /// </summary>
        public static void SetIntegrated(CThirdPartyPackage package, bool integrated, Action<bool, string> onCompleted = null)
        {
            if (package == null)
            {
                onCompleted?.Invoke(false, "未知的第三方依赖。");
                return;
            }

            if (_busy)
            {
                const string busy = "已有一次第三方依赖变更正在进行中，请等 Unity 重新解析完成后再试。";
                Debug.LogWarning($"[{LogTag}] {busy}");
                onCompleted?.Invoke(false, busy);
                return;
            }

            CThirdPartySource source = GetSource(package);
            if (integrated && source == CThirdPartySource.ManagedGit)
            {
                string already = $"{package.DisplayName} 已从 Git 集成（{package.Url}），无需重复操作。";
                Debug.Log($"[{LogTag}] {already}");
                onCompleted?.Invoke(true, already);
                return;
            }
            if (!integrated && source == CThirdPartySource.None)
            {
                string absent = $"{package.DisplayName} 当前未集成，无需移除。";
                Debug.Log($"[{LogTag}] {absent}");
                onCompleted?.Invoke(true, absent);
                return;
            }

            string summary = integrated
                ? $"已从 Git 集成 {package.DisplayName} {package.Version}（{package.Url}）"
                : $"已从工程移除 {package.DisplayName}（{package.Id}）";

            string[] toAdd = integrated ? new[] { package.Url } : new string[0];
            string[] toRemove = integrated ? new string[0] : new[] { package.Id };

            // 意图先落盘：域重载后本域的静态字段与回调都不在了，只有 SessionState 还在
            SessionState.SetString(PendingKey, EncodePending(package.Id, integrated, summary));
            SessionState.SetString(PendingStampKey, DateTime.UtcNow.Ticks.ToString());
            _busy = true;

            AddAndRemoveRequest request;
            try
            {
                request = Client.AddAndRemove(toAdd, toRemove);
            }
            catch (Exception e)
            {
                _busy = false;
                ClearPending();
                string message = $"提交 UPM 请求失败：{e.Message}";
                Debug.LogError($"[{LogTag}] {message}");
                onCompleted?.Invoke(false, message);
                return;
            }

            Debug.Log($"[{LogTag}] {summary} —— UPM 正在解析依赖…");

            PollUntilCompleted(request, ok =>
            {
                _busy = false;
                if (!ok)
                {
                    ClearPending();
                    string message = $"第三方依赖变更失败：{(request.Error == null ? "unknown error" : request.Error.message)}\n（{summary}）";
                    Debug.LogError($"[{LogTag}] {message}");
                    onCompleted?.Invoke(false, message);
                    return;
                }

                // 成功 → 通常紧接着会重载域。标记"请求已被接受"，让重载后的校验只负责核对结果，
                // 不再重复报一次成功；同时把有效期从"提交时刻"顺延到"完成时刻"（git 克隆可能很久）。
                SessionState.SetString(PendingKey, EncodePending(package.Id, integrated, summary) + "|reported");
                SessionState.SetString(PendingStampKey, DateTime.UtcNow.Ticks.ToString());
                AssetDatabase.Refresh();
                onCompleted?.Invoke(true, summary);
            });
        }

        // ==================== 菜单 ====================

        private const string ToggleUniRxMenu = MenuRoot + "集成 UniRx（Git）";
        private const string ToggleUniTaskMenu = MenuRoot + "集成 UniTask（Git）";
        private const string StatusMenu = MenuRoot + "查看第三方依赖状态";

        [MenuItem(ToggleUniRxMenu, false, 310)]
        private static void ToggleUniRx()
        {
            Toggle(CThirdPartyCatalog.UniRx);
        }

        [MenuItem(ToggleUniRxMenu, true, 310)]
        private static bool ValidateToggleUniRx()
        {
            Menu.SetChecked(ToggleUniRxMenu, IsIntegratedFromGit(CThirdPartyCatalog.UniRx));
            return !_busy;
        }

        [MenuItem(ToggleUniTaskMenu, false, 311)]
        private static void ToggleUniTask()
        {
            Toggle(CThirdPartyCatalog.UniTask);
        }

        [MenuItem(ToggleUniTaskMenu, true, 311)]
        private static bool ValidateToggleUniTask()
        {
            Menu.SetChecked(ToggleUniTaskMenu, IsIntegratedFromGit(CThirdPartyCatalog.UniTask));
            return !_busy;
        }

        [MenuItem(StatusMenu, false, 330)]
        private static void LogStatus()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[{LogTag}] 第三方依赖状态（读取自 {ManifestPath}）：");
            for (int i = 0; i < CThirdPartyCatalog.All.Count; i++)
            {
                CThirdPartyPackage package = CThirdPartyCatalog.All[i];
                string value = ReadManifestEntry(package.Id);
                CThirdPartySource source = CThirdPartyCatalog.Classify(package, value);
                sb.AppendLine($"· {package.DisplayName}（{package.Id}）：{CThirdPartyCatalog.DescribeSource(package, value)}");

                if (source == CThirdPartySource.ManagedGit)
                {
                    string revision = CThirdPartyCatalog.GetRevision(value);
                    if (revision != package.Revision)
                    {
                        sb.AppendLine($"    注意：当前修订为 {(string.IsNullOrEmpty(revision) ? "默认分支" : revision)}，" +
                                      $"框架锁定 {package.Version}（{package.RevisionDisplay}）；取消勾选再勾选即可切到锁定修订。");
                    }
                }
                else
                {
                    sb.AppendLine($"    框架锁定：{package.Version} {package.Url}");
                }

                sb.AppendLine($"    用途：{package.Purpose}");
            }
            Debug.Log(sb.ToString());
        }

        private static void Toggle(CThirdPartyPackage package)
        {
            CThirdPartySource source = GetSource(package);
            string value = ReadManifestEntry(package.Id);

            if (source == CThirdPartySource.ManagedGit)
            {
                // 已由本框架从 Git 集成 → 取消勾选 = 从工程移除（破坏性，先确认）
                if (EditorUtility.DisplayDialog(
                        $"移除 {package.DisplayName}",
                        $"{package.DisplayName} 当前由 Git 提供：\n{package.Url}\n\n" +
                        "确定从工程移除？工程里引用它的代码会立刻编译失败（包本身不会被删除，随时可以再勾回来）。",
                        "移除", "取消"))
                {
                    SetIntegrated(package, false);
                }
                return;
            }

            if (source == CThirdPartySource.None)
            {
                // 工程里没有 → 勾上就是装进来，没什么可确认的
                SetIntegrated(package, true);
                return;
            }

            // 已由**别的来源**提供（file: 本地路径 / registry 版本 / 别的 git 地址）：
            // 勾上 = 用框架锁定的地址**替换**这一项，所以先把"会被替换成什么"讲清楚。
            if (EditorUtility.DisplayDialog(
                    $"改用 Git 集成 {package.DisplayName}",
                    $"{package.DisplayName} 当前来源：{CThirdPartyCatalog.DescribeSource(package, value)}\n\n" +
                    $"继续会把 manifest 里的这一项替换为框架锁定的地址：\n{package.Url}\n\n" +
                    "（原文件/原包不会被删除，只是不再被工程引用）",
                    "替换为 Git 集成", "取消"))
            {
                SetIntegrated(package, true);
            }
        }

        // ==================== 域重载后的收尾 ====================

        /// <summary>
        /// 一次成功的 UPM 变更通常会带来域重载；重载后回读 manifest，把结果确凿地报出来。
        /// 这也是"点了没反应"的兜底：即使完成回调随旧域一起消失，这里仍会给出结论。
        /// （实测：batchmode 下成功变更也可以**不**触发重载，所以别的路径也都要能收尾。）
        /// </summary>
        [InitializeOnLoadMethod]
        private static void ResumeAfterReload()
        {
            string pending = SessionState.GetString(PendingKey, string.Empty);
            if (string.IsNullOrEmpty(pending)) return;

            long stamp;
            long.TryParse(SessionState.GetString(PendingStampKey, "0"), out stamp);
            ClearPending();
            _busy = false;

            if (IsPendingStale(stamp, DateTime.UtcNow)) return;   // 陈年遗留：静默丢弃，免得报出莫名其妙的结果

            EditorApplication.delayCall += () => ReportPending(pending);
        }

        /// <summary>遗留意图是否已过期（时间戳缺失也算过期）。</summary>
        internal static bool IsPendingStale(long stampTicks, DateTime utcNow)
        {
            if (stampTicks <= 0) return true;
            return (utcNow - new DateTime(stampTicks, DateTimeKind.Utc)).TotalMinutes > PendingValidMinutes;
        }

        private static void ClearPending()
        {
            SessionState.EraseString(PendingKey);
            SessionState.EraseString(PendingStampKey);
        }

        private static void ReportPending(string pending)
        {
            string packageId, summary;
            bool integrated, alreadyReported;
            if (!TryDecodePending(pending, out packageId, out integrated, out summary, out alreadyReported)) return;

            CThirdPartyPackage package = CThirdPartyCatalog.Find(packageId);
            if (package == null) return;

            CThirdPartySource source = GetSource(package);
            bool tookEffect = integrated
                ? source == CThirdPartySource.ManagedGit
                : source == CThirdPartySource.None;

            if (tookEffect)
            {
                if (!alreadyReported) Debug.Log($"[{LogTag}] {summary}（已生效）");
                return;
            }

            Debug.LogError($"[{LogTag}] {summary} —— 但 manifest 里 {package.Id} 现在仍是：" +
                           $"{CThirdPartyCatalog.DescribeSource(package, ReadManifestEntry(package.Id))}。" +
                           "操作可能未生效，请检查 Console 里的 UPM 报错（常见原因：网络/Git 地址不可达、包名冲突）。");
        }

        // ==================== 内部工具 ====================

        internal static string EncodePending(string packageId, bool integrated, string summary)
        {
            // summary 里不会出现 '|'（URL 与包名都不含），用 4 段限制切分即可
            return packageId + "|" + (integrated ? "add" : "remove") + "|" + summary;
        }

        internal static bool TryDecodePending(string pending, out string packageId, out bool integrated,
            out string summary, out bool alreadyReported)
        {
            packageId = null;
            integrated = false;
            summary = null;
            alreadyReported = false;

            if (string.IsNullOrEmpty(pending)) return false;

            string[] parts = pending.Split(new[] { '|' }, 4, StringSplitOptions.None);
            if (parts.Length < 3) return false;

            packageId = parts[0];
            integrated = parts[1] == "add";
            summary = parts[2];
            alreadyReported = parts.Length >= 4 && parts[3] == "reported";
            return !string.IsNullOrEmpty(packageId);
        }

        private static void PollUntilCompleted(Request request, Action<bool> onDone)
        {
            EditorApplication.update += Poll;

            void Poll()
            {
                if (!request.IsCompleted) return;
                EditorApplication.update -= Poll;
                onDone(request.Status == StatusCode.Success);
            }
        }
    }
}
