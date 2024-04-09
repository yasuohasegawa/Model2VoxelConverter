StructuredBuffer<float4> _colors;
StructuredBuffer<float3> _modifiedVertices;

float4x4 _Rotation;
float _BoxSize;

struct Attributes
{
    float4 vertex     : POSITION;
    float4 color      : COLOR;
    float3 normal     : NORMAL;
    float2 uv         : TEXCOORD0;
};

struct Varyings
{
    float4 vertex   : SV_POSITION;
    float4 color    : COLOR;
    float2 uv       : TEXCOORD0;
    float2 uv2      : TEXCOORD1;
    float3 normal   : NORMAL;
};

Varyings vert(Attributes IN)
{
    Varyings OUT;
    //float3 modifiedVertex = _modifiedVertices[IN.vertexID];
    //OUT.vertex = TransformObjectToHClip(modifiedVertex); // IN.vertex.xyz
    VertexPositionInputs vertexInput = GetVertexPositionInputs(IN.vertex.xyz);
    OUT.vertex = float4(vertexInput.positionWS,1.0);
    OUT.uv = IN.uv;
    OUT.color = IN.color;
    OUT.normal = TransformObjectToWorldNormal(IN.normal);
                
    return OUT;
}

float4 ApplyRotation(float3 pos) {
    return mul(_Rotation,float4(pos, 1.0f));
}

float3 ApplyRotationToNormal(float3 normal) {
    return mul(_Rotation, float4(normal, 0.0f)).xyz;
}

[maxvertexcount(24)]
void geom(uint pid : SV_PrimitiveID, triangle Attributes input[3], inout TriangleStream<Varyings> outStream)
{
    Varyings output = (Varyings)0;
    float4 pos = input[0].vertex;
    float4 col = float4(1,0,0,1);
    col = _colors[pid];
    float scale = _BoxSize;
    pos.xyz -= float3(scale, scale, scale)*0.5;

    float4x4 vp = UNITY_MATRIX_VP;
    {
        // front
        float3 p = pos.xyz;
        float4 p0 = float4(p + ApplyRotation(float3(0, 0, 0) * scale),1.0);
        float4 p1 = float4(p + ApplyRotation(float3(0, 1, 0) * scale),1.0);
        float4 p2 = float4(p + ApplyRotation(float3(1, 0, 0) * scale),1.0);
        float4 p3 = float4(p + ApplyRotation(float3(1, 1, 0) * scale),1.0);

        output.vertex = mul(vp,p0);
        output.color = col;
        output.normal = ApplyRotationToNormal(float3(0, 0, -1));
        outStream.Append(output);
        output.vertex = mul(vp,p1);
        output.color = col;
        output.normal = ApplyRotationToNormal(float3(0, 0, -1));
        outStream.Append(output);
        output.vertex = mul(vp,p2);
        output.color = col;
        output.normal = ApplyRotationToNormal(float3(0, 0, -1));
        outStream.Append(output);
        output.vertex = mul(vp,p3);
        output.color = col;
        output.normal = ApplyRotationToNormal(float3(0, 0, -1));
        outStream.Append(output);
        outStream.RestartStrip();

        // back
        p0 = float4(p + ApplyRotation(float3(0, 0, 1) * scale),1.0);
        p1 = float4(p + ApplyRotation(float3(1, 0, 1) * scale),1.0);
        p2 = float4(p + ApplyRotation(float3(0, 1, 1) * scale),1.0);
        p3 = float4(p + ApplyRotation(float3(1, 1, 1) * scale),1.0);
        
        output.vertex = mul(vp,p0);
        output.color = col;
        output.normal = ApplyRotationToNormal(float3(0, 0, 1));
        outStream.Append(output);
        output.vertex = mul(vp,p1);
        output.color = col;
        output.normal =  ApplyRotationToNormal(float3(0, 0, 1));
        outStream.Append(output);
        output.vertex = mul(vp,p2);
        output.color = col;
        output.normal =  ApplyRotationToNormal(float3(0, 0, 1));
        outStream.Append(output);
        output.vertex = mul(vp,p3);
        output.color = col;
        output.normal =  ApplyRotationToNormal(float3(0, 0, 1));
        outStream.Append(output);
        outStream.RestartStrip();

        // top
        p0 = float4(p + ApplyRotation(float3(0, 1, 0) * scale),1.0);
        p1 = float4(p + ApplyRotation(float3(0, 1, 1) * scale),1.0);
        p2 = float4(p + ApplyRotation(float3(1, 1, 0) * scale),1.0);
        p3 = float4(p + ApplyRotation(float3(1, 1, 1) * scale),1.0);

        output.vertex = mul(vp,p0);
        output.color = col;
        output.normal = ApplyRotationToNormal(float3(0, 1, 0));
        outStream.Append(output);
        output.vertex = mul(vp,p1);
        output.color = col;
        output.normal = ApplyRotationToNormal(float3(0, 1, 0));
        outStream.Append(output);
        output.vertex = mul(vp,p2);
        output.color = col;
        output.normal = ApplyRotationToNormal(float3(0, 1, 0));
        outStream.Append(output);
        output.vertex = mul(vp,p3);
        output.color = col;
        output.normal = ApplyRotationToNormal(float3(0, 1, 0));
        outStream.Append(output);
        outStream.RestartStrip();

        // bottom
        p0 = float4(p + ApplyRotation(float3(1, 0, 1) * scale),1.0);
        p1 = float4(p + ApplyRotation(float3(0, 0, 1) * scale),1.0);
        p2 = float4(p + ApplyRotation(float3(1, 0, 0) * scale),1.0);
        p3 = float4(p + ApplyRotation(float3(0, 0, 0) * scale),1.0);

        output.vertex = mul(vp,p0);
        output.color = col;
        output.normal = ApplyRotationToNormal(float3(0, -1, 0));
        outStream.Append(output);
        output.vertex = mul(vp,p1);
        output.color = col;
        output.normal = ApplyRotationToNormal(float3(0, -1, 0));
        outStream.Append(output);
        output.vertex = mul(vp,p2);
        output.color = col;
        output.normal = ApplyRotationToNormal(float3(0, -1, 0));
        outStream.Append(output);
        output.vertex = mul(vp,p3);
        output.color = col;
        output.normal = ApplyRotationToNormal(float3(0, -1, 0));
        outStream.Append(output);
        outStream.RestartStrip();

        // left
        p0 = float4(p + ApplyRotation(float3(0, 0, 0) * scale),1.0);
        p1 = float4(p + ApplyRotation(float3(0, 0, 1) * scale),1.0);
        p2 = float4(p + ApplyRotation(float3(0, 1, 0) * scale),1.0);
        p3 = float4(p + ApplyRotation(float3(0, 1, 1) * scale),1.0);

        output.vertex = mul(vp,p0);
        output.color = col;
        output.normal = ApplyRotationToNormal(float3(-1, 0, 0));
        outStream.Append(output);
        output.vertex = mul(vp,p1);
        output.color = col;
        output.normal = ApplyRotationToNormal(float3(-1, 0, 0));
        outStream.Append(output);
        output.vertex = mul(vp,p2);
        output.color = col;
        output.normal = ApplyRotationToNormal(float3(-1, 0, 0));
        outStream.Append(output);
        output.vertex = mul(vp,p3);
        output.color = col;
        output.normal = ApplyRotationToNormal(float3(-1, 0, 0));
        outStream.Append(output);
        outStream.RestartStrip();

        // right
        p0 = float4(p + ApplyRotation(float3(1, 1, 1) * scale),1.0);
        p1 = float4(p + ApplyRotation(float3(1, 0, 1) * scale),1.0);
        p2 = float4(p + ApplyRotation(float3(1, 1, 0) * scale),1.0);
        p3 = float4(p + ApplyRotation(float3(1, 0, 0) * scale),1.0);

        output.vertex = mul(vp,p0);
        output.color = col;
        output.normal = ApplyRotationToNormal(float3(1, 0, 0));
        outStream.Append(output);
        output.vertex = mul(vp,p1);
        output.color = col;
        output.normal = ApplyRotationToNormal(float3(1, 0, 0));
        outStream.Append(output);
        output.vertex = mul(vp,p2);
        output.color = col;
        output.normal = ApplyRotationToNormal(float3(1, 0, 0));
        outStream.Append(output);
        output.vertex = mul(vp,p3);
        output.color = col;
        output.normal = ApplyRotationToNormal(float3(1, 0, 0));
        outStream.Append(output);
        outStream.RestartStrip();
    }
}

