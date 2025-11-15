namespace HoldThatThread.Domain;

public class Session
{
    public Guid Id { get; init; }
    public List<MainMessage> MainChain { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime LastUpdated { get; private set; }

    public Session()
    {
        Id = Guid.NewGuid();
        MainChain = new List<MainMessage>();
        CreatedAt = DateTime.UtcNow;
        LastUpdated = DateTime.UtcNow;
    }

    public void AddToMainChain(MainMessage message)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        MainChain.Add(message);
        LastUpdated = DateTime.UtcNow;
    }
}
