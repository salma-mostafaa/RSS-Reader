using RSS_Reader.Models;

namespace RSS_Reader.Storage;

public interface IUserStore
{
    User? GetByEmail(string email);
    User? GetById(Guid id);
    User? GetByGoogleId(string googleId);
    User? GetByVerificationToken(string token);
    List<User> GetAll();
    User Create(string email, string? passwordHash = null);
    User CreateFromGoogle(string email, string googleId);
    void UpdatePassword(Guid userId, string passwordHash);
    void Delete(Guid userId);
    void LinkGoogle(Guid userId, string googleId);
    void VerifyEmail(string token);
}
