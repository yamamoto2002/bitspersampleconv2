1. ASIO SDKを以下の場所からダウンロードしてきます。展開するとASIOSDK2ディレクトリが出来ます。
http://www.steinberg.net/en/company/3rd_party_developer.html

2. 以下のリポジトリを、svn チェックアウトします。
https://bitspersampleconv2.googlecode.com/svn/trunk

C:\work\BpsConvWin2\ディレクトリにチェックアウトしたとして話を進めます。

3. チェックアウトしたBpsConvWin2ディレクトリの中に
AsioIOディレクトリとASIOSDK2ディレクトリが並列に並ぶように、
エクスプローラでASIOSDK2ディレクトリをコピーします。すると以下のような感じになります：

C:\work\BpsConvWin2\AsioIO\AsioIO.sln 等
C:\work\BpsConvWin2\ASIOSDK2\readme.txt 等
C:\work\BpsConvWin2\sqwave2\sqwave2.sln 等

4. C:\work\BpsConvWin2\AsioIO\AsioIODLL\copyasiofiles.batを実行します。
以下のファイルがC:\work\BpsConvWin2\AsioIO\AsioIODLL\の中にコピーされます:
ASIOSDK2/common/asiosys.h
ASIOSDK2/common/iasiodrv.h

ASIOSDK2/host/ginclude.h
ASIOSDK2/pc/asiolist.cpp
ASIOSDK2/pc/asiolist.h

5. C:\work\BpsConvWin2\sqwave2\sqwave2.slnをVS2010で開いて、リビルドします

ASIO is a trademark and software of Steinberg Media Technologies GmbH