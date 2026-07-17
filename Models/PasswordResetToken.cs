namespace RSS_Reader.Models;

public class PasswordResetToken
{
    public string Token { get; set; } = "";
    public Guid UserId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool Used { get; set; }
}
