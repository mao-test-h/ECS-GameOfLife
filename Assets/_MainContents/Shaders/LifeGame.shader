Shader "Custom/LifeGame"
{
    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            
            #include "UnityCG.cginc"

            struct Data { float state; };
            int _Width; int _Height;
            StructuredBuffer<Data> _MaterialBuff;

            fixed4 frag (v2f_img i) : SV_Target
            {
                int2 xy = int2(_Width, _Height) * i.uv;
                Data data = _MaterialBuff[xy.y * _Width  + xy.x];
                fixed pixel = data.state;
                return fixed4(pixel, pixel, pixel, 1);
            }
            ENDCG
        }
    }
}
