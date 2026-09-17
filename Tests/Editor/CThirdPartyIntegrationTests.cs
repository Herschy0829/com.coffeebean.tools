using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CoffeeBean.EditorTools;
using NUnit.Framework;
using UnityEditor;

namespace CoffeeBean.Tools.Tests
{
    /// <summary>
    /// 第三方依赖集成的测试：manifest 解析、工程状态判定、跨域重载的意图编解码、菜单结构。
    ///
    /// 刻意<b>不</b>触发真正的 UPM 变更 —— <c>Client.AddAndRemove</c> 会改工程 manifest 并引发域重载，
    /// 那既会污染工程、又会把测试进程打断。会发请求的路径只在"确认是空操作"时才被调用。
    /// </summary>
    public class CThirdPartyIntegrationTests
    {
        private const string SampleManifest = @"{
  ""testables"": [
    ""com.coffeebean.tools""
  ],
  ""dependencies"": {
    ""com.coffeebean.tools"": ""file:../../packages/com.coffeebean.tools"",
    ""com.cysharp.memorypack"": ""https://github.com/Cysharp/MemoryPack.git?path=src/MemoryPack.Unity/Assets/MemoryPack.Unity#1.21.4"",
    ""com.neuecc.unirx"": ""file:../OtherAssets/SDK/CySharp/UniRx/Scripts"",
    ""com.unity.ugui"": ""2.0.0""
  }
}";

        // ========== manifest 解析 ==========

        [Test]
        public void ParseDependencies_ReadsFlatObject()
        {
            Dictionary<string, string> deps = CThirdPartyIntegration.ParseDependencies(SampleManifest);

            Assert.AreEqual(4, deps.Count, "只应返回 dependencies 段里的条目");
            Assert.AreEqual("file:../../packages/com.coffeebean.tools", deps["com.coffeebean.tools"]);
            Assert.AreEqual("2.0.0", deps["com.unity.ugui"]);
        }

        [Test]
        public void ParseDependencies_KeepsGitUrlsIntact()
        {
            // 值里同时有 ':'、'?'、'#'、'/'，随便按冒号或井号切都会切坏
            Dictionary<string, string> deps = CThirdPartyIntegration.ParseDependencies(SampleManifest);

            Assert.AreEqual(
                "https://github.com/Cysharp/MemoryPack.git?path=src/MemoryPack.Unity/Assets/MemoryPack.Unity#1.21.4",
                deps["com.cysharp.memorypack"]);
        }

        [Test]
        public void ParseDependencies_IgnoresArraysAndOtherSections()
        {
            Dictionary<string, string> deps = CThirdPartyIntegration.ParseDependencies(SampleManifest);

            Assert.IsFalse(deps.ContainsKey("testables"), "testables 不是 dependencies，不应混进来");
            Assert.IsFalse(deps.ContainsKey("com.coffeebean.tools\""), "键名不该带引号");
        }

        [Test]
        public void ParseDependencies_EmptyObject_IsEmpty()
        {
            Assert.AreEqual(0, CThirdPartyIntegration.ParseDependencies("{\"dependencies\": {}}").Count);
        }

        [Test]
        public void ParseDependencies_WithoutSection_IsEmpty()
        {
            Assert.AreEqual(0, CThirdPartyIntegration.ParseDependencies("{\"testables\": [\"a\"]}").Count);
        }

        [Test]
        public void ParseDependencies_NullOrEmpty_IsEmpty()
        {
            Assert.AreEqual(0, CThirdPartyIntegration.ParseDependencies(null).Count);
            Assert.AreEqual(0, CThirdPartyIntegration.ParseDependencies(string.Empty).Count);
        }

        [Test]
        public void ParseDependencies_LookupIsCaseInsensitive()
        {
            Dictionary<string, string> deps = CThirdPartyIntegration.ParseDependencies(SampleManifest);

            Assert.IsTrue(deps.ContainsKey("COM.UNITY.UGUI"), "包名查找应忽略大小写（UPM 包名本身是大小写不敏感的）");
        }

        // ========== 读取真实工程 ==========

        [Test]
        public void ManifestPath_ExistsFromTestWorkingDirectory()
        {
            Assert.IsTrue(File.Exists(CThirdPartyIntegration.ManifestPath),
                $"测试的工作目录应是工程根，但找不到 {CThirdPartyIntegration.ManifestPath}（实际：{Directory.GetCurrentDirectory()}）");
        }

