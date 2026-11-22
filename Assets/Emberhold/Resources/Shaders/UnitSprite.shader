// Dinosaurs (and later units) drawn from Peter's painted portraits: one camera-facing quad per creature.
// Alpha-cut, unlit, tinted by the global day/night colour like the buildings, plus a white hit flash
// and a dithered fade for spawning in and dying.
Shader "Emberhold/UnitSprite"
{
    Properties
    {
        _MainTex ("Picture", 2D) = "white" {}
        _Flash ("Hit flash", Range(0, 1)) = 0
        _Fade ("Visible", Range(0, 1)) = 1
        _Shade ("Brightness", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "UnitSprite"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half _Flash;
                half _Fade;
                half _Shade;
            CBUFFER_END
            half4 _EH_SpriteTint;
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float fog : TEXCOORD1; float4 screen : TEXCOORD2; };
            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv;
                o.fog = ComputeFogFactor(o.positionCS.z);
                o.screen = ComputeScreenPos(o.positionCS);
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                clip(c.a - 0.5);
                // ordered dither so the fade needs no transparency sorting
                float2 p = floor(i.screen.xy / max(i.screen.w, 1e-5) * _ScreenParams.xy);
                float d = frac(dot(p, float2(0.0671, 0.00584)) * 52.98);
                clip(_Fade - d * 0.999 - 0.0005);
                half4 tint = _EH_SpriteTint.a > 0 ? _EH_SpriteTint : half4(1, 1, 1, 1);
                half3 col = c.rgb * tint.rgb * _Shade;
                col = lerp(col, half3(1, 0.95, 0.9), _Flash * 0.75);
                return half4(MixFog(col, i.fog), 1);
            }
            ENDHLSL
        }
    }
}
