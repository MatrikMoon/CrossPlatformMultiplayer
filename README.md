# CrossPlatformMultiplayer

This is a proof of concept mod. This is not guaranteed to run, build, or even exist without errors.
There is another team working on a more polished version of this, but for now, the code is private.
In the meantime...

This set of code is designed to act as a dummy server for Beat Saber Multiplayer. Because Multiplayer uses NAT holepunching (this notably excludes Matchmaking), the mock server is relatively simple. I've chosen to piggyback off of as many Beat Games libraries as I can, so you'll notice those in your references.

## CrossPlatformMultiplayer
This is the Plugin side of the mod. This patches Beat Saber to connect to our mock client. Change the server address as you will.

## MasterServer
This is the meat of the trickery. This is a dot-net-core implementation of a Beat Saber Master Server. Run it, and then press "Create a Server" in-game to have the client do its work. Players on Oculus and Steam alike will be able to create and join each others' games.

## Quest
The Quest version of the client can be found [here](https://github.com/sc2ad/QuestCustomMulti).
