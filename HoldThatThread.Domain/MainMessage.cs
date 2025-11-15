namespace HoldThatThread.Domain;

public class MainMessage
{
    public string Role { get; init; }
    public string Content { get; init; }
    public DateTime Timestamp { get; init; }

    public MainMessage(string role, string content)
    {
        if (role == null)
            throw new ArgumentNullException(nameof(role));
        if (string.IsNullOrEmpty(role))
            throw new ArgumentException("Role cannot be empty", nameof(role));

        if (content == null)
            throw new ArgumentNullException(nameof(content));
        if (string.IsNullOrEmpty(content))
            throw new ArgumentException("Content cannot be empty", nameof(content));

        Role = role;
        Content = content;
        Timestamp = DateTime.UtcNow;
    }
}
