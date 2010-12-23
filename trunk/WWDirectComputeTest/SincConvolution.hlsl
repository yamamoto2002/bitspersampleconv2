// 日本語UTF-8

struct BufType
{
    double d;
};

StructuredBuffer<BufType>   SampleDataBuffer : register(t0);
StructuredBuffer<BufType>   SinxBuffer : register(t1);
RWStructuredBuffer<BufType> BufferOut : register(u0);

inline double Sinc(double sinx, double x)
{
    if (-0.000000001 < sinx || sinx < 0.000000001) {
        return 1.0;
    }

    float odxf = x;
    odxf = 1.0f / odxf;
    double odxd = odxf;

    return sinx * odxd;
}

[numthreads(1, 1, 1)]
void CSMain( uint3 DTid : SV_DispatchThreadID )
{
    //double r = 0.0;
    // for (i=CONV_START; i <0; ++i) {
    BufferOut[DTid.x].d = Sinc(SampleDataBuffer[DTid.x].d, SinxBuffer[DTid.x].d);
}
