Shader "UI/PixelScreenTransition"
{
    Properties
    {
        [PerRendererData] _MainTex ("Captured Screen", 2D) = "black" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Columns ("Pixel Columns", Float) = 1920
        _AspectRatio ("Screen Aspect Ratio", Float) = 1.777778
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct Attributes
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 position : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 localPosition : TEXCOORD1;
            };
            sampler2D _MainTex;
            float _Columns;
            float _AspectRatio;
            fixed4 _Color;
            float4 _ClipRect;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.color = input.color * _Color;
                output.uv = input.uv;
                output.localPosition = input.vertex.xy;
                return output;
            }

            fixed4 frag(Varyings input) : SV_Target
            {
                float columns = max(1.0, round(_Columns));
                float2 grid = float2(columns, max(1.0, round(columns / max(0.01, _AspectRatio))));
                float2 uv = (floor(saturate(input.uv) * grid) + 0.5) / grid;
                fixed4 result = fixed4(tex2D(_MainTex, saturate(uv)).rgb, 1.0) * input.color;
                #ifdef UNITY_UI_CLIP_RECT
                result.a *= UnityGet2DClipping(input.localPosition, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a - 0.001);
                #endif
                return result;
            }
            ENDCG
        }
    }
}
