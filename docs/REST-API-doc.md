# REST API Documentation

This document specifies the REST API for the Irma backend. The API is heavily inspired by the Microsoft 365 Copilot Chat API and provides functionality for creating and managing chat conversations with Irma, an intelligent assistant that can answer questions about your connected devices.

The single point of truth of how the API is and should be implemented can be found in the REST-API-spec.yaml file that has the OpenAPI/Swagger Specification. These two files should stay in sync all the time, but if they by some reason don't, then the specification is the valid one.

---

## Table of Contents

1. [Introduction](#1-introduction)
2. [Getting Started](#2-getting-started)
3. [Core Concepts](#3-core-concepts)
4. [API Reference](#4-api-reference)
5. [Best Practices](#5-best-practices)
6. [Appendix](#6-appendix)

---

## 1. Introduction

### 1.1 Overview

The Irma API enables developers to build conversational experiences with Irma, an AI-powered assistant designed to answer questions about your connected devices (such as cameras, sensors, and other IoT equipment). The API follows REST principles and supports both synchronous and streaming response patterns.

### 1.2 Key Features

- **Multi-turn conversations**: Maintain context across multiple exchanges
- **Streaming responses**: Real-time Server-Sent Events (SSE) for progressive content delivery
- **Device context**: Provide additional context about your devices for more accurate responses
- **Secure authentication**: Azure Entra ID and B2C integration
- **Comprehensive error handling**: Structured error responses with trace IDs for debugging

### 1.3 Use Cases

- "What is the current temperature reading from my device?"
- "Show me the status of camera model Ixx/2.0"
- "What features does this sensor support?"
- "Explain the configuration options available"

### 1.4 Base URL

All API URLs referenced in this document have the following base URL:

```
https://<irma-url>/v1/irma/
```

In this URL, `<irma-url>` is a placeholder for the actual URL where the Irma backend will be hosted.

**Note:** Version format is `v1`, not semantic versioning. Future major versions will be `v2`, `v3`, etc.

---

## 2. Getting Started

### 2.1 Prerequisites

- An active Azure Entra ID or Azure AD B2C tenant
- Valid credentials for authentication
- HTTPS-enabled client
- A product identifier (e.g., `Ixx/1.0`) representing your device model and version

### 2.2 Authentication

All requests to the Irma API must be authenticated using a bearer token in the `Authorization` header.

```
Authorization: Bearer {token}
```

#### Authentication Flow

The Irma API uses **Azure Entra ID** (formerly Azure AD) and **Azure AD B2C** for authentication and authorization.

**Token Acquisition:**

1. Obtain an access token from Azure Entra ID or Azure AD B2C using OAuth 2.0 flows
2. Include the token in the `Authorization` header of all API requests
3. Tokens are typically valid for 1 hour; implement token refresh logic in your client

**Required Scopes/Claims:**

- `api://irma/chat.read` - Read access to chat conversations
- `api://irma/chat.write` - Create and send messages in conversations

**Token Validation:**

- Tokens are validated on every request
- Expired tokens return `401 Unauthorized`
- Invalid tokens return `401 Unauthorized` with error details

**Example Token Request (Azure AD B2C):**

```http
POST https://login.microsoftonline.com/{tenant-id}/oauth2/v2.0/token
Content-Type: application/x-www-form-urlencoded

client_id={client-id}
&scope=api://irma/chat.read api://irma/chat.write
&grant_type=client_credentials
&client_secret={client-secret}
```

### 2.3 Quick Start Example

**Step 1: Create a conversation**

```bash
curl -X POST https://irma.example.com/v1/irma/conversations \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{}'
```

**Step 2: Send a message**

```bash
curl -X POST https://irma.example.com/v1/irma/conversations/{conversationId}/chat \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "message": "What is the current status?",
    "product": "Ixx/1.0"
  }'
```

---

## 3. Core Concepts

### 3.1 Conversations

A **conversation** represents a multi-turn chat session between a user and Irma. Each conversation:

- Has a unique identifier (`id`)
- Maintains message history across turns
- Can be in different states (`active`, `disengagedForRai`)
- Automatically generates a display name from the first user message
- Tracks the number of turns (exchanges) in the conversation

**Conversation Lifecycle:**

1. **Created** - A new conversation is initialized (POST `/conversations`)
2. **Active** - Messages can be sent and received
3. **Disengaged for RAI** - Conversation stopped due to Responsible AI policies (e.g., harmful content detected)

### 3.2 Messages

Messages are the fundamental units of conversation. Each message has:

- A unique identifier (`id`)
- Text content
- Creation timestamp
- Role (user request or assistant response)

Messages are returned in chronological order, with the most recent messages appearing in the response.

### 3.3 Product Identifier

The **product** field is a required parameter that identifies the device model and version you're asking about. It follows the format:

```
<ProductName>/<Version>
```

**Examples:**
- `Ixx/1.0` - Ixx camera, version 1.0
- `Ixx-Pro/2.5` - Ixx Pro camera, version 2.5
- `SensorX/1.2` - SensorX device, version 1.2

**Purpose:**
- Helps Irma provide device-specific answers
- Used for analytics and usage tracking
- Enables version-specific feature guidance

**Validation:**
- Must be a non-empty string
- Should follow the slash-separated format
- Invalid formats return `400 Bad Request`

### 3.4 Additional Context

The `additionalContext` parameter allows you to provide extra information to help Irma answer more accurately. It's an **array** of context messages, where each message contains:

| Property | Type | Description |
| --- | --- | --- |
| `text` | String | The contextual information (e.g., sensor readings, configuration data) |
| `description` | String | Optional description of what this context represents |

**Example:**

```json
{
  "message": "Is the temperature too high?",
  "additionalContext": [
    {
      "text": "Current temperature: 42°C, Normal range: 20-35°C",
      "description": "Temperature sensor reading"
    },
    {
      "text": "Device location: Server Room A, Floor 3",
      "description": "Physical location"
    }
  ],
  "product": "Ixx/1.0"
}
```

### 3.5 Conversation States

Conversations can be in one of the following states:

| State | Description |
| --- | --- |
| `active` | Conversation is active and can receive new messages |
| `disengagedForRai` | Conversation has been stopped due to Responsible AI policy violations (e.g., harmful content, policy violations) |

**Note:** Once a conversation enters `disengagedForRai` state, no further messages can be sent. Create a new conversation to continue.

### 3.6 Streaming vs. Synchronous

The Irma API offers two response patterns:

**Synchronous (`/chat`):**
- Returns the complete response in a single HTTP response
- Simpler to implement
- Higher perceived latency for long responses
- Use for: Short queries, batch processing

**Streaming (`/chatOverStream`):**
- Returns response chunks progressively via Server-Sent Events (SSE)
- Lower perceived latency
- Better user experience for long responses
- Use for: Interactive chat UIs, real-time applications

---

## 4. API Reference

### 4.1 Error Handling

### 4.1 Error Handling

Irma uses a consistent error envelope across all endpoints. Clients should inspect the HTTP status code first and then parse the JSON body for additional context.

#### Common Status Codes

| Status | Meaning | Typical Causes |
| --- | --- | --- |
| `400 Bad Request` | The request payload is invalid. | Missing required fields, malformed JSON, unsupported values. |
| `401 Unauthorized` | Authentication failed. | Missing/expired token, invalid token. |
| `403 Forbidden` | Authenticated but not authorized. | Token lacks required scopes/claims. |
| `404 Not Found` | Resource does not exist. | Unknown `conversationId`. |
| `409 Conflict` | Request conflicts with current state. | Attempting to message a disengaged conversation, or the conversation context has expired on the server. |
| `500 Internal Server Error` | Unexpected server-side failure. | Transient backend issues. |

#### Error Response Body

```json
{
  "code": "InvalidRequest",
  "message": "The 'message' field is required.",
  "target": "message",
  "details": [
    {
      "code": "MissingField",
      "message": "Provide a non-empty string.",
      "target": "message"
    }
  ],
  "traceId": "00-4d7b42a3f9c1c24baa7231e4ff0d1b61-7e12a2f4b4de924b-01"
}
```

| Property | Type | Description |
| --- | --- | --- |
| `code` | String | Machine-readable error code (e.g., `InvalidRequest`, `Unauthorized`, `NotFound`). |
| `message` | String | Human-readable description suitable for logging and debugging. |
| `target` | String | Optional pointer to the field or resource that caused the error. |
| `details` | Array | Optional list of structured error details for compound failures. |
| `traceId` | String | Correlation identifier for support and observability. **Include this when contacting Irma support.** |

#### Error Handling in Streaming

For streaming endpoints (`/chatOverStream`), errors can occur at two stages:

**Before Stream Establishment (HTTP error):**
- Standard error response with appropriate status code and error body
- Connection never upgrades to SSE

**After Stream Establishment (during streaming):**
- An error event is sent before closing the connection:

```
event: error
data: {
  "code": "InternalError",
  "message": "Stream processing interrupted",
  "traceId": "00-abc123..."
}
```

- Client should handle the error event and gracefully close the connection
- No further data events will be sent after an error event

### 4.2 Endpoints

#### 4.2.1 Create Conversation

Creates a new conversation with Irma.

**URL:** `POST /conversations`

**Request Headers:**

| Header | Value | Required |
| --- | --- | --- |
| `Authorization` | `Bearer {token}` | Yes |
| `Content-Type` | `application/json` | Yes |

**Request Body:**

The request body should be an empty JSON object.

```json
{}
```

**Response:**

If successful, this method returns a `201 Created` response code and a JSON object in the response body with the following properties:

| Property | Type | Description |
| --- | --- | --- |
| `id` | String | The unique identifier for the conversation. Use this in subsequent chat requests. |
| `createdDateTime` | String | The date and time the conversation was created, in UTC (ISO 8601 format). |
| `displayName` | String | The display name of the conversation. Empty on creation; auto-generated from first message. |
| `state` | String | The state of the conversation (`active`, `disengagedForRai`). |
| `turnCount` | Integer | The number of turns in the conversation. Initially `0`. |

**Error Responses:**
- `401 Unauthorized` - Invalid or missing authentication token
- `403 Forbidden` - Token lacks required permissions
- `500 Internal Server Error` - Server-side error (check `traceId`)

**Example:**

*Request:*

```http
POST https://irma.example.com/v1/irma/conversations
Content-Type: application/json
Authorization: Bearer {token}

{}
```

*Response:*

```http
HTTP/1.1 201 Created
Content-Type: application/json

{
  "id": "0d110e7e-2b7e-4270-a899-fd2af6fde333",
  "createdDateTime": "2025-10-29T10:00:00.000Z",
  "displayName": "",
  "state": "active",
  "turnCount": 0
}
```

---

#### 4.2.2 Chat (Synchronous)

Sends a message to an existing conversation and receives a complete response.

**URL:** `POST /conversations/{conversationId}/chat`

**Path Parameters:**

| Parameter | Type | Description |
| --- | --- | --- |
| `conversationId` | String | The unique identifier of the conversation (from create conversation response). |

**Request Headers:**

| Header | Value | Required |
| --- | --- | --- |
| `Authorization` | `Bearer {token}` | Yes |
| `Content-Type` | `application/json` | Yes |

**Request Body:**

| Property | Type | Description | Required |
| --- | --- | --- | --- |
| `message` | String | The chat message to send to Irma. Cannot be empty. | Yes |
| `additionalContext` | Array | Additional context messages. See [Additional Context](#34-additional-context). | No |
| `product` | String | A slash-separated string identifying the product and version (e.g., "Ixx/1.0"). | Yes |

**Additional Context Schema:**

Each item in the `additionalContext` array should have:

| Property | Type | Description | Required |
| --- | --- | --- | --- |
| `text` | String | The contextual information. | Yes |
| `description` | String | Description of what this context represents. | No |

**Response:**

If successful, this method returns a `200 OK` response code and a JSON object with the following properties:

| Property | Type | Description |
| --- | --- | --- |
| `id` | String | The unique identifier for the conversation. |
| `createdDateTime` | String | The date and time the conversation was created, in UTC. |
| `displayName` | String | The display name of the conversation (auto-generated from first message). |
| `state` | String | The state of the conversation. |
| `turnCount` | Integer | The number of turns in the conversation. |
| `messages` | Array | An array of message objects representing the full conversation history. |

**Message Object Schema:**

| Property | Type | Description |
| --- | --- | --- |
| `id` | String | The unique identifier for the message. |
| `text` | String | The content of the message. |
| `createdDateTime` | String | The date and time the message was created, in UTC. |

**Note:** The response includes the **full conversation history** including all previous messages and the latest exchange.

**Error Responses:**
- `400 Bad Request` - Invalid request payload (missing required fields, invalid format)
- `401 Unauthorized` - Invalid or missing authentication token
- `403 Forbidden` - Token lacks required permissions
- `404 Not Found` - Conversation ID not found
- `409 Conflict` - Conversation is in `disengagedForRai` state, or the server-side context has expired. If the context has expired, the client **must** start a new conversation by calling `POST /conversations`.
- `500 Internal Server Error` - Server-side error (check `traceId`)

**Example:**

*Request:*

```http
POST https://irma.example.com/v1/irma/conversations/0d110e7e-2b7e-4270-a899-fd2af6fde333/chat
Content-Type: application/json
Authorization: Bearer {token}

{
  "message": "What is the weather like in Stockholm?",
  "product": "Ixx/1.0"
}
```

*Response:*

```http
HTTP/1.1 200 OK
Content-Type: application/json

{
  "id": "0d110e7e-2b7e-4270-a899-fd2af6fde333",
  "createdDateTime": "2025-10-29T10:00:00.000Z",
  "displayName": "Weather in Stockholm",
  "state": "active",
  "turnCount": 1,
  "messages": [
    {
      "id": "cc211f56-1a5e-0af0-fec2-c354ce468b95",
      "text": "What is the weather like in Stockholm?",
      "createdDateTime": "2025-10-29T10:05:00.000Z"
    },
    {
      "id": "3fe6b260-c682-4f8e-a201-022ccb300742",
      "text": "The weather in Stockholm is sunny with a temperature of 15°C.",
      "createdDateTime": "2025-10-29T10:05:05.000Z"
    }
  ]
}
```

*Example with Additional Context:*

```http
POST https://irma.example.com/v1/irma/conversations/0d110e7e-2b7e-4270-a899-fd2af6fde333/chat
Content-Type: application/json
Authorization: Bearer {token}

{
  "message": "Is this temperature reading normal?",
  "additionalContext": [
    {
      "text": "Current temperature: 42°C",
      "description": "Sensor reading"
    },
    {
      "text": "Normal operating range: 20-35°C",
      "description": "Device specifications"
    }
  ],
  "product": "Ixx/1.0"
}
```

---

#### 4.2.3 Chat Over Stream (SSE)

Sends a message to an existing conversation and receives a streamed response via Server-Sent Events.

**URL:** `POST /conversations/{conversationId}/chatOverStream`

**Path Parameters:**

| Parameter | Type | Description |
| --- | --- | --- |
| `conversationId` | String | The unique identifier of the conversation. |

**Request Headers:**

| Header | Value | Required |
| --- | --- | --- |
| `Authorization` | `Bearer {token}` | Yes |
| `Content-Type` | `application/json` | Yes |

**Request Body:**

Same as the synchronous chat endpoint. See [Chat (Synchronous)](#422-chat-synchronous) for details.

**Response:**

If successful, this method returns a `200 OK` response with a `Content-Type` of `text/event-stream`. The response is a stream of Server-Sent Events (SSE).

**SSE Event Types:**

1. **Data Events** (`event: message`, default)
2. **Completion Event** (`event: end`)
3. **Heartbeat Event** (`event: keepalive`)
4. **Error Event** (`event: error`)

**Data Event Schema:**

Each data event contains a JSON object with:

| Property | Type | Description |
| --- | --- | --- |
| `id` | String | The unique identifier for the conversation. |
| `messages` | Array | An array containing one or more message objects with text chunks. |

**Message Object in Stream:**

| Property | Type | Description |
| --- | --- | --- |
| `id` | String | The unique identifier for the message (consistent across chunks). |
| `text` | String | A chunk of the response message content. |
| `createdDateTime` | String | The date and time the message was created, in UTC. |

**Stream Semantics and Framing:**

- **Incremental Deltas:** Each chunk contains a text fragment, not the cumulative response. Clients must concatenate `text` fields in arrival order to assemble the complete message.
- **Event Types:** 
  - Default data events have `event: message` (may be omitted per SSE spec)
  - Final event has `event: end` with empty messages array
  - Heartbeat events have `event: keepalive` with empty data
  - Error events have `event: error` with error details
- **Completion Signal:** The stream concludes with an `event: end` event. Clients should close the connection after receiving this.
- **Heartbeat Frames:** Every ~15 seconds, a `keepalive` event may be sent to prevent connection timeouts. Ignore these for content assembly.
- **SSE Format:** Each event ends with a blank line (`\n\n`). Do not assume fixed chunk sizes.
- **Error Handling:** If an error occurs mid-stream, an `event: error` is sent followed by connection closure.

**Error Responses:**

Same as synchronous endpoint for errors before stream establishment. After stream starts, errors are sent as error events.

**Example:**

*Request:*

```http
POST https://irma.example.com/v1/irma/conversations/0d110e7e-2b7e-4270-a899-fd2af6fde333/chatOverStream
Content-Type: application/json
Authorization: Bearer {token}

{
  "message": "What is the weather like in Stockholm?",
  "product": "Ixx/1.0"
}
```

*Response:*

```http
HTTP/1.1 200 OK
Content-Type: text/event-stream

data: {
  "id": "0d110e7e-2b7e-4270-a899-fd2af6fde333",
  "messages": [
    {
      "id": "3fe6b260-c682-4f8e-a201-022ccb300742",
      "text": "The weather in Stockholm is sunny",
      "createdDateTime": "2025-10-29T10:05:05.000Z"
    }
  ]
}

data: {
  "id": "0d110e7e-2b7e-4270-a899-fd2af6fde333",
  "messages": [
    {
      "id": "3fe6b260-c682-4f8e-a201-022ccb300742",
      "text": " with a temperature of 15°C.",
      "createdDateTime": "2025-10-29T10:05:05.000Z"
    }
  ]
}

event: end
data: {
  "id": "0d110e7e-2b7e-4270-a899-fd2af6fde333",
  "messages": []
}

```

*Example with Heartbeat and Error:*

```http
HTTP/1.1 200 OK
Content-Type: text/event-stream

data: {
  "id": "0d110e7e-2b7e-4270-a899-fd2af6fde333",
  "messages": [
    {
      "id": "3fe6b260-c682-4f8e-a201-022ccb300742",
      "text": "Processing your request",
      "createdDateTime": "2025-10-29T10:05:05.000Z"
    }
  ]
}

event: keepalive
data: {}

event: error
data: {
  "code": "InternalError",
  "message": "Stream processing interrupted",
  "traceId": "00-4d7b42a3f9c1c24baa7231e4ff0d1b61-7e12a2f4b4de924b-01"
}

```

---

### 4.3 Data Models

#### 4.3.1 Conversation

Represents a chat conversation with Irma.

```json
{
  "id": "string",
  "createdDateTime": "string (ISO 8601)",
  "displayName": "string",
  "state": "active | disengagedForRai",
  "turnCount": 0
}
```

#### 4.3.2 Message

Represents a single message in a conversation.

```json
{
  "id": "string",
  "text": "string",
  "createdDateTime": "string (ISO 8601)"
}
```

#### 4.3.3 Context Message

Represents additional context for a conversation.

```json
{
  "text": "string",
  "description": "string (optional)"
}
```

#### 4.3.4 Error

Represents an error response.

```json
{
  "code": "string",
  "message": "string",
  "target": "string (optional)",
  "details": [
    {
      "code": "string",
      "message": "string",
      "target": "string (optional)"
    }
  ],
  "traceId": "string"
}
```

---

## 5. Best Practices

### 5.1 Error Handling

**Always check HTTP status codes first:**

```python
response = requests.post(url, headers=headers, json=payload)
if response.status_code == 200:
    # Success
    data = response.json()
elif response.status_code == 401:
    # Refresh token and retry
    refresh_token()
elif response.status_code >= 500:
    # Retry with exponential backoff
    retry_with_backoff()
else:
    # Handle client errors
    error = response.json()
    log_error(error['traceId'])
```

**Include traceId when reporting issues:**

Always include the `traceId` from error responses when contacting support. This helps quickly locate the specific request in logs.

**Handle streaming errors gracefully:**

```javascript
const eventSource = new EventSource(url);

eventSource.addEventListener('error', (event) => {
  const error = JSON.parse(event.data);
  console.error('Stream error:', error.traceId);
  eventSource.close();
});

eventSource.addEventListener('end', () => {
  eventSource.close();
});
```

### 5.2 Security

**Never log or expose bearer tokens:**

```python
# BAD
logger.info(f"Request: {headers}")  # Contains Authorization header

# GOOD
safe_headers = {k: v for k, v in headers.items() if k != 'Authorization'}
logger.info(f"Request headers: {safe_headers}")
```

**Always use HTTPS in production:**

The Irma API requires HTTPS for all requests in production environments. HTTP is only supported for local development.

**Implement token refresh logic:**

```python
def get_valid_token():
    if token_expired():
        return refresh_token()
    return current_token
```

**Store tokens securely:**

- Use secure storage mechanisms (keychain, encrypted storage)
- Never hardcode tokens in source code
- Use environment variables for development

### 5.3 Streaming Best Practices

**Assemble chunks correctly:**

```javascript
let fullMessage = '';

eventSource.addEventListener('message', (event) => {
  const data = JSON.parse(event.data);
  data.messages.forEach(msg => {
    fullMessage += msg.text;  // Concatenate chunks
  });
});
```

**Handle connection interruptions:**

```javascript
eventSource.addEventListener('error', (event) => {
  if (eventSource.readyState === EventSource.CLOSED) {
    // Connection closed, reconnect if needed
    setTimeout(() => reconnect(), 5000);
  }
});
```

**Implement timeout handling:**

```python
import signal

def timeout_handler(signum, frame):
    raise TimeoutError("Stream timeout")

signal.signal(signal.SIGALRM, timeout_handler)
signal.alarm(60)  # 60 second timeout
```

### 5.4 Performance

**Choose the right endpoint:**

- Use `/chat` for batch processing or when you need the complete response
- Use `/chatOverStream` for interactive UIs to reduce perceived latency

**Provide relevant context:**

Use `additionalContext` to give Irma the information it needs upfront, reducing back-and-forth exchanges:

```json
{
  "message": "Is this normal?",
  "additionalContext": [
    {
      "text": "Temperature: 42°C, Humidity: 85%, Pressure: 1013 hPa"
    }
  ],
  "product": "Ixx/1.0"
}
```

**Reuse conversations:**

Don't create a new conversation for every message. Reuse the same conversation to maintain context and reduce overhead.

### 5.5 Observability

**Use traceId for correlation:**

Store the `traceId` from responses to correlate client-side events with server-side logs:

```python
response = requests.post(url, headers=headers, json=payload)
data = response.json()
logger.info(f"Response received, traceId: {data.get('traceId')}")
```

**Monitor conversation states:**

Track when conversations enter `disengagedForRai` state to identify potential content issues:

```python
if conversation['state'] == 'disengagedForRai':
    logger.warning(f"Conversation {conversation['id']} disengaged for RAI")
    alert_moderation_team(conversation['id'])
```

---

## 6. Appendix

### 6.1 Changelog

#### v1 (2025-10-29)
- Initial release
- Added conversation creation endpoint
- Added synchronous chat endpoint
- Added streaming chat endpoint
- Azure Entra ID and B2C authentication support

### 6.2 Support

**Reporting Issues:**

When reporting issues, please include:
- The `traceId` from the error response
- Request timestamp
- HTTP status code
- Request payload (with sensitive data redacted)

**Contact:**

- Email: support@irma.example.com
- Documentation: https://docs.irma.example.com

### 6.3 Future Enhancements

The following features are under consideration for future API versions:

- **List Conversations:** `GET /conversations` to retrieve all user conversations
- **Get Conversation:** `GET /conversations/{conversationId}` to retrieve specific conversation details
- **Delete Conversation:** `DELETE /conversations/{conversationId}` to remove conversations
- **Conversation Search:** Search within conversation history
- **Webhooks:** Proactive notifications for device events
- **Batch Operations:** Process multiple messages in a single request

### 6.4 Related Resources

- [Microsoft 365 Copilot Chat API Overview](https://learn.microsoft.com/en-us/microsoft-365-copilot/extensibility/api/ai-services/chat/overview)
- [Azure Entra ID Documentation](https://learn.microsoft.com/en-us/entra/identity/)
- [Server-Sent Events Specification](https://html.spec.whatwg.org/multipage/server-sent-events.html)
- [OAuth 2.0 RFC](https://datatracker.ietf.org/doc/html/rfc6749)

---

**End of API Specification**
