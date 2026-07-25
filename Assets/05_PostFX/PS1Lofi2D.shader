Shader "Hidden/PostFX/PS1Lofi2D"
{
    Properties
    {
        [Header(Master)]
        _Intensity            ("Effect Intensity", Range(0,1)) = 1

        [Header(Resolution)]
        _TargetHeight         ("Virtual Vertical Resolution", Range(60, 1080)) = 240
        _PixelateAmount       ("Pixelate Amount", Range(0,1)) = 1

        [Header(Color Quantization)]
        [KeywordEnum(Off, Bayer4, Bayer8)]
        _Dither               ("Dither Pattern", Float) = 1
        _ColorBits            ("Bits Per Channel", Range(1,8)) = 5
        _DitherStrength       ("Dither Strength", Range(0,1.5)) = 1
        _DitherScale          ("Dither Cell Scale", Range(0.25,4)) = 1

        [Header(Grade)]
        _Brightness           ("Brightness", Range(0,2)) = 0.95
        _Contrast             ("Contrast", Range(0,3)) = 1.15
        _Saturation           ("Saturation", Range(0,2)) = 0.6
        _ShadowTint           ("Shadow Tint", Color) = (0.24, 0.33, 0.45, 1)
        _HighlightTint        ("Highlight Tint", Color) = (1.0, 0.94, 0.84, 1)
        _TintStrength         ("Tint Strength", Range(0,1)) = 0.6

        [Header(CRT)]
        _BarrelDistortion     ("Barrel Distortion", Range(0,0.5)) = 0.06
        _ChromaticAberration  ("Chromatic Aberration", Range(0,10)) = 1.0
        _ScanlineIntensity    ("Scanline Intensity", Range(0,1)) = 0.12
        _VignetteInner        ("Vignette Inner", Range(0,1.5)) = 0.45
        _VignetteOuter        ("Vignette Outer", Range(0,1.5)) = 1.05
        _Vignette             ("Vignette Amount", Range(0,1)) = 0.7

        [Header(Signal Noise)]
        _NoiseAmount          ("Static Noise", Range(0,0.5)) = 0.05
        _FrameRate            ("Effect Frame Rate (0 = smooth)", Range(0,60)) = 15
        _JitterAmount         ("Line Jitter", Range(0,0.05)) = 0.002
        _JitterDensity        ("Line Jitter Density", Range(0,1)) = 0.03
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        Pass
        {
            Name "PS1Lofi2D"

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile_local_fragment _DITHER_OFF _DITHER_BAYER4 _DITHER_BAYER8

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // Vert / Varyings / _BlitTexture / sampler_PointClamp / sampler_LinearClamp 제공
            // URP 17(Unity 6)부터 Blit.hlsl 은 core 패키지로 이동함
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float  _Intensity;
                float  _TargetHeight;
                float  _PixelateAmount;
                float  _ColorBits;
                float  _DitherStrength;
                float  _DitherScale;
                float  _Brightness;
                float  _Contrast;
                float  _Saturation;
                float4 _ShadowTint;
                float4 _HighlightTint;
                float  _TintStrength;
                float  _BarrelDistortion;
                float  _ChromaticAberration;
                float  _ScanlineIntensity;
                float  _VignetteInner;
                float  _VignetteOuter;
                float  _Vignette;
                float  _NoiseAmount;
                float  _FrameRate;
                float  _JitterAmount;
                float  _JitterDensity;
            CBUFFER_END

            // ---------------------------------------------------------------
            // Helpers
            // ---------------------------------------------------------------

            // 배열 인덱싱 없는 해석적 Bayer 행렬 (GLES/모바일 안전)
            float Bayer2(float2 a)
            {
                a = floor(a);
                return frac(a.x * 0.5 + a.y * a.y * 0.75);
            }
            #define BAYER4(a) (Bayer2(0.5 * (a)) * 0.25 + Bayer2(a))
            #define BAYER8(a) (BAYER4(0.5 * (a)) * 0.25 + Bayer2(a))

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float2 BarrelWarp(float2 uv, float k)
            {
                float2 c  = uv - 0.5;
                float  r2 = dot(c, c);
                return 0.5 + c * (1.0 + k * r2);
            }

            // 가상 해상도 그리드로 UV 스냅
            float2 SnapUV(float2 uv, float2 vres, float amount)
            {
                float2 s = (floor(uv * vres) + 0.5) / vres;
                return lerp(uv, s, amount);
            }

            float3 SampleScene(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, saturate(uv)).rgb;
            }

            // ---------------------------------------------------------------
            // Fragment
            // ---------------------------------------------------------------
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv0    = input.texcoord;
                float  aspect = _ScreenParams.x / max(_ScreenParams.y, 1.0);

                // 가상 해상도 (예: 240p -> 427x240)
                float2 vres = float2(max(round(_TargetHeight * aspect), 1.0),
                                     max(round(_TargetHeight), 1.0));

                // 이펙트 전용 시간을 저프레임으로 계단화 -> 로파이한 "덜컹임"
                float t = (_FrameRate > 0.5)
                        ? floor(_Time.y * _FrameRate) / _FrameRate
                        : _Time.y;

                // ---- 1. 화면 왜곡 -------------------------------------------------
                float2 uv = BarrelWarp(uv0, _BarrelDistortion);

                // 스캔라인 단위 수평 지터 (VHS/신호 불량)
                float lineIdx  = floor(uv.y * vres.y);
                float lineSeed = Hash21(float2(lineIdx * 0.137, floor(t * 7.0)));
                float lineOn   = step(1.0 - _JitterDensity, lineSeed);
                float jitter   = (Hash21(float2(lineIdx, floor(t * 11.0))) - 0.5) * 2.0;
                uv.x += jitter * _JitterAmount * lineOn;

                // 화면 밖(배럴 왜곡으로 밀려난 영역)은 검게
                float2 bounds   = step(0.0, uv) * step(uv, 1.0);
                float  inBounds = bounds.x * bounds.y;

                // ---- 2. 샘플링 (색수차 + 픽셀 스냅) --------------------------------
                float2 dir = (uv - 0.5);
                float2 ca  = dir * _ChromaticAberration * 0.002;

                float3 col;
                col.r = SampleScene(SnapUV(uv + ca, vres, _PixelateAmount)).r;
                col.g = SampleScene(SnapUV(uv,      vres, _PixelateAmount)).g;
                col.b = SampleScene(SnapUV(uv - ca, vres, _PixelateAmount)).b;

                float3 original = SampleScene(uv0);

                // 디더/스캔라인/노이즈 좌표는 "가상 픽셀" 단위로 고정
                float2 pcoord = floor(uv * vres);

                // ---- 3. 이후 연산은 감마 공간에서 (PS1은 8bit 감마 프레임버퍼) -----
            #ifndef UNITY_COLORSPACE_GAMMA
                col = LinearToSRGB(col);
            #endif

                // ---- 4. 컬러 그레이딩 ----------------------------------------------
                col *= _Brightness;
                col  = (col - 0.5) * _Contrast + 0.5;

                float lum = dot(col, float3(0.299, 0.587, 0.114));
                col = lerp(lum.xxx, col, _Saturation);

                float3 tint = lerp(_ShadowTint.rgb, _HighlightTint.rgb, saturate(lum));
                col = lerp(col, col * tint, _TintStrength);

                // ---- 5. CRT 연출 ---------------------------------------------------
                float2 vd  = (uv - 0.5) * float2(aspect, 1.0) * 2.0;
                float  vig = 1.0 - smoothstep(_VignetteInner, _VignetteOuter, length(vd));
                col *= lerp(1.0, vig, _Vignette);

                float scan = 1.0 - _ScanlineIntensity * step(0.5, frac(pcoord.y * 0.5));
                col *= scan;

                // ---- 6. 필름 그레인 / 신호 노이즈 -----------------------------------
                float n = Hash21(pcoord + float2(t * 71.3, t * 37.7));
                col += (n - 0.5) * _NoiseAmount;

                col = saturate(col);

                // ---- 7. 디더링 + 색심도 양자화 (반드시 마지막) ----------------------
                float levels = max(exp2(round(_ColorBits)) - 1.0, 1.0);

                float bayer = 0.5;
            #if defined(_DITHER_BAYER4)
                bayer = BAYER4(pcoord / max(_DitherScale, 0.01));
            #elif defined(_DITHER_BAYER8)
                bayer = BAYER8(pcoord / max(_DitherScale, 0.01));
            #endif

                float d = 0.5 + (bayer - 0.5) * _DitherStrength;
                col = floor(col * levels + d) / levels;

            #ifndef UNITY_COLORSPACE_GAMMA
                col = SRGBToLinear(col);
            #endif

                col *= inBounds;

                col = lerp(original, col, saturate(_Intensity));
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
