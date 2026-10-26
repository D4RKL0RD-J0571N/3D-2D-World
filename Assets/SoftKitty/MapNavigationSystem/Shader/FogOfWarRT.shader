
Shader "SoftKitty/FogOfWarRT"
{
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

           sampler2D _MainTex;
           float2 _RevealUV;
           float _Radius;
           float _Mode;

           struct v2f {
                float2 uv : TEXCOORD0;
                float4 pos : SV_POSITION;
            };

            v2f vert (
                float4 vertex : POSITION,  
                float2 uv : TEXCOORD0  
                )
            {
                v2f o;
                o.pos = UnityObjectToClipPos(vertex);
                o.uv = uv;
                return o;
            }

           float4 frag(v2f i) : SV_Target
           {
               float oldFog = tex2D(_MainTex, i.uv).r;
               float dist = distance(i.uv, _RevealUV);
               float reveal = clamp((_Radius*2-dist)*0.2+(_Radius-dist)*(0.2/_Radius), 0, 1);
               float final= 0;
               if(_Mode<0.8)
               {
                  final=min(oldFog+reveal, 1);
                  if(dist<_Radius*1.5){
                     if(dist>_Radius){
                        final= min(final*1.3,1);
                     }
                  }
               }
               else if(_Mode<1.8)
               {
                  final= max(oldFog-reveal*6, 0);
               } 
               else if(_Mode<2.8)
               {
                  final= 1;
               }

               
               return final;
           }
           ENDCG
        }
    }
}