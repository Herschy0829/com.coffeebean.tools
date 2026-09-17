using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CoffeeBean
{
    /// <summary>
    /// build.gradle 依赖注入器（框架级必需依赖的自带兜底路径）。
    ///
    /// 这是 tools 模块**不依赖 build 模块**时的最小实现：往锚点块（默认 <c>dependencies {</c>）里
    /// 插入 <c>implementation 'group:artifact:version'</c> 行，行级幂等（已有则跳过）。
    ///
    /// 装了 build 模块时会改由它的 <c>CAndroidRequiredDeps</c>（内部用更完整的 <c>CGradleFile</c>）
    /// 执行，本类只在没装 build 时生效 —— 见 <c>CAndroidGradleDependencyFallback</c>。
    ///
    /// 与 <c>CGradleFile</c> 的差别：本类**不抛异常**，而是返回 false + error，
    /// 因为兜底路径不该因为一个文件写不进去就中断整个构建。
    /// </summary>
    public static class CAndroidGradleInjector
    {
        /// <summary>默认锚点块。</summary>
        public const string DependenciesAnchor = "dependencies {";

        /// <summary>把 <paramref name="artifacts"/> 以 implementation 形式注入到默认锚点块。</summary>
        public static bool TryEnsureDependencies(string gradlePath, IEnumerable<string> artifacts,
            out int inserted, out string error)
            => TryEnsureDependencies(gradlePath, DependenciesAnchor, artifacts, out inserted, out error);

        /// <summary>把 Maven 坐标注入为 <c>implementation '坐标'</c> 行。</summary>
        public static bool TryEnsureDependencies(string gradlePath, string anchor, IEnumerable<string> artifacts,
            out int inserted, out string error)
        {
            var lines = new List<string>();
            if (artifacts != null)
            {
                foreach (string artifact in artifacts)
                {
                    // 空白串要挡掉：否则会写出 implementation '' 这种坏行
                    if (string.IsNullOrWhiteSpace(artifact)) continue;
                    lines.Add("implementation '" + artifact.Trim() + "'");
                }
            }
            return TryEnsureLines(gradlePath, anchor, lines, out inserted, out error);
        }

        /// <summary>
        /// 在 <paramref name="anchor"/> 块内插入缺失的行（幂等）。返回是否有可写文件且无错。
        /// <paramref name="inserted"/> 为实际插入行数；文件已含全部行时返回 true 且 inserted=0。
        /// </summary>
        public static bool TryEnsureLines(string gradlePath, string anchor, IEnumerable<string> lines,
            out int inserted, out string error)
        {
            inserted = 0;
            error = null;

            if (string.IsNullOrEmpty(gradlePath) || !File.Exists(gradlePath))
            {
                error = "文件不存在：" + gradlePath;
                return false;
            }
            if (string.IsNullOrEmpty(anchor))
            {
                error = "锚点为空";
                return false;
            }
            if (lines == null)
            {
                error = "待注入行为空";
                return false;
            }

            try
            {
                string[] content = File.ReadAllLines(gradlePath, Encoding.UTF8);

                int anchorIndex = -1;
                for (int i = 0; i < content.Length; i++)
                {
                    if (content[i].Trim().Equals(anchor, StringComparison.Ordinal))
                    {
                        anchorIndex = i;
                        break;
                    }
                }
                if (anchorIndex < 0)
                {
                    error = "未找到锚点块：" + anchor;
                    return false;
                }

                string indent = LeadingWhitespace(content[anchorIndex]) + "    ";
                var toInsert = new List<string>();
                foreach (string raw in lines)
                {
                    if (string.IsNullOrWhiteSpace(raw)) continue;
                    foreach (string sub in raw.Split('\n'))
                    {
                        string trimmed = sub.TrimEnd('\r').Trim();
                        if (trimmed.Length == 0) continue;
                        if (LineExists(content, trimmed)) continue;              // 幂等：文件里已有
                        if (toInsert.Exists(x => x.Trim() == trimmed)) continue;  // 同批去重
                        toInsert.Add(indent + trimmed);
                        inserted++;
                    }
                }

                if (inserted == 0) return true;

                var sb = new StringBuilder();
                for (int i = 0; i <= anchorIndex; i++) sb.AppendLine(content[i]);
                foreach (string line in toInsert) sb.AppendLine(line);
                for (int i = anchorIndex + 1; i < content.Length; i++) sb.AppendLine(content[i]);

                File.WriteAllText(gradlePath, sb.ToString(), new UTF8Encoding(false));
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                inserted = 0;
                return false;
            }
        }

        private static bool LineExists(string[] content, string trimmed)
        {
            foreach (string line in content)
            {
                if (line.Trim() == trimmed) return true;
            }
            return false;
        }

        private static string LeadingWhitespace(string line)
        {
            int i = 0;
            while (i < line.Length && (line[i] == ' ' || line[i] == '\t')) i++;
            return line.Substring(0, i);
        }
    }
}
