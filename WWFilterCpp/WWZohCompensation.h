// 日本語

#pragma once

#include "WWFIRFilter.h"

class WWZohCompensation {
public:
    WWZohCompensation(void);
    ~WWZohCompensation(void);

    void Filter(int count, const double * inPcm, double *outPcm);

private:
    WWFIRFilter mFIRFilter;
};

