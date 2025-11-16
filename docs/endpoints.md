# HoldThatThread API Endpoints

Base URL (local): `http://localhost:5146`

## Main Chat Endpoints

### 1. Start Conversation Turn (GET SSE Pattern - Step 1)

**POST** `/api/chat/main/turn`

Creates a conversation turn and returns a turnId for streaming via GET.

**Request:**
```json
{
  "sessionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",  // optional, null for new session
  "userInput": "Explain quantum entanglement"
}
```

**Response:**
```json
{
  "sessionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "turnId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "initialMessages": [
    {
      "role": "user",
      "text": "Previous question",
      "timestamp": "2025-01-16T10:00:00Z"
    },
    {
      "role": "assistant",
      "text": "Previous answer",
      "timestamp": "2025-01-16T10:00:05Z"
    }
  ]
}
```

### 2. Stream Conversation Turn (GET SSE Pattern - Step 2)

**GET** `/api/chat/main/stream/{turnId}`

Streams the AI response using Server-Sent Events. Compatible with browser `EventSource` API.

**Parameters:**
- `turnId` (path): GUID from Step 1

**Response:** Server-Sent Events stream

**Event Types:**
- `thought`: Internal thinking process
- `answer`: Final response chunks
- `done`: Signals end of stream

**Example SSE Stream:**
```
event: thought
data: {"sessionId":"3fa85f64...","type":0,"text":"Let me think about this..."}

event: thought
data: {"sessionId":"3fa85f64...","type":0,"text":"Quantum entanglement involves..."}

event: answer
data: {"sessionId":"3fa85f64...","type":1,"text":"Quantum"}

event: answer
data: {"sessionId":"3fa85f64...","type":1,"text":" entanglement"}

event: done
data: {"sessionId":"3fa85f64...","type":2,"text":""}
```

**Browser Usage (EventSource):**
```javascript
// Step 1: Create turn
const response = await fetch('/api/chat/main/turn', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    sessionId: null,
    userInput: "Explain quantum entanglement"
  })
});
const { turnId, sessionId, initialMessages } = await response.json();

// Step 2: Stream with EventSource
const es = new EventSource(`/api/chat/main/stream/${turnId}`);

es.addEventListener('thought', (e) => {
  const data = JSON.parse(e.data);
  console.log('Thinking:', data.text);
});

es.addEventListener('answer', (e) => {
  const data = JSON.parse(e.data);
  console.log('Answer:', data.text);
});

es.addEventListener('done', () => {
  console.log('Stream complete');
  es.close();
});

es.onerror = () => {
  es.close();
};
```

## Digression Endpoints

### 1. Start Digression

**POST** `/api/chat/digress/start`

Creates a mini-chat to explore specific text from the main conversation.

**Request:**
```json
{
  "sessionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "selectedText": "quantum entanglement",
  "initialUserInput": "Can you explain this in simpler terms?"  // optional
}
```

**Response:**
```json
{
  "digressionId": "8d7c9e11-9a2b-4c5d-8f3e-1a2b3c4d5e6f"
}
```

### 2. Continue Digression

**POST** `/api/chat/digress/{digressionId}`

Sends another message in an ongoing digression.

**Request:**
```json
{
  "userInput": "Can you give an example?"
}
```

**Response:**
```json
{
  "digressionId": "8d7c9e11-9a2b-4c5d-8f3e-1a2b3c4d5e6f",
  "messages": [
    {
      "role": "system",
      "text": "Context: quantum entanglement",
      "timestamp": "2025-01-16T10:30:00Z"
    },
    {
      "role": "user",
      "text": "Can you explain this in simpler terms?",
      "timestamp": "2025-01-16T10:30:05Z"
    },
    {
      "role": "assistant",
      "text": "Quantum entanglement is...",
      "timestamp": "2025-01-16T10:30:07Z"
    },
    {
      "role": "user",
      "text": "Can you give an example?",
      "timestamp": "2025-01-16T10:30:15Z"
    },
    {
      "role": "assistant",
      "text": "Sure! Imagine...",
      "timestamp": "2025-01-16T10:30:18Z"
    }
  ]
}
```

### 3. Merge Digression

**POST** `/api/chat/digress/{digressionId}/merge`

Adds the final digression response to the main conversation history.

**Response:**
```json
{
  "sessionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

### 4. Discard Digression

**DELETE** `/api/chat/digress/{digressionId}`

Deletes the digression without merging.

**Response:** `204 No Content`

## Event Type Reference

| Value | Name | Description |
|-------|------|-------------|
| 0 | Thought | Internal thinking process (o1/o3 models) |
| 1 | Answer | Final response to user |
| 2 | Done | Signals end of stream (empty text) |

## Error Responses

**400 Bad Request:**
```json
{
  "error": "Invalid request",
  "details": "User message cannot be empty"
}
```

**404 Not Found:**
```json
{
  "error": "Turn not found",
  "details": "Turn {turnId} not found"
}
```

## Testing with curl

```bash
# Test two-step GET SSE pattern
# Step 1: Create turn
TURN_RESPONSE=$(curl -X POST http://localhost:5146/api/chat/main/turn \
  -H "Content-Type: application/json" \
  -d '{"sessionId":null,"userInput":"What is 2+2?"}')

TURN_ID=$(echo $TURN_RESPONSE | jq -r '.turnId')

# Step 2: Stream response
curl -N "http://localhost:5146/api/chat/main/stream/$TURN_ID"
```

## Notes

- **GET SSE Pattern** (POST turn → GET stream): Use this for browser `EventSource` API
- **Event Lifecycle**: `thought` events → `answer` events → `done` event
- **Digressions**: No streaming, returns complete responses for speed
- **Session Management**: Sessions persist in memory (restart clears all data)
- **Turn Cleanup**: Turns auto-delete after streaming completes
- **DTO Properties**: All messages use `text` property (not `content`), all requests use `userInput` (not `message`)
