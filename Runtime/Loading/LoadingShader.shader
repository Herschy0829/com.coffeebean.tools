// CoffeeBean 通用 Loading 指示器：一圈沿轨道旋转、逐渐变大的圆点。
//
// 相对初版的改动：
// · 去掉了从未被采样的 _MainTex（死属性）
// · 去掉了 #define PI（改用本文件自己的 CB_PI，避免与 UnityCG.cginc 里的宏重定义打架）
// · 修掉 `fixed4 finalCol = (0,0,0,0);` 这种靠逗号运算符凑出来的写法，改用正规构造
// · 圆点边缘换成 smoothstep 抗锯齿（初版是硬边 if 分支，边缘有锯齿且产生分支）
// · 点数 / 尺寸 / 柔边 / 速度 / 半径 全部参数化（初版是 0.5、0.01、7→2 这些魔法数字）
// · 叠加改为按 alpha 覆盖（初版是 += 累加，圆点重叠处会过曝发白）
// · 补上 UGUI 的 Stencil/ColorMask，使其在 Mask 下也能被正确裁剪（初版没有，Image 的
//   m_Maskable 形同虚设）
//
// 属性名 _Color / _Speed / _Radius 与初版保持一致，因此已调好的材质无需改动。
Shader "CoffeeBean/Loading"
{
    Properties
    {
        _Color ("Dot Color", Color) = (1, 1, 1, 1)
        _Speed ("Rotation Speed (rad/s)", Range(0.5, 20)) = 6.27
        _Radius ("Orbit Radius", Range(0, 0.5)) = 0.3
        _DotCount ("Dot Count", Range(1, 12)) = 6
        _DotSize ("Dot Size Step", Range(0.002, 0.05)) = 0.01
        _Softness ("Edge Softness", Range(0.0005, 0.05)) = 0.004

        // UGUI 遮罩支持（与 UI/Default 一致，默认不裁剪）
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        LOD 100

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // 3.0：固定 12 次迭代的循环在默认 2.5 的指令上限下可能不够用
            #pragma target 3.0
            #include "UnityCG.cginc"

            #define CB_PI 3.14159265
            #define CB_MAX_DOTS 12
            // 相邻圆点之间的相位差（初版是 count * 0.5）
            #define CB_PHASE_STEP 0.5

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv     : TEXCOORD0;
                fixed4 color  : COLOR;
            };

            fixed4 _Color;
            half _Speed;
            half _Radius;
            half _DotCount;
            half _DotSize;
            half _Softness;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;   // 承接顶点色（Canvas 启用顶点色通道时由 Image 传入）
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                half edge = max(_Softness, 1e-5);
                fixed4 acc = fixed4(0, 0, 0, 0);

                // 固定上界 + step 掩码：避免动态循环边界，同时支持 1..12 个圆点
                for (int idx = 1; idx <= CB_MAX_DOTS; idx++)
                {
                    half active = step((half)idx, _DotCount);

                    half radian = fmod(_Time.y * _Speed + idx * CB_PHASE_STEP, CB_PI * 2.0);
                    half2 center = half2(0.5 - _Radius * cos(radian), 0.5 + _Radius * sin(radian));

                    half radius = idx * _DotSize * active;
                    half dist = length(i.uv - center);
                    half alpha = (1.0 - smoothstep(radius - edge, radius + edge, dist)) * active;

                    // 按 alpha 覆盖：重叠处不会像累加那样过曝
                    acc.rgb = lerp(acc.rgb, _Color.rgb, alpha);
                    acc.a = max(acc.a, alpha * _Color.a);
                }

                acc *= i.color;
                return acc;
            }
            ENDCG
        }
    }

    Fallback Off
}
