using Kettu;

namespace Furball.Vixie.Backends.Veldrid; 

internal class LoggerLevelVeldrid : LoggerLevel {
    public override string Name => "Veldrid";

    private new enum Channel {
        Info,
        Warning,
        Error
    }

    public static readonly LoggerLevelVeldrid InstanceInfo    = new(Channel.Info);
    public static readonly LoggerLevelVeldrid InstanceWarning = new(Channel.Warning);
    public static readonly LoggerLevelVeldrid InstanceError   = new(Channel.Error);
        
    private LoggerLevelVeldrid(Channel channel) {
        base.Channel = channel.ToString();
    }
}