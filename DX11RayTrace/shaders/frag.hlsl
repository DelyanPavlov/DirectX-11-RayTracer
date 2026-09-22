struct ps_in
{
    float4 fragPos : SV_Position;
};

struct Ray
{
    double3 Dir;
    double3 Pos;
};

cbuffer ScreenParams : register(b0)
{
    float3 Pixel00;
    float3 pixelDeltaU;
    float3 pixelDeltaV;
};

Ray getRay(int i, int j)
{
    double3 cameraPos = double3(0, 0, 0);
    double3 pixelSample = Pixel00 + (i * pixelDeltaU) + (j * pixelDeltaV);
    Ray outRay;
    outRay.Pos = cameraPos;
    outRay.Dir = pixelSample - cameraPos;
    return outRay;
}

double3 UnitVec(double3 v)
{
    return double3(v / (double) sqrt(v.x * v.x + v.y * v.y + v.z * v.z));
}

float4 ps_main(ps_in input) : SV_TARGET
{
    Ray currRay = getRay(input.fragPos.x, input.fragPos.y);
    double3 unitDir = UnitVec(currRay.Dir);
    double a = 0.5 * (unitDir.y + 1.0);
    double3 outColor = ((1.0 - a) * double3(1.0, 1.0, 1.0)) + (a * double3(0.5, 0.7, 1.0));
    
    return double4(outColor.x, outColor.y, outColor.z, 1.0);
}
