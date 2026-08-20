Shader "Crownfront/Sprite Outline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Outline Color", Color) = (0.15, 0.9, 1, 1)
        _OutlineSize ("Outline Size", Range(1, 4)) = 2
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            float _OutlineSize;

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 stepUv = _MainTex_TexelSize.xy * _OutlineSize;
                fixed center = tex2D(_MainTex, input.uv).a;
                fixed neighbour = 0;
                neighbour = max(neighbour, tex2D(_MainTex, input.uv + float2(stepUv.x, 0)).a);
                neighbour = max(neighbour, tex2D(_MainTex, input.uv - float2(stepUv.x, 0)).a);
                neighbour = max(neighbour, tex2D(_MainTex, input.uv + float2(0, stepUv.y)).a);
                neighbour = max(neighbour, tex2D(_MainTex, input.uv - float2(0, stepUv.y)).a);
                neighbour = max(neighbour, tex2D(_MainTex, input.uv + stepUv).a);
                neighbour = max(neighbour, tex2D(_MainTex, input.uv - stepUv).a);
                neighbour = max(neighbour, tex2D(_MainTex, input.uv + float2(stepUv.x, -stepUv.y)).a);
                neighbour = max(neighbour, tex2D(_MainTex, input.uv + float2(-stepUv.x, stepUv.y)).a);
                fixed outline = saturate(neighbour - center);
                return fixed4(input.color.rgb, input.color.a * outline);
            }
            ENDCG
        }
    }
}
