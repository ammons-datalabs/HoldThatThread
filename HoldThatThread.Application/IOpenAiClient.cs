using HoldThatThread.Domain;

namespace HoldThatThread.Application;

public interface IOpenAiClient
{
    IAsyncEnumerable<StreamChunk> ReasonAsyncStreaming(List<MainMessage> messages);
    Task<string> DigressAsync(List<MainMessage> messages);
}

public class StreamChunk
{
    public StreamChunkType Type { get; set; }
    public string Content { get; set; } = string.Empty;
}

public enum StreamChunkType
{
    Reasoning,
    Answer
}
