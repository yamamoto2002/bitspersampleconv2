using System;
using System.Collections.Generic;

namespace BpsConvWin2
{
    struct SampledData1channel
    {
        uint    sampleRate;
        float[] samples;
    }

    class SampledDataAllChannels
    {
        List<SampledData1channel> channels;
    }
}
