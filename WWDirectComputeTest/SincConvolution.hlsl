// 日本語UTF-8

struct BufTypeD
{
    double d;
};

StructuredBuffer<BufTypeD>   SampleDataBuffer : register(t0);
StructuredBuffer<BufTypeD>   SinxBuffer       : register(t1);
RWStructuredBuffer<BufTypeD> BufferOut        : register(u0);

inline double Sinc(double sinx, float x)
{
    if (-0.000000001 < x && x < 0.000000001) {
        return 1.0;
    } else {
        float odxf = 1.0f / x;
        double odxd = odxf;

        return sinx * odxd;
    }
}

[numthreads(1, 1, 1)]
void CSMain(uint3 DTid : SV_DispatchThreadID)
{
    //double r = 0.0;
    // for (i=CONV_START; i <0; ++i) {
    BufferOut[DTid.x].d = Sinc(SampleDataBuffer[DTid.x].d, SinxBuffer[DTid.x].d);
}