float3 GetViewDirectionFromPosition(float3 positionWS) {
    return normalize(GetCameraPositionWS() - positionWS);
}

float4 frag(Varyings IN) : SV_Target
{
    #ifdef SHADOW_CASTER_PASS
        return 0;
    #else
        InputData lightingInput = (InputData)0;
        lightingInput.positionWS = IN.vertex;
        lightingInput.normalWS = IN.normal; // No need to renormalize, since triangles all share normals
        lightingInput.viewDirectionWS = GetViewDirectionFromPosition(IN.vertex);
        lightingInput.shadowCoord = TransformWorldToShadowCoord(IN.vertex);

        // Read the main texture
        float3 albedo = IN.color; //SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).rgb;

        SurfaceData surfaceInput = (SurfaceData)0;
	    surfaceInput.albedo = albedo; //colorSample.rgb * _ColorTint.rgb;
	    surfaceInput.alpha = 1.0;

        #if UNITY_VERSION >= 202120
	        return UniversalFragmentPBR(lightingInput, surfaceInput);
        #else
            return UniversalFragmentPBR(lightingInput, albedo, 1, 0, 0, 1);
        #endif
    #endif
    /*
    float4 col = IN.color;
    Light mainLight = GetMainLight();

    // dot product between normal and light direction for
    // standard diffuse (Lambert) lighting
                
    float nl = max(0, dot(IN.normal, mainLight.direction));
    float3 diffuseLight = mainLight.color * nl;
    col.rgb*=diffuseLight+float3(0.8,0.8,0.8);
                
    return col;
    */
}