        [Test]
        public void ReadManifestEntry_KnownPackage_ReturnsManifestValue()
        {
            // tools 自己一定在场（测试就是从它的 testables 跑起来的）
            string value = CThirdPartyIntegration.ReadManifestEntry("com.coffeebean.tools");

            Assert.IsNotNull(value, "tools 应出现在工程的 manifest 里");
            Assert.IsNotEmpty(value);
        }

        [Test]
        public void ReadManifestEntry_UnknownPackage_ReturnsNull()
        {
            Assert.IsNull(CThirdPartyIntegration.ReadManifestEntry("com.definitely.not.installed"));
        }

        [Test]
        public void ReadManifestEntry_EmptyId_ReturnsNull()
        {
            Assert.IsNull(CThirdPartyIntegration.ReadManifestEntry(null));
            Assert.IsNull(CThirdPartyIntegration.ReadManifestEntry(string.Empty));
        }

        [Test]
        public void IsIntegrated_ReflectsManifest()
        {
            Assert.IsTrue(CThirdPartyIntegration.IsIntegrated("com.coffeebean.tools"));
            Assert.IsFalse(CThirdPartyIntegration.IsIntegrated("com.definitely.not.installed"));
            Assert.IsFalse(CThirdPartyIntegration.IsIntegrated(null));
        }

        [Test]
        public void GetSource_KnownPackage_IsNotNone()
        {
            // 用真实在场的 tools 包（UPM 地址与清单里的都不一样 → 必然不是 ManagedGit，但也不该是 None）
            var probe = new CThirdPartyPackage("com.coffeebean.tools", "Tools", "https://example.invalid/x.git", "1.0.0", "1.0.0", "探针");

            Assert.AreNotEqual(CThirdPartySource.None, CThirdPartyIntegration.GetSource(probe));
        }

        [Test]
        public void GetSource_AgreesWithManifestValue()
        {
            foreach (CThirdPartyPackage package in CThirdPartyCatalog.All)
            {
                string value = CThirdPartyIntegration.ReadManifestEntry(package.Id);
                Assert.AreEqual(CThirdPartyCatalog.Classify(package, value), CThirdPartyIntegration.GetSource(package),
                    $"{package.Id} 的两次判定应一致");
            }
        }

        [Test]
        public void GetSource_NullPackage_IsNone()
        {
            Assert.AreEqual(CThirdPartySource.None, CThirdPartyIntegration.GetSource(null));
        }

        // ========== 只读路径（不碰 UPM） ==========

        [Test]
        public void SetIntegrated_NullPackage_Fails()
        {
            bool? ok = null;
            CThirdPartyIntegration.SetIntegrated(null, true, (success, _) => ok = success);

            Assert.AreEqual(false, ok, "未知包应立刻返回失败，而不是提交一个空请求");
        }

        [Test]
        public void SetIntegrated_RemoveWhenAbsent_IsSynchronousNoOp()
        {
            CThirdPartyPackage absent = null;
            foreach (CThirdPartyPackage package in CThirdPartyCatalog.All)
            {
                if (!CThirdPartyIntegration.IsIntegrated(package.Id)) { absent = package; break; }
            }
            if (absent == null) Assert.Ignore("本工程两个第三方依赖都已集成，跳过（避免测试真的去改 manifest）");

            Assert.AreEqual(CThirdPartySource.None, CThirdPartyIntegration.GetSource(absent), "前置条件：该包不在工程里");

            bool? first = null;
            CThirdPartyIntegration.SetIntegrated(absent, false, (success, _) => first = success);
            Assert.AreEqual(true, first, "未集成时点击取消勾选应直接成功返回");

            // 同步返回还证明了没有把 _busy 留在 true 上（否则第二次会以"正在变更中"失败）
            bool? second = null;
            CThirdPartyIntegration.SetIntegrated(absent, false, (success, _) => second = success);
            Assert.AreEqual(true, second, "空操作不应占用进行中状态");
        }

        // ========== 跨域重载的意图编解码 ==========

