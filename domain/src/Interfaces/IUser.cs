namespace Heartbeat.Domain.Interfaces;

public interface IUser
{
    int Id { get; set; }
    string DeviceId { get; set; }
    string PairCode { get; set; }
    DateTime CreatedAt { get; set; }
}