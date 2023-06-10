# lh2mgr

A Lighthouse 2 management application that is used to register, turn on/off, and automatically change power states when starting/exiting applications like SteamVR.

Written in C# 11 (running .NET 7), loosely based on [risa2000/lh2ctrl](https://github.com/risa2000/lh2ctrl) as reference material for the UUIDs.

## Known working System Configurations

- Manjaro (Linux 5.15.144-2-MANJARO Kernel) with bluez 5.66-1 for Valve Index with Lighthouse 2

## Powering on/off lighthouses

`lh2mgr power <on/off> [MAC addresses]`

If you want to turn on/off pre-registered lighthouses, run `lh2mgr power <on/off>`

If you want to manually turn on/off specific lighthouses, run `lh2mgr power <on/off> 01:23:45:67:89:AB CD:EF:01:23:45:67`

## Registering lighthouses for later access

`lh2mgr register [MAC addresses]`

e.g. `lh2mgr register 01:23:45:67:89:AB CD:EF:01:23:45:67`

## Executing another program (requires registered lighthouses)

`lh2mgr exec <execution string, unquoted>`

e.g. `lh2mgr exec /path/to/steam/ubuntu12_32/reaper SteamLaunch AppId=250820 -- /path/to/steam/ubuntu12_32/steam-launch-wrapper -- /path/to/steam/steamapps/common/SteamVR/bin/vrstartup.sh`

## Debugging

If you want to get extra information about what lh2mgr is doing you can add `-v` before any commands: `lh2mgr -v <command and arguments>`

e.g. `lh2mgr -v power on`
