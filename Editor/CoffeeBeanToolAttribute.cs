using System;

namespace CoffeeBean.EditorTools
{
    /// <summary>
    /// CoffeeBean 工具标记（**tools 模块自己的副本**）。
    ///
    /// 解耦说明：为保持模块独立（tools 不编译期依赖 core），各模块在自己的 Editor 程序集里
    /// 复制一个**同命名空间同名**的 CoffeeBeanToolAttribute 类（几行），
    /// Hub（Window &gt; CoffeeBean）用反射**按全名**匹配识别，无需模块引用 core 程序集。
    /// 改动这里请同步 core 里那份（以及其它模块的副本），保持一致。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class CoffeeBeanToolAttribute : Attribute
    {
        /// <summary>工具标题（Hub 导航列表显示）。</summary>
        public string Title { get; }

        /// <summary>工具描述（Hub 中悬停/副标题显示）。</summary>
        public string Description { get; }

        /// <summary>所属模块显示名（分组用，如 "Tools" / "Excel"）。</summary>
        public string Module { get; }

        public CoffeeBeanToolAttribute(string title, string description = "", string module = "")
        {
            Title = title;
            Description = description;
            Module = module;
        }
    }
}
