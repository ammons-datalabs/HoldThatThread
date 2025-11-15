namespace HoldThatThread.Domain;

/// <summary>
/// Represents a digression mini-chat session that branches off from a main session
/// to explore a specific selected text or concept without disrupting the main conversation flow.
/// </summary>
public class DigressionSession
{
    public Guid DigressionId { get; init; }
    public Guid ParentSessionId { get; init; }
    public string? SelectedText { get; private set; }
    public List<MainMessage> Messages { get; init; }
    public DateTime CreatedUtc { get; init; }
    public DateTime LastUpdatedUtc { get; private set; }

    public DigressionSession(Guid parentSessionId, string? selectedText = null)
    {
        if (parentSessionId == Guid.Empty)
            throw new ArgumentException("Parent session ID cannot be empty", nameof(parentSessionId));

        DigressionId = Guid.NewGuid();
        ParentSessionId = parentSessionId;
        SelectedText = selectedText;
        Messages = new List<MainMessage>();
        CreatedUtc = DateTime.UtcNow;
        LastUpdatedUtc = DateTime.UtcNow;
    }

    // For deserialization/testing
    private DigressionSession()
    {
        DigressionId = Guid.Empty;
        ParentSessionId = Guid.Empty;
        Messages = new List<MainMessage>();
        CreatedUtc = DateTime.UtcNow;
        LastUpdatedUtc = DateTime.UtcNow;
    }

    public void AddMessage(MainMessage message)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        Messages.Add(message);
        LastUpdatedUtc = DateTime.UtcNow;
    }

    public void UpdateSelectedText(string? selectedText)
    {
        SelectedText = selectedText;
        LastUpdatedUtc = DateTime.UtcNow;
    }
}