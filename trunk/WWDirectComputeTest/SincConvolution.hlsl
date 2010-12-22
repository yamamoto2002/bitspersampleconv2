struct BufType
{
    double d;
};

StructuredBuffer<BufType>   SampleDataBuffer : register(t0);
StructuredBuffer<BufType>   SinxBuffer : register(t1);
RWStructuredBuffer<BufType> BufferOut : register(u0);

[numthreads(1, 1, 1)]
void CSMain( uint3 DTid : SV_DispatchThreadID )
{
    BufferOut[DTid.x].d = SampleDataBuffer[DTid.x].d * SinxBuffer[DTid.x].d;
}
