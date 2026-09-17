using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CoffeeBean.EditorTools
{
    /// <summary>
    /// CoffeeBean Hub 窗口（Window &gt; CoffeeBean）里的「第三方依赖」**内嵌面板**。
    ///
    /// 与 <c>Tools/CoffeeBean/第三方依赖/</c> 下的菜单项是**同一套逻辑**（都走
    /// <see cref="CThirdPartyIntegration"/> / <see cref="CThirdPartyCatalog"/>），只是入口不同：
    /// 菜单适合顺手点，Hub 面板适合"打开工具中心一次看全"，两边状态永远一致（同一份 manifest）。
    ///
    /// 被 Hub 发现的方式是**结构约定**，不是编译期引用：
    /// <c>static class</c> + <c>[CoffeeBeanTool]</c> + <c>public static void DrawTool(Action requestRepaint)</c>。
    /// 所以 tools 依然零依赖 core；Hub 反射按 attribute 全名 + 方法签名匹配。
    /// </summary>
    [CoffeeBeanTool("第三方依赖", "UniRx / UniTask 一键从 Git 集成：勾选即装、取消即移除", "Tools")]
    public static class CThirdPartyIntegrationPanel
    {
        /// <summary>Hub 调用它来画面板；<paramref name="requestRepaint"/> 用于异步操作结束后刷新。</summary>
        public static void DrawTool(Action requestRepaint)
        {
            EditorGUILayout.LabelField(
                "与 Tools/CoffeeBean/第三方依赖/ 下的菜单是同一套操作，改哪边都一样（同一份 manifest）。",
                EditorStyles.miniLabel);
            EditorGUILayout.Space(6);

            bool busy = CThirdPartyIntegration.IsBusy;
            if (busy)
            {
                EditorGUILayout.HelpBox("正在等待 UPM 完成上一次变更（期间不要重复点击）。", MessageType.Info);
            }

            for (int i = 0; i < CThirdPartyCatalog.All.Count; i++)
            {
                DrawPackage(CThirdPartyCatalog.All[i], busy, requestRepaint);
                EditorGUILayout.Space(4);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("刷新状态", GUILayout.Width(90)))
            {
                requestRepaint?.Invoke();
            }
            if (GUILayout.Button("把状态打到 Console", GUILayout.Width(150)))
            {
                Debug.Log(BuildStatusReport());
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(
                "勾选 = Client.Add(\"<git 地址>#<锁定修订>\")，取消 = Client.Remove(包名) ——\n" +
                "与在 Package Manager 里手动操作完全等价，只改 Packages/manifest.json 的 dependencies。",
                EditorStyles.wordWrappedMiniLabel);
        }

        private static void DrawPackage(CThirdPartyPackage package, bool busy, Action requestRepaint)
        {
            string value = CThirdPartyIntegration.ReadManifestEntry(package.Id);
            CThirdPartySource source = CThirdPartyCatalog.Classify(package, value);
            bool fromGit = source == CThirdPartySource.ManagedGit;

            EditorGUILayout.BeginVertical(GUI.skin.box);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{package.DisplayName}  {package.Version}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(package.Id, EditorStyles.miniLabel, GUILayout.Width(180));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(
                "当前来源：" + CThirdPartyCatalog.DescribeSource(package, value), EditorStyles.wordWrappedMiniLabel);

            if (!fromGit)
            {
                EditorGUILayout.LabelField("框架锁定：" + package.Url, EditorStyles.wordWrappedMiniLabel);
            }

            EditorGUILayout.LabelField("作用：" + package.Purpose, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4);

            EditorGUI.BeginDisabledGroup(busy);
            string toggleLabel = fromGit
                ? "已从 Git 集成（取消勾选 = 从工程移除）"
                : "从 Git 集成（勾选 = 替换为上面那个锁定地址）";
            bool now = EditorGUILayout.ToggleLeft(toggleLabel, fromGit);
            EditorGUI.EndDisabledGroup();

            if (now != fromGit)
            {
                // 复用菜单那条路径（含确认框与全部来源分支），完成后让 Hub 重画
                CThirdPartyIntegration.ToggleWithConfirmation(package, (ok, message) =>
                {
                    if (!ok) Debug.LogError($"[CoffeeBean.Tools] {message}");
                    requestRepaint?.Invoke();
                });
                requestRepaint?.Invoke();
            }

            EditorGUILayout.EndVertical();
        }

        private static string BuildStatusReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[CoffeeBean.Tools] 第三方依赖状态（Hub 面板）：");
            for (int i = 0; i < CThirdPartyCatalog.All.Count; i++)
            {
                CThirdPartyPackage package = CThirdPartyCatalog.All[i];
                string value = CThirdPartyIntegration.ReadManifestEntry(package.Id);
                sb.AppendLine($"· {package.DisplayName}（{package.Id}）：{CThirdPartyCatalog.DescribeSource(package, value)}");
                sb.AppendLine($"    框架锁定：{package.Version} {package.Url}");
            }
            return sb.ToString();
        }
    }
}
