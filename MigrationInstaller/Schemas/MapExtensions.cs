namespace OpenShock.ShockOSC.MigrationInstaller.Schemas;

public static class MapExtensions
{
    public static NewSchema.ShockOscConfig ConvertToNew(this OldSchema.ShockOscConfig oldConfig)
    {
        return new NewSchema.ShockOscConfig
        {
            Osc = new NewSchema.OscConf
            {
                Hoscy = oldConfig.Osc.Hoscy,
                HoscySendPort = oldConfig.Osc.HoscySendPort,
                QuestSupport = oldConfig.Osc.QuestSupport,
                OscQuery = oldConfig.Osc.OscQuery,
                OscSendPort = oldConfig.Osc.OscSendPort,
                OscReceivePort = oldConfig.Osc.OscReceivePort,
                // New field in new schema, default to loopback
                OscSendIp = "127.0.0.1"
            },
            Behaviour = new NewSchema.BehaviourConf
            {
                RandomIntensity = oldConfig.Behaviour.RandomIntensity,
                RandomDuration = oldConfig.Behaviour.RandomDuration,
                DurationRange = new NewSchema.JsonRange<ushort>
                {
                    Min = oldConfig.Behaviour.DurationRange.Min,
                    Max = oldConfig.Behaviour.DurationRange.Max
                },
                IntensityRange = new NewSchema.JsonRange<byte>
                {
                    Min = oldConfig.Behaviour.IntensityRange.Min,
                    Max = oldConfig.Behaviour.IntensityRange.Max
                },
                FixedIntensity = oldConfig.Behaviour.FixedIntensity,
                FixedDuration = oldConfig.Behaviour.FixedDuration,
                CooldownTime = oldConfig.Behaviour.CooldownTime,
                WhileBoneHeld = (NewSchema.BoneAction)oldConfig.Behaviour.WhileBoneHeld,
                WhenBoneReleased = (NewSchema.BoneAction)oldConfig.Behaviour.WhenBoneReleased,
                BoneHeldDurationLimit = oldConfig.Behaviour.BoneHeldDurationLimit,
                HoldTime = oldConfig.Behaviour.HoldTime,
                DisableWhileAfk = oldConfig.Behaviour.DisableWhileAfk,
                ForceUnmute = oldConfig.Behaviour.ForceUnmute
            },
            Chatbox = ConvertChatbox(oldConfig.Chatbox),
            Groups = oldConfig.Groups.ToDictionary(k => k.Key, v => ConvertGroup(v.Value))
        };
    }

    private static NewSchema.ChatboxConf ConvertChatbox(this OldSchema.ChatboxConf old)
    {
        return new NewSchema.ChatboxConf
        {
            Enabled = old.Enabled,
            Prefix = old.Prefix,
            DisplayRemoteControl = old.DisplayRemoteControl,
            TimeoutEnabled = old.TimeoutEnabled,
            Timeout = old.Timeout,
            HoscyType = (NewSchema.ChatboxConf.HoscyMessageType)old.HoscyType,
            IgnoredKillSwitchActive = old.IgnoredKillSwitchActive,
            IgnoredGroupPauseActive = "Ignoring Shock, {GroupName} is paused", // New default message
            IgnoredAfk = old.IgnoredAfk,
            Types = old.Types.ToDictionary(k => (NewSchema.ControlType)k.Key, v => new NewSchema.ChatboxConf.ControlTypeConf
            {
                Enabled = v.Value.Enabled,
                Local = v.Value.Local,
                Remote = v.Value.Remote,
                RemoteWithCustomName = v.Value.RemoteWithCustomName
            })
        };
    }

    private static NewSchema.Group ConvertGroup(this OldSchema.Group g)
    {
        return new NewSchema.Group
        {
            Name = g.Name,
            Shockers = g.Shockers.ToList(),
            OverrideIntensity = g.OverrideIntensity,
            OverrideDuration = g.OverrideDuration,
            OverrideCooldownTime = g.OverrideCooldownTime,
            OverrideBoneHeldAction = g.OverrideBoneHeldAction,
            OverrideBoneReleasedAction = g.OverrideBoneReleasedAction,
            OverrideBoneHeldDurationLimit = g.OverrideBoneHeldDurationLimit,
            RandomIntensity = g.RandomIntensity,
            RandomDuration = g.RandomDuration,
            DurationRange = new NewSchema.JsonRange<ushort> { Min = g.DurationRange.Min, Max = g.DurationRange.Max },
            IntensityRange = new NewSchema.JsonRange<byte> { Min = g.IntensityRange.Min, Max = g.IntensityRange.Max },
            FixedIntensity = g.FixedIntensity,
            FixedDuration = g.FixedDuration,
            CooldownTime = g.CooldownTime,
            WhileBoneHeld = (NewSchema.BoneAction)g.WhileBoneHeld,
            WhenBoneReleased = (NewSchema.BoneAction)g.WhenBoneReleased,
            BoneHeldDurationLimit = g.BoneHeldDurationLimit
        };
    }
}