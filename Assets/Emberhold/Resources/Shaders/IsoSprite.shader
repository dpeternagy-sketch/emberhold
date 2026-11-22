// Draws a pre-rendered isometric building image on a camera-facing quad.
// Depth is written per pixel as if the image were an upright card through the building's centre,
// clamped to the ground for the base plate, so 3D troops and dinos sort correctly in front of and
// behind the building. Lighting is a global day/night tint plus a window glow mask.
Shader "Emberhold/IsoSprite"
{
    Properties
    {
        _MainTex ("Image", 2D) = "white" {}
        _EmisTex ("Glow mask", 2D) = "black" {}
        _Center ("Ground centre (world)", Vector) = (0, 0, 0, 0)
        _Normal ("Card normal (towards camera, flat)", Vector) = (0, 0, -1, 0)
        _Overlay ("Overlay colour (a = strength)", Color) = (1, 1, 1, 0)
        _Shade ("Shade (ruins, 1 = normal)", Float) = 1
        _Cut ("Reveal from bottom (0..1)", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "IsoSprite"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_EmisTex); SAMPLER(sampler_EmisTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Center;
                float4 _Normal;
                half4 _Overlay;
                half _Shade;
                half _Cut;
            CBUFFER_END

            // set every frame by DayNightCycle (see SpriteBuilding.SetLighting)
            half4 _EH_SpriteTint;
            half _EH_SpriteGlow;

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = i.uv;
                return o;
            }

            half4 frag(Varyings i, out float depth : SV_Depth) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                clip(c.a - 0.5);
                clip(_Cut * 1.02 - i.uv.y);

                // where would this pixel be if the building were an upright card through its centre,
                // or, for the base plate, lying on the ground?
                float3 cam = _WorldSpaceCameraPos;
                float3 dir = normalize(i.positionWS - cam);
                float3 n = _Normal.xyz;
                float tv = dot(_Center.xyz - cam, n) / min(dot(dir, n), -1e-4);
                float tg = dir.y < -1e-4 ? (_Center.y + 0.03 - cam.y) / dir.y : 1e9;
                float3 hit = cam + dir * min(tv, tg);
                float4 hc = TransformWorldToHClip(hit);
                depth = hc.z / hc.w;

                half glow = SAMPLE_TEXTURE2D(_EmisTex, sampler_EmisTex, i.uv).r;
                half4 tint = _EH_SpriteTint.a > 0 ? _EH_SpriteTint : half4(1, 1, 1, 1);
                half3 col = c.rgb * tint.rgb * _Shade + c.rgb * glow * _EH_SpriteGlow;
                col = lerp(col, _Overlay.rgb, _Overlay.a);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