        [Test]
        public void Pending_AddIntent_RoundTrips()
        {
            string encoded = CThirdPartyIntegration.EncodePending("com.neuecc.unirx", true, "已从 Git 集成 UniRx 7.1.0");

            Assert.IsTrue(CThirdPartyIntegration.TryDecodePending(encoded, out string id, out bool integrated,
                out string summary, out bool reported));
            Assert.AreEqual("com.neuecc.unirx", id);
            Assert.IsTrue(integrated);
            Assert.AreEqual("已从 Git 集成 UniRx 7.1.0", summary);
            Assert.IsFalse(reported);
        }

        [Test]
        public void Pending_RemoveIntent_RoundTrips()
        {
            string encoded = CThirdPartyIntegration.EncodePending("com.cysharp.unitask", false, "已移除");

            Assert.IsTrue(CThirdPartyIntegration.TryDecodePending(encoded, out string id, out bool integrated,
                out string summary, out _));
            Assert.AreEqual("com.cysharp.unitask", id);
            Assert.IsFalse(integrated);
            Assert.AreEqual("已移除", summary);
        }

        [Test]
        public void Pending_SummaryMayContainUrlsWithPipesUnlikely()
        {
            // 摘要里最"脏"的可能就是完整 URL（含 ? : / #），必须原样带回来
            string summary = "已从 Git 集成 UniTask 2.5.11（" + CThirdPartyCatalog.UniTask.Url + "）";
            string encoded = CThirdPartyIntegration.EncodePending("com.cysharp.unitask", true, summary);

            Assert.IsTrue(CThirdPartyIntegration.TryDecodePending(encoded, out _, out _, out string decoded, out _));
            Assert.AreEqual(summary, decoded);
        }

        [Test]
        public void Pending_ReportedFlag_IsDetected()
        {
            string encoded = CThirdPartyIntegration.EncodePending("com.neuecc.unirx", true, "x") + "|reported";

            Assert.IsTrue(CThirdPartyIntegration.TryDecodePending(encoded, out _, out _, out _, out bool reported));
            Assert.IsTrue(reported, "带 reported 标记时应只做校验、不重复报成功");
        }

        [Test]
        public void Pending_MalformedInput_IsRejected()
        {
            Assert.IsFalse(CThirdPartyIntegration.TryDecodePending(null, out _, out _, out _, out _));
            Assert.IsFalse(CThirdPartyIntegration.TryDecodePending(string.Empty, out _, out _, out _, out _));
            Assert.IsFalse(CThirdPartyIntegration.TryDecodePending("只有一段", out _, out _, out _, out _));
            Assert.IsFalse(CThirdPartyIntegration.TryDecodePending("id|add", out _, out _, out _, out _));
            Assert.IsFalse(CThirdPartyIntegration.TryDecodePending("|add|摘要", out _, out _, out _, out _),
                "空包名应视为无效");
        }

        [Test]
        public void Pending_FreshStamp_IsNotStale()
        {
            DateTime now = DateTime.UtcNow;

            Assert.IsFalse(CThirdPartyIntegration.IsPendingStale(now.Ticks, now));
            Assert.IsFalse(CThirdPartyIntegration.IsPendingStale(now.AddMinutes(-1).Ticks, now));
            Assert.IsFalse(CThirdPartyIntegration.IsPendingStale(now.AddMinutes(4.9).Ticks, now));
        }

        [Test]
        public void Pending_OldOrMissingStamp_IsStale()
        {
            DateTime now = DateTime.UtcNow;

            // 实测：batchmode 下成功的变更**不一定**触发域重载。若不留有效期，
            // 这条记录会一直留到很久以后某次无关的重载上，届时 manifest 早被手动改过，
            // 就会报出一条莫名其妙的"未生效"。
            Assert.IsTrue(CThirdPartyIntegration.IsPendingStale(now.AddMinutes(-6).Ticks, now));
            Assert.IsTrue(CThirdPartyIntegration.IsPendingStale(now.AddDays(-2).Ticks, now));
            Assert.IsTrue(CThirdPartyIntegration.IsPendingStale(0, now), "没有时间戳的一律当过期");
            Assert.IsTrue(CThirdPartyIntegration.IsPendingStale(-1, now));
        }

        // ========== 菜单结构 ==========

