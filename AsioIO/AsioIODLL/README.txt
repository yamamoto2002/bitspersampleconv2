1. ASIO SDKを以下の場所からダウンロードしてASIOSDK2ディレクトリが出来ます。
http://www.steinberg.net/en/company/3rd_party_developer.html

2. 以下のリポジトリをsvn チェックアウトします。
https://bitspersampleconv2.googlecode.com/svn/trunk

3. チェックアウトしたBpsConvWin2ディレクトリの中に
AsioIOディレクトリとASIOSDK2ディレクトリが並列に並ぶように、
エクスプローラでASIOSDK2ディレクトリをコピーします。すると以下のような感じになります：

C:\work\BpsConvWin2\AsioIO\AsioIO.sln 等
C:\work\BpsConvWin2\ASIOSDK2\readme.txt 等
C:\work\BpsConvWin2\sqwave2\sqwave2.sln 等

4. BpsConvWin2\AsioIO\AsioIODLL\copyasiofiles.batを実行します。以下のファイルがAsioIODLLの中にコピーされます:
ASIOSDK2/common/asiosys.h
ASIOSDK2/common/iasiodrv.h

ASIOSDK2/host/ginclude.h
ASIOSDK2/pc/asiolist.cpp
ASIOSDK2/pc/asiolist.h

5. BpsConvWin2\sqwave2\sqwave2.slnを開いて、リビルドします

ASIO is a trademark and software of Steinberg Media Technologies GmbH