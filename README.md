Tribes Vengeance master server
==============================

This repository is a failsafe for when QTracker GameSpy emulation service goes offline. It contains results of my investigation into TribesVengeance master server protocol. [reference](reference) folder contains semi-working example of how to do this in C# and C PInvoke.

Reference materials
-------------------

All required information can be found here http://aluigi.altervista.org/papers.htm

[GameSpy SDK Help.chm](../GameSpy SDK Help.chm) desribes qr2 protocol that servers use when communicating with master. This information is however not really required.

Plan of attack when QTracker is shut down
-----------------------------------------

Remimplement working master using performant and safe language like Rust.
1. embed, wrap or rewrite `encrtypex_decoder.c`
2. listen on UDP `27900` for server heartbeats.
    * These appear to happen every 20 seconds
    * One last HB is received on graceful shutdown
    * Periodically query each server using gamespy query protocol. Example of this can be seen in TribesRevengeance Stats repository.
    * Make sure that the server is reachable before declaring it live. Some servers may be behind NAT and unreachable
    * Keep index of live servers
3. listen on TCP `28910` for client server list requests.
    * Encrypt responses containing client's IP and active server list
    * Reply to clients
    * Close connection, I don't think that Tribes does this by itself

Notes
-----

* Consider publishing a firehose of events. So that apps like TribesRevengeance Stats can subscribe instead of polling
* Consider persisting in case of restarts, shutdowns and crashes
* Fuzz!