        private struct MenuEntry
        {
            public string Path;
            /// <summary>Unity 规定 validate 函数返回 bool、执行函数返回 void —— 用返回值区分角色，比读私有属性稳。</summary>
            public bool IsValidator;
            public string Method;
        }

        private static List<MenuEntry> CollectMenuEntries()
        {
            var entries = new List<MenuEntry>();

            foreach (MethodInfo method in TypeCache.GetMethodsWithAttribute<MenuItem>())
            {
                if (method.DeclaringType != typeof(CThirdPartyIntegration)) continue;

                foreach (object attribute in method.GetCustomAttributes(typeof(MenuItem), false))
                {
                    entries.Add(new MenuEntry
                    {
                        Path = ((MenuItem)attribute).menuItem,
                        IsValidator = method.ReturnType == typeof(bool),
                        Method = method.Name,
                    });
                }
            }
            return entries;
        }

        [Test]
        public void Menu_EntriesExist()
        {
            Assert.GreaterOrEqual(CollectMenuEntries().Count, 5, "2 个勾选项（各含 handler + validate）+ 1 个状态项");
        }

        [Test]
        public void Menu_EverythingLivesUnderThirdPartySubmenu()
        {
            foreach (MenuEntry entry in CollectMenuEntries())
            {
                StringAssert.StartsWith(CThirdPartyIntegration.MenuRoot, entry.Path,
                    $"{entry.Method} 的菜单项应挂在 {CThirdPartyIntegration.MenuRoot} 下");
            }
        }

        [Test]
        public void Menu_EachPathHasExactlyOneHandler()
        {
            var handlers = new Dictionary<string, string>();
            foreach (MenuEntry entry in CollectMenuEntries())
            {
                if (entry.IsValidator) continue;

                // 注意：断言的消息参数会被**立即**求值，所以这里不能写 handlers[entry.Path]（键不存在时会先抛异常）
                string previous;
                Assert.IsFalse(handlers.TryGetValue(entry.Path, out previous),
                    $"{entry.Path} 有多个执行函数（{previous} 与 {entry.Method}），Unity 只会调用其中一个");
                handlers[entry.Path] = entry.Method;
            }

            Assert.IsNotEmpty(handlers);
            foreach (KeyValuePair<string, string> pair in handlers)
            {
                MethodInfo handler = typeof(CThirdPartyIntegration).GetMethod(pair.Value,
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.IsNotNull(handler, $"{pair.Value} 找不到");
                Assert.AreEqual(typeof(void), handler.ReturnType, $"{pair.Value} 作为执行函数应返回 void");
                Assert.IsTrue(handler.IsStatic, $"{pair.Value} 必须是 static");
            }
        }

        [Test]
        public void Menu_EachToggleHasExactlyOneValidator()
        {
            foreach (CThirdPartyPackage package in CThirdPartyCatalog.All)
            {
                var paths = new List<string>();
                foreach (MenuEntry entry in CollectMenuEntries())
                {
                    if (entry.Path.Contains(package.DisplayName) && !paths.Contains(entry.Path)) paths.Add(entry.Path);
                }
                Assert.AreEqual(1, paths.Count, $"清单里的 {package.DisplayName} 应该有且只有一个菜单项");

                int validators = 0;
                var methods = new List<string>();
                foreach (MenuEntry entry in CollectMenuEntries())
                {
                    if (entry.Path != paths[0]) continue;
                    methods.Add(entry.Method + (entry.IsValidator ? "(validate)" : "(handler)"));
                    if (entry.IsValidator) validators++;
                }

                Assert.AreEqual(1, validators,
                    $"{paths[0]} 必须**恰好**有一个 validate 函数（Menu.SetChecked 只在这里刷勾）：{string.Join("、", methods)}");
            }
        }

        [Test]
        public void Menu_PathsAreUnique()
        {
            var seen = new HashSet<string>();
            foreach (MenuEntry entry in CollectMenuEntries())
            {
                Assert.IsFalse(string.IsNullOrEmpty(entry.Path), $"{entry.Method} 的菜单路径为空");
                seen.Add(entry.Path);
            }
            Assert.AreEqual(seen.Count, CThirdPartyCatalog.All.Count + 1,
                "菜单项应是「每个第三方依赖一个勾选项」+「一个状态项」");
        }
    }
}
