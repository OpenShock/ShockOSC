# ShockOsc
Used as an interface for ShockLink to communicate with applications that support OSC like ChilloutVR and VRChat.  
Use at your own risk.

## Avatar setup for VRC
1. Add new **bool** parameter to your avatars (animator & params file). Name template ``ShockOsc/{ShockerName}`` e.g. ``ShockOsc/Leg``
2. Configure one or more contact receivers on your avatar
3. Set it to constant and have it drive the designated parameter you have just created
4. Upload
5. Reset & Enable OSC

## PhysBone setup for VRC
Optionally you can use physbones to trigger shocks with verifying intensity based on the distance the bone is stretched once it's released.
1. Add a new parameter to a physbone component on your avatar with the same name as your shocker, e.g. ``ShockOsc/Leg``
2. Add a new **float** parameter called ``ShockOsc/{ShockerName}_Stretch`` to your avatars animator & params file
3. Add a new **bool** parameter called ``ShockOsc/{ShockerName}_IsGrabbed`` to your avatars animator & params file
4. Edit `IntensityRange` in the configuration file to your liking

## Cooldown parameter
You can add a cooldown parameter to your avatar to visualize when the shocker is on cooldown.
- Add a new **bool** parameter called ``ShockOsc/{ShockerName}_Cooldown`` to your avatars animator & params file
- This parameter will be set to true when the shocker is on cooldown

## How to use the application
1. Download latest release from the releases section
2. Run the Application
3. Config is generated in the same folder as the application
4. Configure your generated configuration file
5. Restart the application

## Details about the configuration file
```json
{
  "Osc": {
    "Chatbox": true, # Display chatbox messages on shocks
    "Hoscy": true, # Use HOSCY for chatbox
    "ReceivePort": 9002, # Where OSC events are coming in, VRChat defalt is 9001. If you wanna use HOSCY create a HOSCY route
    "SendPort": 9001 # Where to send chatbox messages to, VRChat default is 9000. We can use 9001 to send it to HOSCY tho.
  },
  "Behaviour": {
    "RandomIntensity": true,
    "RandomDuration": true, # Durations/time measurements are all in milliseconds since v1.0.1.0
    "RandomDurationStep": 100, # Random step, e.g. 1000 would be full seconds
    "RandomDurationRange": {
      "Min": 300,
      "Max": 5000
    },
    "IntensityRange": { # Intensity percentage range for random/physbone shocks
      "Min": 1,
      "Max": 100
    },
    "FixedIntensity": 50, # If RandomIntensity is false
    "FixedDuration": 2, # If RandomDuration is false
    "HoldTime": 250, # Defines how long the parameter needs to be true in milliseconds for the shock to be triggered
    "CooldownTime": 5000, # Cooldown in milliseconds between shocks **per shocker**
    "DisableWhileAfk": true, # Disable shocks when afk
    "ForceUnmute": false # Force unmute when shock is triggered
  },
  "ShockLink": {
    "Type": 1, # What action to trigger. Shock = 1, Vibrate = 2, Sound = 3
    "BaseUri": "wss://api.shocklink.net",
    "ApiToken": "yourTokenGoesHere",
    "Shockers": { # Key = ShockerName, Value = ShockerId
      "Leg": "d9267ca6-d69b-4b7a-b482-c455f75a4408"
    }
  }
}
```

## Discord
https://discord.gg/AHcCbXbEcF

## Credits
[CoreOSC by dastevens](https://github.com/dastevens/CoreOSC)