struct vs_in
{
    float3 position_local : POS;
};

struct vs_out
{
    float4 pos_clip : SV_POSITION;
};

vs_out vs_main(vs_in input)
{
    vs_out output = (vs_out) 0;
    output.pos_clip = float4(input.position_local, 1.0);
    return output;
}