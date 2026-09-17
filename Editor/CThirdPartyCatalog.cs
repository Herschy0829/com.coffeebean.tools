using System;
using System.Collections.Generic;

namespace CoffeeBean.EditorTools
{
    /// <summary>
    /// 某个第三方依赖在 <c>Packages/manifest.json</c> 里的<b>来源</b>。
    /// 分类只服务于"要不要提示用户会替换掉什么"，UPM 本身并不区分这些。
    /// </summary>
    public enum CThirdPartySource
    {
        /// <summary>manifest 的 dependencies 里没有这一项。</summary>
        None = 0,

        /// <summary>由本框架写入的 Git 地址提供（见 <see cref="CThirdPartyPackage.GitUrl"/>）。</summary>
        ManagedGit = 1,

        /// <summary>由<b>别的</b> Git 地址提供（fork、自建镜像、手写的地址）。</summary>
        ForeignGit = 2,

        /// <summary>由本地路径提供（<c>file:</c>）。</summary>
        LocalPath = 3,

        /// <summary>由 UPM registry 的版本号提供（如 <c>7.1.0</c>）。</summary>
        RegistryVersion = 4,
    }

    /// <summary>
    /// 一个**可选**的第三方依赖：包名 + 框架锁定的 UPM 地址。
    /// 只描述"是什么"，不碰工程 —— 读写 manifest 见 <see cref="CThirdPartyIntegration"/>。
    /// </summary>
    public sealed class CThirdPartyPackage
    {
        public CThirdPartyPackage(string id, string displayName, string gitUrl, string version, string revision,
            string purpose)
        {
            Id = id;
            DisplayName = displayName;
            GitUrl = gitUrl;
            Version = version;
            Revision = revision;
            Purpose = purpose;
        }

        /// <summary>UPM 包名，如 <c>com.neuecc.unirx</c>。</summary>
        public string Id { get; private set; }

        /// <summary>给人看的名字，如 <c>UniRx</c>。</summary>
        public string DisplayName { get; private set; }

        /// <summary>不含 <c>#修订</c> 的 git UPM 地址（可带 <c>?path=</c>）。</summary>
        public string GitUrl { get; private set; }

        /// <summary>上游的版本号（如 <c>7.1.0</c>）—— 只用于展示与说明。</summary>
        public string Version { get; private set; }

        /// <summary>
        /// **锁定**的修订（写进 <c>#</c> 后面）：git tag，或上游没有可用的 tag 时用 commit sha。
        /// 用它而不是分支名的理由：git 包不带修订就失去可复现性，上游一次不兼容提交
        /// 就能让所有新工程装不上。
        /// </summary>
        public string Revision { get; private set; }

        /// <summary>为什么工程会想要它（显示在状态菜单里）。</summary>
        public string Purpose { get; private set; }

        /// <summary>可直接交给 UPM 的完整引用：<c>GitUrl#Revision</c>。</summary>
        public string Url
        {
            get { return BuildUrl(GitUrl, Revision); }
        }

        /// <summary>修订的简短展示（commit sha 只显示前 7 位）。</summary>
        public string RevisionDisplay
        {
            get
            {
                if (Revision == Version || Revision.Length < 12) return Revision;
                return Revision.Substring(0, 7);
            }
        }

        public override string ToString()
        {
            return DisplayName + "（" + Id + "）" + Version + " " + Url;
        }

        /// <summary>拼 UPM 引用；修订为空则用仓库默认分支（不拼 <c>#</c>）。</summary>
        public static string BuildUrl(string gitUrl, string revision)
        {
            if (string.IsNullOrEmpty(gitUrl)) return string.Empty;
            return string.IsNullOrEmpty(revision) ? gitUrl : gitUrl + "#" + revision;
        }
    }

    /// <summary>
    /// 框架支持"一键集成"的第三方依赖清单。
    ///
    /// 修订（<c>#</c> 后面那一段）是**刻意写死**的，理由和框架模块一致：git 包不带修订就失去
    /// 可复现性，上游一次不兼容提交就能让所有新工程装不上。两个地址都在 UPM 官方支持的
    /// <c>?path=</c> 形式下**实测装过**（package.json 的 name / version 与仓库子目录都对得上）。
    /// 要升级版本，改这里的常量即可。
    /// </summary>
    public static class CThirdPartyCatalog
    {
        /// <summary>
        /// UniRx 7.1.0。UPM 包实际在仓库子目录 <c>Assets/Plugins/UniRx/Scripts</c>。
        ///
        /// **为什么锁 commit 而不是 tag**：上游的 tag <c>7.1.0</c>（最后一个 release）
        /// **早于**给这个子目录补上 <c>package.json</c> 的提交（<c>c244f9a</c>，2020-04-16，
        /// 提交信息就叫 "Add package.json"）。用 <c>#7.1.0</c> 会直接失败：
        /// <c>Repository does not contain a package manifest</c>（已实测）。
        /// 这里锁的是补上清单的那个 commit —— 与 tag 7.1.0 相比**只多了 package.json 和它的 .meta**，
        /// 源码逐字节一致；而 <c>c244f9a..master</c> 之间只改过 README，所以它同时等价于最新代码。
        /// </summary>
        public static readonly CThirdPartyPackage UniRx = new CThirdPartyPackage(
            "com.neuecc.unirx",
            "UniRx",
            "https://github.com/neuecc/UniRx.git?path=Assets/Plugins/UniRx/Scripts",
            "7.1.0",
            "c244f9a89d05cda62acd0e4572510c2d6843164c",
            "Unity 的响应式编程库（Observable / ReactiveProperty）。save 模块带了对接它的 ReactiveProperty 序列化器，但两者互不依赖。");

