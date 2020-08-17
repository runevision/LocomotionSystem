/*
	Renders doubled sides objects without lighting. Useful for grass, trees or foliage.

	This shader renders two passes for all geometry, one for opaque parts and one with semitransparent details.
	
	This makes it possible to render transparent objects like grass without them being sorted by depth.
*/

 Shader "Nature/Vegetation Two Pass lit" {
         Properties {
                 _Color ("Main Color", Color) = (1, 1, 1, 1)
                 _MainTex ("Base (RGB) Alpha (A)", 2D) = "white" {}
                 _Cutoff ("Base Alpha cutoff", Range (0,.9)) = 1
         }
         SubShader {
                 // Set up basic lighting
                 Material {
                         Diffuse [_Color]
                         Ambient [_Color]
                 }       
                 Lighting On
                 Colormask RGB
 
                 // Render both front and back facing polygons.
                 Cull back
 
                 // first pass:
                 //   render any pixels that are more than [_Cutoff] opaque
                 Pass {  
                         AlphaTest Greater [_Cutoff]
                         SetTexture [_MainTex] {
                                 combine texture * primary, texture
                         }
                 }
 
                 // Second pass:
                 //   render in the semitransparent details.
                 Pass {
                         // Dont write to the depth buffer
                         ZWrite off
                         
                         // Only render pixels less or equal to the value
                         AlphaTest LEqual [_Cutoff]
                         
                         // Set up alpha blending
                         Blend SrcAlpha OneMinusSrcAlpha
 
                         SetTexture [_MainTex] {
                                 combine texture * primary, texture
                         }
                 }
         }
 }