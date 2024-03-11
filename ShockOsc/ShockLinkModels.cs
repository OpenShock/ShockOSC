namespace OpenShock.ShockOsc;

public class OwnShockersResponseResponseData
{
    public string? Message { get; set; }
    public required Device[] Data { get; set; }
}

public class Device
{
    public required Guid Id { get; set; }
    public required string Name { get; set; }
    public DateTime CreatedOn { get; set; }
    public IList<Shocker> Shockers { get; set; } = new List<Shocker>();

    public class Shocker
    {
        public required Guid Id { get; set; }
        public required string Name { get; set; }
        public required bool IsPaused { get; set; }
        public required DateTime CreatedOn { get; set; }
        public required int RfId { get; set; }
        public required string Model { get; set; }
    }
}