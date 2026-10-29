Shader "WebCloth/Sample"
{
    Properties { _Color ("Color", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            struct VertexInput { float4 position : POSITION; float3 normal : NORMAL; };
            struct VertexOutput { float4 position : SV_POSITION; float3 normal : TEXCOORD0; };
            fixed4 _Color;
            VertexOutput Vert(VertexInput input)
            {
                VertexOutput output;
                output.position = UnityObjectToClipPos(input.position);
                output.normal = UnityObjectToWorldNormal(input.normal);
                return output;
            }
            fixed4 Frag(VertexOutput input, fixed facing : VFACE) : SV_Target
            {
                float3 normal = normalize(input.normal) * (facing > 0 ? 1 : -1);
                float lighting = 0.55 + 0.45 * saturate(dot(normal, normalize(float3(-0.4,0.8,-0.6))));
                return fixed4(_Color.rgb * lighting, _Color.a);
            }
            ENDCG
        }
    }
}
