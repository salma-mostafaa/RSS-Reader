namespace RSS_Reader.Models;


public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string? PasswordHash { get; set; }
    public string? GoogleId { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool EmailVerified { get; set; }
    public string? VerificationToken { get; set; }
}