        /// <summary>UniTask 2.5.11：UPM 包在 <c>src/UniTask/Assets/Plugins/UniTask</c>，该 tag 自带 package.json（已实测）。</summary>
        public static readonly CThirdPartyPackage UniTask = new CThirdPartyPackage(
            "com.cysharp.unitask",
            "UniTask",
            "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask",
            "2.5.11",
            "2.5.11",
            "零分配的 async/await（PlayerLoop 驱动的 Unity 版 Task）。框架自身只用 System.Threading.Tasks.Task，这是纯可选集成。");

        private static readonly CThirdPartyPackage[] Packages = { UniRx, UniTask };

        /// <summary>全部可一键集成的第三方依赖。</summary>
        public static IReadOnlyList<CThirdPartyPackage> All
        {
            get { return Packages; }
        }

        /// <summary>按包名查找；找不到返回 null。</summary>
        public static CThirdPartyPackage Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < Packages.Length; i++)
            {
                if (string.Equals(Packages[i].Id, id, StringComparison.OrdinalIgnoreCase)) return Packages[i];
            }
            return null;
        }

        /// <summary>判断 manifest 里的取值来自哪一类来源。</summary>
        public static CThirdPartySource Classify(CThirdPartyPackage package, string manifestValue)
        {
            if (package == null || string.IsNullOrEmpty(manifestValue)) return CThirdPartySource.None;

            string value = manifestValue.Trim();
            if (IsManagedGitUrl(package, value)) return CThirdPartySource.ManagedGit;
            if (value.StartsWith("file:", StringComparison.OrdinalIgnoreCase)) return CThirdPartySource.LocalPath;

            // git 的几种写法：https://host/x.git、git@host:x/y.git、ssh://...
            if (value.IndexOf(".git", StringComparison.OrdinalIgnoreCase) >= 0
                || value.StartsWith("git@", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase))
            {
                return CThirdPartySource.ForeignGit;
            }

            return CThirdPartySource.RegistryVersion;
        }

        /// <summary>
        /// manifest 取值是否就是本框架锁定的那个地址（**忽略 <c>#tag</c>** ——
        /// tag 变了仍然算"由我们管理"，只是需要提示可以更新）。
        /// </summary>
        public static bool IsManagedGitUrl(CThirdPartyPackage package, string manifestValue)
        {
            if (package == null || string.IsNullOrEmpty(manifestValue)) return false;

            int hash = manifestValue.IndexOf('#');
            string withoutRevision = hash >= 0 ? manifestValue.Substring(0, hash) : manifestValue;
            return string.Equals(withoutRevision.Trim(), package.GitUrl, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>取 manifest 取值里 <c>#</c> 后面的版本 tag；没有则返回空串。</summary>
        public static string GetRevision(string manifestValue)
        {
            if (string.IsNullOrEmpty(manifestValue)) return string.Empty;
            int hash = manifestValue.IndexOf('#');
            return hash < 0 || hash == manifestValue.Length - 1 ? string.Empty : manifestValue.Substring(hash + 1).Trim();
        }

        /// <summary>把来源翻译成一句人话（状态菜单与确认框共用）。</summary>
        public static string DescribeSource(CThirdPartyPackage package, string manifestValue)
        {
            switch (Classify(package, manifestValue))
            {
                case CThirdPartySource.ManagedGit:
                    string revision = GetRevision(manifestValue);
                    return "已从 Git 集成（" + (string.IsNullOrEmpty(revision) ? "默认分支" : revision) + "）";
                case CThirdPartySource.ForeignGit:
                    return "由其它 Git 地址提供（" + manifestValue + "）";
                case CThirdPartySource.LocalPath:
                    return "由本地路径提供（" + manifestValue + "）";
                case CThirdPartySource.RegistryVersion:
                    return "由 registry 提供（" + manifestValue + "）";
                default:
                    return "未集成";
            }
        }
    }
}
