Reference and testing master server implementation
--------------------------------------------------

TestServer project contains an master server example.
TestClient contains example client, that simulates what game does.

Communication between game and master goes as follows: game(fields + encryption key) => master(encrypt(ip:port + fields)) => game(decrypt)

Communication between server and master is using GameSpy's qr2 protocol as documented in [GameSpy SDK Help.chm](../GameSpySDKHelp.chm). I don't think that understanding this protocol is necessary for implementing master however.

I could not get VB port from Comms to encrypt correctly, so I had to PInvoke the original library (`encrypex_decoder.dll`). This dll can be rebuilt by installing 32bit mingw and `gcc -m32 -shared "-Wl,--add-stdcall-alias" -o "encrypex_decoder.dll" .\enctypex_decoder.c`.
