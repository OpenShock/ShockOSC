# ShockOsc

[![Release Version](https://img.shields.io/github/v/release/OpenShock/ShockOsc?style=for-the-badge&color=6451f1)](https://github.com/Shock-Link/ShockOsc/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/OpenShock/ShockOsc/total?style=for-the-badge&color=6451f1)](https://github.com/Shock-Link/ShockOsc/releases/latest)
[![Discord](https://img.shields.io/discord/1078124408775901204?style=for-the-badge&color=6451f1&label=OpenShock%20Discord&logo=discord)](https://shockl.ink/discord)

Used as an interface for OpenShock to communicate with applications that support OSC and OscQuery like VRChat.  
Use at your own risk.

## Setup

### How to use the application

1. Download latest release from the releases section
2. Run the Application
3. Config is generated in the same folder as the application
4. Configure your generated configuration file
5. Restart the application

### Avatar contacts setup for VRC

1. Add a new **bool** parameter to your avatars (animator & params file). Name template `ShockOsc/{ShockerName}` e.g. `ShockOsc/Leg`
2. Configure one or more contact receivers on your avatar
3. Set it to constant and have it drive the designated parameter you have just created
4. Upload
5. Reset & Enable OSC

### Avatar PhysBone setup for VRC

You can use physbones to trigger shocks with verifying intensity based on the distance the bone is stretched once it's released.

1. Add a new parameter to a physbone component on your avatar with the same name as your shocker, e.g. `ShockOsc/Leg`
2. Add a new **float** parameter called `ShockOsc/{ShockerName}_Stretch` to your avatars animator & do **NOT** add to params file
3. Add a new **bool** parameter called `ShockOsc/{ShockerName}_IsGrabbed` to your avatars animator & do **NOT** add to params file
4. Edit `IntensityRange` in the configuration file to your liking

### Visual parameters

You can add some optional parameters to your avatar to visualize when the shocker is active or on cooldown.
Add these parameters to your avatars animator & params file.

- **bool** `ShockOsc/{ShockerName}_Active` enabled only while the shocker is active
- **bool** `ShockOsc/{ShockerName}_Cooldown` enabled only while the shocker isn't active and on cooldown
- **float** `ShockOsc/{ShockerName}_CooldownPercentage` 0f = shocker isn't on cooldown, 1f = shocker on cooldown (0f while shocker is active)
- **float** `ShockOsc/{ShockerName}_Intensity` 0..1f percentage value that represents how close the shock was to maximum intensity from `IntensityRange` (except for FixedIntensity)

#### Virtual Shockers

You can use the virtual, or pseudo, shockers with the name `_Any` and `_All` for some limited actions. Read more below.

##### `_Any`
- `ShockOsc/_Any_Active` is true whenever there is any shocker currently **shocking**
- `ShockOsc/_Any_Cooldown` is true whenever there is any shocker currently **on cooldown**

##### `_All`
This one can be used to make all shockers configured go off at the same time or with the same trigger.  
This virtual shocker behaves just like another configured shockers, except it relays its actions to all others.


## Config input parameters

## Details about the configuration file

```json
{
  "Osc": {
    "Chatbox": true, # Display chatbox messages on shocks
    "Hoscy": false, # Use HOSCY for chatbox
    "SendPort": 9000, # Where to send osc messages to, VRChat default is 9000. This can be left like it is for HOSCY, seem next item.
    "HoscySendPort": 9001 # Used to send chatbox message via hoscy is 'Hoscy' is to true.
  },
  "Behaviour": {
    "RandomIntensity": true,
    "RandomDuration": true, # Durations/time measurements are all in milliseconds since v1.0.1.0
    "RandomDurationStep": 100, # Random step, e.g. 1000 would be full seconds
    "DurationRange": {
      "Min": 300,
      "Max": 5000
    },
    "IntensityRange": { # Intensity percentage range for random/physbone shocks
      "Min": 1,
      "Max": 30
    },
    "FixedIntensity": 50, # If RandomIntensity is false
    "FixedDuration": 2, # If RandomDuration is false
    "HoldTime": 250, # Defines how long the parameter needs to be true in milliseconds for the shock to be triggered
    "CooldownTime": 5000, # Cooldown in milliseconds between shocks **per shocker**,
    "WhileBoneHeld": "Vibrate", # `Vibrate`, `Shock`, `None` - defines what happens when a physbone is held in hand
    "DisableWhileAfk": true, # Disable shocks when AFK (VRChats AFK Detection needs to be turned on for this)
    "ForceUnmute": false # Force unmute when shock is triggered
  },
  "Chatbox": {
    "Prefix": "[ShockOsc] ", # Prefix shown on all messages
    "DisplayRemoteControl": true, # Display commands from outside of ShockOsc in the chatbox?
    "HoscyType": "Message", # Send as Message or Notification type in hosy?
    "Types": { ## If you chose to specify any of those, you need to specify all, all or nothing :)
      "Stop": {
        "Enabled": true, # Weither to show this type as a message at all or not
        "Local": "⏸ '{ShockerName}'", # When a action is done from ShockOsc
        "Remote": "⏸ '{ShockerName}' by {Name}", # When it comes from a share code or share link (logged in)
        "RemoteWithCustomName": "⏸ '{ShockerName}' by {CustomName} [{Name}]" # When its a share link guest controlling
      },
      "Shock": {
        "Enabled": true,
        "Local": "⚡ '{ShockerName}' {Intensity}%:{DurationSeconds}s",
        "Remote": "⚡ '{ShockerName}' {Intensity}%:{DurationSeconds}s by {Name}",
        "RemoteWithCustomName": "⚡ '{ShockerName}' {Intensity}%:{DurationSeconds}s by {CustomName} [{Name}]"
      },
      "Vibrate": {
        "Enabled": true,
        "Local": "〜 '{ShockerName}' {Intensity}%:{DurationSeconds}s",
        "Remote": "〜 '{ShockerName}' {Intensity}%:{DurationSeconds}s by {Name}",
        "RemoteWithCustomName": "〜 '{ShockerName}' {Intensity}%:{DurationSeconds}s by {CustomName} [{Name}]"
      },
     "Sound": {
        "Enabled": true,
        "Local": "🔈 '{ShockerName}' {Intensity}%:{DurationSeconds}s",
        "Remote": "🔈 '{ShockerName}' {Intensity}%:{DurationSeconds}s by {Name}",
        "RemoteWithCustomName": "🔈 '{ShockerName}' {Intensity}%:{DurationSeconds}s by {CustomName} [{Name}]"
     }
    }
  },
  "ShockLink": {
    "UserHub": "https://api.shocklink.net/1/hubs/user",
    "ApiToken": "SET THIS TO YOUR OPENSHOCK API TOKEN",
    "ChatboxRemoteControls": true, # Weither to display Shocks via some other source (e.g. Website) in the Chatbox
    "Shockers": { # Key = ShockerName, Value = ShockerId
      "Leg": "d9267ca6-d69b-4b7a-b482-c455f75a4408" # Example with a name you can freely choose, e.g. Leg and a ShockerId
    }
  },
  "LastIgnoredVersion": null # Auto updater uses this to ignore updates, dont touch unless you wanna be prompted for an update you previously ignored.
}
```

## Credits

[ShockOsc Contributors](https://github.com/OpenShock/ShockOsc/graphs/contributors)
