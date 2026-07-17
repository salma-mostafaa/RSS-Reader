using RSS_Reader.Models;

namespace RSS_Reader.Storage;

public interface IChatStore
{
    List<ChatMessage> LoadMessages(Guid userId);
    void AddMessage(Guid userId, ChatMessage message);
    void Clear(Guid userId);
}