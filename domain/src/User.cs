using Heartbeat.Domain.Interfaces;

namespace Heartbeat.Domain;

public class User
{
    public int Id { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string PairCode { get; set; } = string.Empty; // Code to share for pairing
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public static string GeneratePairCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // Exclude confusing chars
        var random = new Random();
        return new string(Enumerable.Range(0, 6).Select(_ => chars[random.Next(chars.Length)]).ToArray());
    }
}

