// Map ground: Peter's three tiling textures (grass, dirt, light stone) blended by a control map that
// WorldBuilder paints, plus the old painted colour map for beach, river bed, mountain rock and snow.
// Control map: R = dirt, G = stone paths, B = use the painted colour instead, A = large-scale brightness.
// Grass is sampled twice at different scales and angles so its repeat is hard to spot.
Shader "Emberhold/GroundSplat"
{
    Properties
    {
        _Painted ("Painted colour map", 2D) = "white" {}
        _Control ("Control map", 2D) = "black" {}
        _Grass ("Grass", 2D) = "white" {}
        _GroundSat ("Ground saturation", Float) = 1.05
        _GroundBright ("Ground brightness", Float) = 1.05
        _Dirt ("Dirt", 2D) = "white" {}
        _Stone ("Stone", 2D) = "white" {}
        _Tile ("World units per texture tile", Float) = 6
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Ground"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_Painted); SAMPLER(sampler_Painted);
            TEXTURE2D(_Control); SAMPLER(sampler_Control);
            TEXTURE2D(_Grass); SAMPLER(sampler_Grass);
            TEXTURE2D(_Dirt); SAMPLER(sampler_Dirt);
            TEXTURE2D(_Stone); SAMPLER(sampler_Stone);

            CBUFFER_START(UnityPerMaterial)
                float4 _Painted_ST;
                float4 _Control_ST;
                float4 _Grass_ST;
                half _GroundSat;
                half _GroundBright;
                float4 _Dirt_ST;
                float4 _Stone_ST;
                float _Tile;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float fog : TEXCOORD3;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.uv = i.uv;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 ctl = SAMPLE_TEXTURE2D(_Control, sampler_Control, i.uv);
                half3 painted = SAMPLE_TEXTURE2D(_Painted, sampler_Painted, i.uv).rgb;
                float2 w = i.positionWS.xz / _Tile;
                float2 w2 = float2(w.x * 0.8 - w.y * 0.6, w.x * 0.6 + w.y * 0.8) * 0.63 + 0.37;   // rotated, larger copy
                half3 grass = lerp(SAMPLE_TEXTURE2D(_Grass, sampler_Grass, w).rgb,
                                   SAMPLE_TEXTURE2D(_Grass, sampler_Grass, w2).rgb, 0.4);
                half3 dirt = SAMPLE_TEXTURE2D(_Dirt, sampler_Dirt, w * 0.9).rgb;
                half3 stone = SAMPLE_TEXTURE2D(_Stone, sampler_Stone, w * 1.3).rgb;
                // dirt stays earthy under the global saturation boost (it went rust-orange otherwise)
                dirt = lerp(dot(dirt, half3(0.3, 0.59, 0.11)).xxx, dirt, 0.75);
                half3 tex = lerp(grass, dirt, ctl.r);
                tex = lerp(tex, stone, ctl.g);
                tex *= lerp(0.8, 1.12, ctl.a);
                half3 albedo = lerp(tex, painted, ctl.b);
                // QC: the raw grass is far more saturated and brighter than the grass on Peter's building
                // plates (ground 99,193,0 vs plates 89,118,28), so the city looked pasted onto neon.
                // Pull it towards the plates so buildings sit in the same world.
                half lum = dot(albedo, half3(0.3, 0.59, 0.11));
                albedo = lerp(lum.xxx, albedo, _GroundSat) * _GroundBright;

                Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half3 n = normalize(i.normalWS);
                half ndl = saturate(dot(n, light.direction));
                // painted look like the (unlit) building pictures: flat sunlit ground shows the texture's own
                // colour, slopes and shadows only darken it (no blown-out yellow-green from sun + sky adding up)
                half sun = ndl * light.shadowAttenuation;
                // full sun = the texture colour (x1.0); only real shadows and steep slopes darken, never below 0.75
                half3 lit = albedo * lerp(half3(0.75, 0.78, 0.86), half3(1, 1, 1), saturate(sun / max(n.y * 0.86, 0.5)));
                lit = min(lit, albedo * 1.05);
                lit = MixFog(lit, i.fog);
                return half4(lit, 1);
            }
            ENDHLSL
        }
        // depth (for the depth prepass, so IsoSprite and post effects see the ground) and shadow casting
        UsePass "Universal Render Pipeline/Lit/DEPTHONLY"
        UsePass "Universal Render Pipeline/Lit/SHADOWCASTER"
    }
    FallBack "Universal Render Pipeline/Lit"
}
