using RSS_Reader.Models;

namespace RSS_Reader.Storage;

public interface IPasswordResetStore
{
    PasswordResetToken Create(Guid userId);
    PasswordResetToken? Get(string token);
    void MarkUsed(string token);
}
