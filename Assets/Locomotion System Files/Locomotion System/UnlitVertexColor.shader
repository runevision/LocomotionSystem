Shader "Unlit/VertexColor" {
	SubShader {
		Tags { "RenderType"="Transparent" Queue=Transparent }
		Blend SrcAlpha OneMinusSrcAlpha
		ZWrite Off

		Pass {
			CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag

				#include "UnityCG.cginc"

				struct VertOut {
					float4 position : POSITION;
					float4 color : COLOR;
				};

				struct VertIn {
					float4 vertex : POSITION;
					float4 color : COLOR;
				};

				VertOut vert (VertIn input)
				{
					VertOut output;
					output.position = UnityObjectToClipPos(input.vertex);
					output.color = input.color;
					return output;
				}

				float4 frag (VertOut input) : SV_Target
				{
					return input.color;
				}
			ENDCG
		}
	}
}
