<center><div align="center">

<img alt="OpenShock Logo" height="150px" width="150px" src="https://openshock.org/IconSlowSpin.svg" />

<h1><b>ShockOSC</b></h1>

[![Release Version](https://img.shields.io/github/v/release/OpenShock/ShockOsc?style=for-the-badge&color=e14a6d)](https://github.com/OpenShock/ShockOsc/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/OpenShock/ShockOsc/total?style=for-the-badge&color=e14a6d)](https://github.com/OpenShock/ShockOsc/releases/latest)
[![Discord](https://img.shields.io/discord/1078124408775901204?style=for-the-badge&color=e14a6d&label=OpenShock%20Discord&logo=discord)](https://openshock.net/discord)

![ShockOsc](https://sea.zlucplayz.com/f/72732636ab0743c6b365/?raw=1)

Used as an interface for OpenShock to communicate with applications that support OSC and OscQuery like VRChat.

</div></center>

## Setup

[Wiki](https://wiki.openshock.org/guides/shockosc-basic/)

### Visual parameters

You can add some optional parameters to your avatar to visualize when the shocker is active or on cooldown.
Add these parameters to your avatars animator & params file.

- **bool** `ShockOsc/{GroupName}_Active` enabled only while the shocker is active
- **bool** `ShockOsc/{GroupName}_Cooldown` enabled only while the shocker isn't active and on cooldown
- **float** `ShockOsc/{GroupName}_CooldownPercentage` 0f = shocker isn't on cooldown, 1f = shocker on cooldown (0f while shocker is active)
- **float** `ShockOsc/{GroupName}_Intensity` 0..1f percentage value that represents how close the shock was to maximum intensity from `IntensityRange` (except for FixedIntensity)

#### Virtual Groups

You can use the virtual, or pseudo, shockers with the name `_Any` and `_All` for some limited actions. Read more below.

##### `_Any`
- `ShockOsc/_Any_Active` is true whenever there is any shocker currently **shocking**
- `ShockOsc/_Any_Cooldown` is true whenever there is any shocker currently **on cooldown**

##### `_All`
This one can be used to make all shockers configured go off at the same time or with the same trigger.  
This virtual shocker behaves just like another configured shockers, except it relays its actions to all others.

#### Instant Shocker Action
You may append `_IShock` to a shocker parameter if u want a shock to trigger **instantly** when this bool parameter jumps to true.
This is useful when working with an animator setup or have contact receivers trigger immediately.

E.g. `ShockOsc/_All_IShock`


## Credits

[ShockOsc Contributors](https://github.com/OpenShock/ShockOsc/graphs/contributors)

## Support

You can support the openshock dev team here: [Sponsor OpenShock](https://github.com/sponsors/OpenShock)
