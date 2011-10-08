cd bin\x86\Release
copy ..\..\..\locbaml.exe .
locbaml /parse en-US\PlayPcmWin.resources.dll /out:..\..\..\PlayPcmWinx86.en-US.csv
del locbaml.exe
cd ..\..\..

pause

