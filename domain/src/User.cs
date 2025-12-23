namespace Heartbeat.Domain ;

  public class User
  {
    public int Id { get; init; }
    public string DeviceId { get; init; } = string.Empty;
    public string PairCode { get; init; } = string.Empty; // Code to share for pairing
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public static string GeneratePairCode()
    {
      const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // Exclude confusing chars
      Random random = new();
      return new string(Enumerable.Range(0, 6).Select(_ => chars[random.Next(chars.Length)]).ToArray());
    }
  }