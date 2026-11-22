// Peter's painted trees, bushes, grass and rocks: atlas quads that face the (fixed-angle) camera,
// baked into a few big meshes. Alpha-cut, unlit, tinted by the same global day/night colour as buildings.
Shader "Emberhold/NatureSprite"
{
    Properties { _MainTex ("Atlas", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "NatureSprite"
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
            CBUFFER_END
            half4 _EH_SpriteTint;
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; float fog : TEXCOORD1; };
            Varyings vert(Attributes i)
            {
                Varyings o;
                // slide the quad toward the camera along the view ray (same pixels, nearer depth), so the ground
                // just in front never slices the painted base off with a straight line (QC run 26)
                float3 ws = TransformObjectToWorld(i.positionOS.xyz);
                ws += normalize(_WorldSpaceCameraPos - ws) * 1.2;
                o.positionCS = TransformWorldToHClip(ws);
                o.uv = i.uv; o.color = i.color;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                clip(c.a - 0.5);
                half4 tint = _EH_SpriteTint.a > 0 ? _EH_SpriteTint : half4(1, 1, 1, 1);
                half3 col = c.rgb * i.color.rgb * tint.rgb;
                return half4(MixFog(col, i.fog), 1);
            }
            ENDHLSL
        }
    }
}
