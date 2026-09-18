using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using CoffeeBean.EditorTools;
using NUnit.Framework;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace CoffeeBean.Tools.Tests
{
    /// <summary>
    /// 第三方依赖（UniRx / UniTask）清单与来源判定的测试。
    /// 只测**纯逻辑**（拼地址、认来源、解析 manifest）—— 真正调用 UPM 的部分由菜单手动验证，
    /// 因为 <c>Client.AddAndRemove</c> 会改工程 manifest 并触发域重载，不适合放进自动化测试。
    /// </summary>
    public class CThirdPartyCatalogTests
    {
        private static readonly CThirdPartyPackage UniRx = CThirdPartyCatalog.UniRx;
        private static readonly CThirdPartyPackage UniTask = CThirdPartyCatalog.UniTask;

        // ========== 清单本身 ==========

        [Test]
        public void All_ContainsUniRxAndUniTask()
        {
            Assert.AreEqual(2, CThirdPartyCatalog.All.Count);
            Assert.IsNotNull(CThirdPartyCatalog.Find("com.neuecc.unirx"));
            Assert.IsNotNull(CThirdPartyCatalog.Find("com.cysharp.unitask"));
        }

        [Test]
        public void All_HasNoDuplicateIds()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (CThirdPartyPackage package in CThirdPartyCatalog.All)
            {
                Assert.IsTrue(seen.Add(package.Id), $"包名重复：{package.Id}");
            }
        }

        [Test]
        public void All_EntriesAreComplete()
        {
            foreach (CThirdPartyPackage package in CThirdPartyCatalog.All)
            {
                Assert.IsFalse(string.IsNullOrEmpty(package.Id), "包名不能为空");
                Assert.IsFalse(string.IsNullOrEmpty(package.DisplayName), $"{package.Id} 缺少显示名");
                Assert.IsFalse(string.IsNullOrEmpty(package.Version), $"{package.Id} 缺少版本号");
                Assert.IsFalse(string.IsNullOrEmpty(package.Revision),
                    $"{package.Id} 必须锁定修订（tag 或 commit）—— 跟分支就不可复现了");
                Assert.IsFalse(string.IsNullOrEmpty(package.Purpose), $"{package.Id} 缺少用途说明");
                Assert.IsTrue(package.Id.Contains("."), $"{package.Id} 不像是 UPM 包名");
            }
        }

        [Test]
        public void All_GitUrlsAreUpmPathForm()
        {
            foreach (CThirdPartyPackage package in CThirdPartyCatalog.All)
            {
                StringAssert.StartsWith("https://", package.GitUrl, $"{package.Id} 的地址应是 https");
                StringAssert.Contains(".git?path=", package.GitUrl,
                    $"{package.Id} 必须带 ?path= 指向子目录，否则 UPM 会拿到仓库根（那里没有 package.json）");
            }
        }

        // ========== 地址拼接 ==========

        [Test]
        public void UniRx_Url_IsPinnedToAKnownInstallableRevision()
        {
            // 上游 tag 7.1.0 早于"给这个子目录补 package.json"的提交，用 tag 会报
            // "Repository does not contain a package manifest"（已在真机上实测）。
            // 所以锁的是 c244f9a —— 与 tag 7.1.0 相比只多了 package.json 和它的 .meta。
            Assert.AreEqual(
                "https://github.com/neuecc/UniRx.git?path=Assets/Plugins/UniRx/Scripts" +
                "#c244f9a89d05cda62acd0e4572510c2d6843164c",
                UniRx.Url);
        }

        [Test]
        public void UniRx_RevisionIsACommit_NotTheUpstreamTag()
        {
            Assert.AreEqual("7.1.0", UniRx.Version, "给人看的版本号仍是 7.1.0");
            Assert.AreNotEqual(UniRx.Version, UniRx.Revision,
                "这个库必须锁 commit：它的 tag 里没有 package.json，装不上");
            Assert.AreEqual(40, UniRx.Revision.Length, "锁定的应是完整 commit sha");
            StringAssert.IsMatch("^[0-9a-f]{40}$", UniRx.Revision);
        }

        [Test]
        public void UniTask_Url_IsPinnedToAKnownInstallableRevision()
        {
            // 这个 tag 自带 package.json（已核对：name=com.cysharp.unitask、version=2.5.11）
            Assert.AreEqual(
                "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11",
                UniTask.Url);
            Assert.AreEqual("2.5.11", UniTask.Revision);
        }

        [Test]
        public void RevisionDisplay_ShortensCommitButKeepsTags()
        {
            Assert.AreEqual("2.5.11", UniTask.RevisionDisplay, "tag 原样显示");
            Assert.AreEqual("c244f9a", UniRx.RevisionDisplay, "commit 只显示前 7 位");
        }

        [Test]
        public void BuildUrl_WithoutRevision_UsesDefaultBranch()
        {
            Assert.AreEqual("https://github.com/a/b.git", CThirdPartyPackage.BuildUrl("https://github.com/a/b.git", null));
            Assert.AreEqual("https://github.com/a/b.git", CThirdPartyPackage.BuildUrl("https://github.com/a/b.git", string.Empty));
        }

        [Test]
        public void BuildUrl_WithoutGitUrl_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, CThirdPartyPackage.BuildUrl(null, "1.0.0"));
            Assert.AreEqual(string.Empty, CThirdPartyPackage.BuildUrl(string.Empty, "1.0.0"));
        }

        [Test]
        public void ToString_MentionsIdAndUrl()
        {
            StringAssert.Contains("com.neuecc.unirx", UniRx.ToString());
            StringAssert.Contains("#c244f9a", UniRx.ToString());
        }

        // ========== 查找 ==========

        [Test]
        public void Find_IsCaseInsensitive()
        {
            Assert.IsNotNull(CThirdPartyCatalog.Find("COM.NEUECC.UNIRX"));
        }

        [Test]
        public void Find_UnknownOrEmpty_ReturnsNull()
        {
            Assert.IsNull(CThirdPartyCatalog.Find("com.not.here"));
            Assert.IsNull(CThirdPartyCatalog.Find(null));
            Assert.IsNull(CThirdPartyCatalog.Find(string.Empty));
        }

        // ========== 来源判定 ==========

        [Test]
        public void Classify_MissingValue_IsNone()
        {
            Assert.AreEqual(CThirdPartySource.None, CThirdPartyCatalog.Classify(UniRx, null));
            Assert.AreEqual(CThirdPartySource.None, CThirdPartyCatalog.Classify(UniRx, string.Empty));
            Assert.AreEqual(CThirdPartySource.None, CThirdPartyCatalog.Classify(null, UniRx.Url));
        }

        [Test]
        public void Classify_OwnPinnedUrl_IsManagedGit()
        {
            Assert.AreEqual(CThirdPartySource.ManagedGit, CThirdPartyCatalog.Classify(UniRx, UniRx.Url));
            Assert.AreEqual(CThirdPartySource.ManagedGit, CThirdPartyCatalog.Classify(UniTask, UniTask.Url));
        }

        [Test]
        public void Classify_OwnUrlWithOtherRevision_IsStillManagedGit()
        {
            // 手动改过 tag 也算"我们管的"，只是状态菜单会提示可以切回锁定版本
            string older = UniRx.GitUrl + "#7.0.0";
            Assert.AreEqual(CThirdPartySource.ManagedGit, CThirdPartyCatalog.Classify(UniRx, older));
            Assert.AreEqual(CThirdPartySource.ManagedGit, CThirdPartyCatalog.Classify(UniRx, UniRx.GitUrl));
        }

        [Test]
        public void Classify_LocalPath_IsLocalPath()
        {
            Assert.AreEqual(CThirdPartySource.LocalPath,
                CThirdPartyCatalog.Classify(UniRx, "file:../OtherAssets/SDK/CySharp/UniRx/Scripts"));
            Assert.AreEqual(CThirdPartySource.LocalPath,
                CThirdPartyCatalog.Classify(UniRx, "FILE:../Whatever"),
                "file: 前缀应忽略大小写");
        }

        [Test]
        public void Classify_RegistryVersion_IsRegistryVersion()
        {
            Assert.AreEqual(CThirdPartySource.RegistryVersion, CThirdPartyCatalog.Classify(UniRx, "7.1.0"));
            Assert.AreEqual(CThirdPartySource.RegistryVersion, CThirdPartyCatalog.Classify(UniRx, "1.0.0-pre.2"));
        }

        [Test]
        public void Classify_ForeignGit_IsForeignGit()
        {
            Assert.AreEqual(CThirdPartySource.ForeignGit,
                CThirdPartyCatalog.Classify(UniRx, "https://github.com/someone/UniRx.git?path=Assets/Plugins/UniRx/Scripts"));
            Assert.AreEqual(CThirdPartySource.ForeignGit,
                CThirdPartyCatalog.Classify(UniRx, "git@github.com:someone/UniRx.git"));
            Assert.AreEqual(CThirdPartySource.ForeignGit,
                CThirdPartyCatalog.Classify(UniRx, "ssh://git@example.com/UniRx.git#main"));
        }

        [Test]
        public void IsManagedGitUrl_IgnoresTrailingRevision()
        {
            Assert.IsTrue(CThirdPartyCatalog.IsManagedGitUrl(UniTask, UniTask.GitUrl + "#2.5.3"));
            Assert.IsTrue(CThirdPartyCatalog.IsManagedGitUrl(UniTask, "  " + UniTask.GitUrl + "#2.5.11  "),
                "首尾空白应被忽略");
        }

        [Test]
        public void IsManagedGitUrl_RejectsOtherSources()
        {
            Assert.IsFalse(CThirdPartyCatalog.IsManagedGitUrl(UniTask, "file:../UniTask"));
            Assert.IsFalse(CThirdPartyCatalog.IsManagedGitUrl(UniTask, "2.5.11"));
            Assert.IsFalse(CThirdPartyCatalog.IsManagedGitUrl(UniTask, null));
            Assert.IsFalse(CThirdPartyCatalog.IsManagedGitUrl(UniTask, string.Empty));

            // 只差一个字符（多了个斜杠）就不该被当成"我们写的"
            Assert.IsFalse(CThirdPartyCatalog.IsManagedGitUrl(UniTask, UniTask.GitUrl + "/#2.5.11"));
        }

        [Test]
        public void IsManagedGitUrl_DoesNotConfuseTheTwoLibraries()
        {
            Assert.IsFalse(CThirdPartyCatalog.IsManagedGitUrl(UniRx, UniTask.Url));
            Assert.IsFalse(CThirdPartyCatalog.IsManagedGitUrl(UniTask, UniRx.Url));
        }

        // ========== 版本 tag ==========

        [Test]
        public void GetRevision_ExtractsTag()
        {
            Assert.AreEqual("c244f9a89d05cda62acd0e4572510c2d6843164c", CThirdPartyCatalog.GetRevision(UniRx.Url));
            Assert.AreEqual("main", CThirdPartyCatalog.GetRevision("https://github.com/a/b.git#main"));
        }

        [Test]
        public void GetRevision_WithoutTag_IsEmpty()
        {
            Assert.AreEqual(string.Empty, CThirdPartyCatalog.GetRevision("https://github.com/a/b.git"));
            Assert.AreEqual(string.Empty, CThirdPartyCatalog.GetRevision("file:../x"));
            Assert.AreEqual(string.Empty, CThirdPartyCatalog.GetRevision(null));
            Assert.AreEqual(string.Empty, CThirdPartyCatalog.GetRevision("https://github.com/a/b.git#"),
                "只有 # 没有内容时应视为无 tag");
        }

        // ========== 说明文案 ==========

        [Test]
        public void DescribeSource_CoversEveryKind()
        {
            StringAssert.Contains("Git", CThirdPartyCatalog.DescribeSource(UniRx, UniRx.Url));
            StringAssert.Contains("c244f9a", CThirdPartyCatalog.DescribeSource(UniRx, UniRx.Url));
            StringAssert.Contains("其它 Git", CThirdPartyCatalog.DescribeSource(UniRx, "https://github.com/x/y.git"));
            StringAssert.Contains("本地路径", CThirdPartyCatalog.DescribeSource(UniRx, "file:../x"));
            StringAssert.Contains("registry", CThirdPartyCatalog.DescribeSource(UniRx, "7.1.0"));
            Assert.AreEqual("未集成", CThirdPartyCatalog.DescribeSource(UniRx, null));
        }

        [Test]
        public void DescribeSource_DefaultBranchIsSpelledOut()
        {
            StringAssert.Contains("默认分支", CThirdPartyCatalog.DescribeSource(UniRx, UniRx.GitUrl));
        }

        // ========== 强制依赖：package.json 必须声明清单里的这两个包 ==========

        /// <summary>
        /// tools 把 UniRx / UniTask 声明成**硬依赖**（"强制依赖"）：装了 tools 的工程必然有它们。
        ///
        /// 声明的版本必须与清单里锁定的版本一致 —— 否则会出现"package.json 说 2.5.11、
        /// 一键集成却装别的版本"这种自相矛盾。另外这两个包不在任何 registry 里，
        /// 所以 core 的 registry 里 tools 条目也必须登记完整 UPM 地址（core 侧另有测试锁住）。
        /// </summary>
        [Test]
        public void PackageJson_DeclaresCatalogEntriesAsHardDependencies()
        {
            PackageInfo info = PackageInfo.FindForAssembly(typeof(CThirdPartyCatalog).Assembly);
            Assert.IsNotNull(info, "应能解析到 tools 包本身");
            Assert.IsNotEmpty(info.resolvedPath);

            string json = File.ReadAllText(Path.Combine(info.resolvedPath, "package.json"));

            foreach (CThirdPartyPackage package in CThirdPartyCatalog.All)
            {
                Match m = Regex.Match(json, "\"" + Regex.Escape(package.Id) + "\"\\s*:\\s*\"([^\"]+)\"");
                Assert.IsTrue(m.Success, $"tools 必须把 {package.Id} 声明为硬依赖（强制依赖），否则工程里不一定有它");
                Assert.AreEqual(package.Version, m.Groups[1].Value,
                    $"{package.Id} 在 package.json 里的版本必须与清单锁定的 {package.Version} 一致");
            }
        }
    }
}
