cd bin\x64\Release
copy ..\..\..\locbaml.exe .
locbaml /parse en-US\PlayPcmWin.resources.dll /out:..\..\..\PlayPcmWin.en-US.csv
del locbaml.exe
pause

