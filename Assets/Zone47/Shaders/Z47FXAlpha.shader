// Simple unlit transparent effect shader (works in URP via the default unlit pass).
Shader "Zone47/FXAlpha"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1,1,1,1)
        _VC ("Use Vertex Color", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _BaseMap; float4 _BaseMap_ST; float4 _BaseColor; float _VC;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            v2f vert (appdata v)
            {
                v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.color = lerp(float4(1,1,1,1), v.color, _VC) * _BaseColor; return o;
            }
            fixed4 frag (v2f i) : SV_Target { return tex2D(_BaseMap, i.uv) * i.color; }
            ENDCG
        }
    }
}
