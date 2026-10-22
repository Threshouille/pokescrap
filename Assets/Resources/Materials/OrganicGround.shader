// The ground of the new island art (ShowcaseView): repeating textures picked pixel by pixel of the art (24 per tile)
// from smooth fields (_Splat: path, bank, water, land as signed distances in tiles), their edges made organic by
// noise. Grass mixes three shades by a slow noise; a path has a darker rim; water flows (_Flow), with ripples, foam
// along the banks and sparkles. Pixel exact: no blending between materials, movements by whole pixels.
Shader "Pokescrap/OrganicGround"
{
    Properties
    {
        _Splat ("Fields (R path, G bank, B water, A land)", 2D) = "black" {}
        _GrassA ("Grass", 2D) = "white" {}
        _GrassB ("Grass, light", 2D) = "white" {}
        _GrassC ("Grass, dark", 2D) = "white" {}
        _Path ("Path", 2D) = "white" {}
        _Bank ("Bank", 2D) = "white" {}
        _Water ("Water", 2D) = "white" {}
        _Ripples ("Ripples", 2D) = "black" {}
        _Foam ("Foam", 2D) = "black" {}
        _Origin ("Map origin (world)", Vector) = (0, 0, 0, 0)
        _Size ("Map size (tiles)", Vector) = (40, 30, 0, 0)
        _Flow ("Water flow (direction)", Vector) = (0, -1, 0, 0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _Splat, _GrassA, _GrassB, _GrassC, _Path, _Bank, _Water, _Ripples, _Foam;
            float4 _Origin, _Size, _Flow;

            static const float ArtPx = 24.0; // pixels of art per tile
            static const float TexPx = 96.0; // side of the repeating textures

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float2 world : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xy;
                return o;
            }

            // Hash without sin(…) × 43758: the compiler reorders that sum, and the rounding then breaks the noise along
            // the lines of its grid (straight seams across the ground).
            float hash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float noise(float2 p)
            {
                // Pixels fall exactly on the noise's grid lines (px / 110 = 4…), where the GPU breaks it into straight
                // seams: shifted so that no pixel of the art ever lands on them.
                p += float2(0.371, 0.643);
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), u.x), lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), u.x), u.y);
            }

            float fbm(float2 p) { return 0.65 * noise(p) + 0.35 * noise(p * 2.13 + 17.3); }

            // A texel of a repeating texture at a pixel of the art.
            float4 at(sampler2D tex, float2 px) { return tex2D(tex, (px + 0.5) / TexPx); }

            float4 frag(v2f i) : SV_Target
            {
                float2 px = floor((i.world - _Origin.xy) * ArtPx); // pixel of the art
                float2 tile = (px + 0.5) / ArtPx;
                float4 f = (tex2D(_Splat, tile / _Size.xy) - 0.5) * 4.0; // signed distances, tiles
                // Organic edges: the fields shifted by noise, large waves and small bumps.
                float edge = (fbm(px / 20.0) - 0.5) * 0.9 + (noise(px / 5.0 + 41.0) - 0.5) * 0.3;
                float land = f.a + edge * 0.8;
                if (land < 0.0) return float4(0, 0, 0, 0);
                float water = f.b + edge * 0.45;
                float path = f.r + edge * 0.6;
                float bank = f.g + edge;
                float t = _Time.y;

                if (water > 0.0)
                {
                    float2 flow = normalize(_Flow.xy);
                    float4 c = at(_Water, px - floor(flow * t * 5.0));
                    float4 r = at(_Ripples, px - floor(flow * t * 11.0 + float2(sin(t * 0.7) * 3.0, 0)));
                    c.rgb = lerp(c.rgb, r.rgb, step(0.5, r.a));
                    // Foam along the banks, a light line right at the edge.
                    float4 foam = at(_Foam, px - floor(flow * t * 3.0));
                    if (water < 0.28 && foam.a > 0.5) c.rgb = foam.rgb;
                    if (water < 0.05) c.rgb = float3(0.88, 0.95, 1.0);
                    // Sparkles: now and then a little cross of light.
                    float2 cell = floor(px / 9.0);
                    float2 local = px - cell * 9.0 - 4.0;
                    float h = hash(cell + floor(t * 1.7 + hash(cell) * 5.0) * 13.1);
                    if (h > 0.975 && water > 0.4 && ((local.x == 0 && abs(local.y) <= 1) || (local.y == 0 && abs(local.x) <= 1)))
                        c.rgb = float3(1, 1, 1);
                    return float4(c.rgb, 1);
                }
                if (path > 0.0)
                {
                    float4 c = at(_Path, px);
                    if (path < 0.07) c.rgb *= 0.8; // darker rim
                    return float4(c.rgb, 1);
                }
                if (bank > 0.0) return float4(at(_Bank, px).rgb, 1);
                // Grass: mostly the main shade, a few broad patches of the light and dark ones, their edges dithered.
                float g = fbm(px / 110.0 + 101.0) + (noise(px / 3.0 + 7.0) - 0.5) * 0.08;
                float4 grass = g < 0.3 ? at(_GrassC, px) : g > 0.7 ? at(_GrassB, px) : at(_GrassA, px);
                return float4(grass.rgb, 1);
            }
            ENDCG
        }
    }
}
