Shader "Unlit/CoursFreya"
{
    Properties //input data 
    {
        _ColorA ("ColorA", Color) = (1,1,1,1)
        _ColorB ("ColorB", Color) = (1,1,1,1)
        
        _ColorStart ("Color Start", Range(0, 1)) = 0.0
        _ColorEnd("ColorEnd", Range(0, 1)) = 1.0
    }
    SubShader
    {
        Pass
        {
            //BLEND DstColor Zero // Multiply

            CGPROGRAM //start shader code, before is shaderlab 
            #pragma vertex vert //tell the compiler which is the gramgnet and vertex shader
            #pragma fragment frag
            
            #include "UnityCG.cginc" //File for unity specific things 
            
            #define TAU 6.28318530718

            float4 _ColorA;
            float4 _ColorB;
            float _ColorStart;
            float _ColorEnd;

            struct MeshData // per vertex MeshData 
            {
                float4 vertex : POSITION; //vertex local position
                float3 normal : NORMAL; //normal local
                //float4 color : COLOR; //color
                float2 uv0 : TEXCOORD0; //uv coordinate, refers uvchannel0
            };

            struct Interpolators //data that get pass from vertes shader to fragment shader
            {
                float4 vertex : SV_POSITION; //clip space position (-1 ; 1)
                float3 normal : TEXCOORD0; // used to pass data not necessarily uv 
                float2 uv : TEXCOORD1;
            };

            //vertex shader take a meshdata in argument (data of a vertex) and output the data to send to fragment shader // can be called interpolator  
            //it interpolates between the vertex to create fragment (not only one fragment per vertex )           
            Interpolators vert (MeshData v) 
            {  
                Interpolators o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal); //convert local space to clip space
                o.uv = v.uv0; //(v.uv0 + _Offset) * _Scale;
                return o;
            }
            
            //float (32 bit float) can be used always and good for world coord
            //half (16 bit float) can be used nearly always
            //fixed (lower precision) -1 to 1
            //float4 -> half4 -> fixed4
            //float4x4 -> half4x4 (C# : Matrix4x4)
            
            float InverseLerp(float a, float b, float v)
            {
                return(v-a)/(b-a);
            }

            float4 frag (Interpolators i) : SV_Target  //take into input the structure send to it by fragment shader and output a color //output to framebuffer which is SC_Target
            {
                return float4(i.uv.xy, 0, 0);
            }
            ENDCG
        }
    }
}
