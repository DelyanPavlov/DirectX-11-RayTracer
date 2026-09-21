struct ps_in
{
    float4 fragPos : SV_Position;
};

cbuffer ScreenParams : register(b0)
{
    float2 screenSize;
    float2 Padding;
};

float4 ps_main(ps_in input) : SV_TARGET
{
    float2 pixelCoords = input.fragPos.xy;
    float pixelX = pixelCoords.x;
    float pixelY = pixelCoords.y;
    
    return float4(pixelX / screenSize.x, pixelY / screenSize.y, 0.0f, 1.0f);
}