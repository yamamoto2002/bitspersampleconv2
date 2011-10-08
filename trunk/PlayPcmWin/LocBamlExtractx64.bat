cd bin\x64\Release
copy ..\..\..\locbaml.exe .
locbaml /parse en-US\PlayPcmWin.resources.dll /out:..\..\..\PlayPcmWinx64.en-US.csv
del locbaml.exe
cd ..\..\..

pause

