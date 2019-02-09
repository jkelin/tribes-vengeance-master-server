Tribes Vengeance master server
==============================

This repository is a failsafe for when QTracker GameSpy emulation service goes offline. [TribesVengeanceMasterServer](TribesVengeanceMasterServer) contains working master server. It should theoretically be possible to use this as a master server for other gamespy games.

TODO
-------------------
* Add timeout for TCP master server
* Add proper logging and Sentry support
* Add ENV based configuration
* Add HTTP service for stats and the like
* Support other games?


Reference
-------------------

[reference](reference) folder contains semi-working example of how to do this in C# and C PInvoke.

All required information can be found here http://aluigi.altervista.org/papers.htm

[GameSpy SDK Help.chm](GameSpySDKHelp.chm) desribes qr2 protocol that servers use when communicating with master. This information is however not really required.