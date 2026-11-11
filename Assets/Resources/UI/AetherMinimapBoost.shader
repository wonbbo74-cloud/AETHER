// 미니맵 화면용. 위에서 찍은 실내(04 벙커·06·07)는 조명이 바닥에만 약하게 닿아 거의 검게 나와
// 방 모양이 안 보였다. 찍은 그림의 밝기만 _Gain 배 올려 보여 준다(게임 화면에는 영향 없음).
Shader "AETHER/UI/MinimapBoost"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Gain ("Gain", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Gain;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed3 rgb = saturate(tex2D(_MainTex, i.uv).rgb * _Gain) * i.color.rgb;
                // 반투명이면 뒤의 게임 화면(바닥 무늬)이 지도 위로 비쳐 길을 읽기 어려웠다. 지도는 불투명하게.
                return fixed4(rgb, 1);
            }
            ENDCG
        }
    }
}
