# Irma Web API Documentation

This document consolidates the previous `API-and-Azure-AI-Foundry.md` and `REST-API-doc.md` guides. It specifies the REST API for the Irma backend and explains how the service integrates with the agent system hosted in Azure AI Foundry. The API is heavily inspired by the Microsoft 365 Copilot Chat API and provides functionality for creating and managing chat conversations with Irma, an intelligent assistant that can answer questions about your connected devices.

The single point of truth of how the API is and should be implemented can be found in the `REST-API-spec.yaml` file that has the OpenAPI/Swagger specification. These two files should stay in sync; if they diverge, the specification is authoritative.

---

## Table of Contents

1. [Introduction](#1-introduction)
2. [Getting Started](#2-getting-started)
3. [Core Concepts](#3-core-concepts)
4. [Architecture & Azure AI Foundry Integration](#4-architecture--azure-ai-foundry-integration)
    - [4.1 Overall Interaction Architecture](#41-overall-interaction-architecture)
    - [4.2 Streaming Architecture](#42-streaming-architecture)
    - [4.3 Endpoint Implementation Details](#43-endpoint-implementation-details)
    - [4.4 Secure State Management and Authorization](#44-secure-state-management-and-authorization)
    - [4.5 Example Chat Workflow](#45-example-chat-workflow)
5. [API Reference](#5-api-reference)
6. [Best Practices](#6-best-practices)
7. [Appendix](#7-appendix)

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

- Has a unique identifier (`conversationId`)
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

- A unique identifier (`messageId`)
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

## 4. Architecture & Azure AI Foundry Integration

This section describes how the Irma REST API cooperates with Azure AI Foundry to deliver conversations backed by a network of agents. It covers the runtime data flow, streaming considerations, endpoint responsibilities, and the state management strategies required for a reliable integration.

### 4.1 Overall Interaction Architecture

The system is composed of two primary components:

- **Irma REST API**: A .NET 9/10 application that serves as the public-facing entry point for all clients. It handles authentication, request validation, conversation management, and state persistence.
- **Azure AI Foundry**: Hosts the conversational AI logic, which is implemented as a system of interconnected agents. This includes a primary "Routing Agent" and multiple specialized "Product Agents."

#### High-Level Data Flow

The interaction follows a clear, decoupled pattern that now includes a crucial state-management step.

```mermaid
graph TD
    subgraph "Client"
        A[Device App]
    end

    subgraph "Irma Web API"
        B(API Endpoint)
        C{Security & State Check}
        D[Conversation Store<br>(Azure SQL)]
        E(AI Foundry Client)
    end

    subgraph "Azure AI Foundry"
        F[Routing Agent]
    end

    A -- 1. HTTPS Request with conversationId & token --> B;
    B -- 2. conversationId & UserId from token --> C;
    C -- 3. "SELECT FoundryThreadId, UserId WHERE ConversationId = ?" --> D;
    D -- 4. Returns FoundryThreadId & Stored UserId --> C;
    C -- 5. "Does token UserId match stored UserId?" --> C;
    C -- 6. On success, calls with FoundryThreadId --> E;
    E -- 7. Forwards request --> F;
    F -- 8. Returns response --> E;
    E -- 9. Returns response --> B;
    B -- 10. HTTPS Response --> A;

    linkStyle 2 stroke:blue,stroke-width:2px;
    linkStyle 3 stroke:blue,stroke-width:2px;
    linkStyle 4 stroke:red,stroke-width:4px;
```

1.  **Client to API**: A client sends an HTTPS request to an endpoint like `/conversations/{conversationId}/chat`. The request includes a JWT bearer token for authentication.
2.  **API Layer - Authentication**: The .NET API validates the JWT, ensuring the user is authenticated. It extracts a stable `UserId` (e.g., the `oid` claim) from the token.
3.  **API Layer - State Lookup**: The API queries the **Conversation Store** (Azure SQL) using the `conversationId` from the URL to retrieve the stored `UserId` and the internal `FoundryThreadId`.
4.  **API Layer - Authorization**: The API **critically compares** the `UserId` from the token with the `UserId` retrieved from the database. If they do not match, the request is rejected with an `HTTP 404 Not Found`.
5.  **API to AI Foundry**: If authorization succeeds, the API layer translates the incoming request into a call to the Azure AI Foundry's **Routing Agent**, using the retrieved `FoundryThreadId`.
6.  **AI Foundry Internal Routing**: The **Routing Agent** inspects the `product` metadata and forwards the request to the appropriate specialized **Product Agent**.
7.  **Response Generation**: The selected **Product Agent** processes the query and generates a response.
8.  **Response Return**: The response is passed back through the agent system to the API layer.
9.  **API to Client**: The API layer formats the response according to the API specification and sends it back to the client.

### 4.2 Streaming Architecture

Both the synchronous and streaming endpoints interact with the same Azure AI Foundry agent, but in different modes. Understanding how streaming works end-to-end is crucial for implementing a responsive user experience.

#### Agent Streaming Support

Azure AI Foundry agents support **both synchronous and streaming response modes**:

- **Synchronous Mode** (used by `/chat`): The agent processes the complete response before returning it as a single payload.
- **Streaming Mode** (used by `/chatOverStream`): The agent generates response chunks progressively and sends them as they become available.

**Key Point**: Both endpoints call the same agent API, but with a different `stream` parameter:

```csharp
// Synchronous call (for /chat endpoint)
var response = await agentClient.GetResponseAsync(
    threadId: foundryThreadId,
    message: userMessage,
    metadata: new { product = "Ixx/1.0" },
    stream: false  // Wait for complete response
);

// Streaming call (for /chatOverStream endpoint)
var responseStream = await agentClient.GetResponseAsync(
    threadId: foundryThreadId,
    message: userMessage,
    metadata: new { product = "Ixx/1.0" },
    stream: true  // Receive chunks progressively
);
```

#### End-to-End Streaming Pipeline

When a client calls `/chatOverStream`, the data flows through a real-time pipeline:

```
AI Foundry Agent → (chunk 1) → .NET API → (SSE event 1) → Android Client
                 → (chunk 2) → .NET API → (SSE event 2) → Android Client
                 → (chunk 3) → .NET API → (SSE event 3) → Android Client
                 → (complete) → .NET API → (end event)  → Android Client
```

**No Buffering**: The .NET API layer acts as a **streaming proxy**. Each chunk from the AI Foundry agent is immediately wrapped in an SSE event and forwarded to the client. The API does not wait for the complete response.

#### Benefits of Streaming

1. **Lower Perceived Latency**: Users see the first words of the response in 1-2 seconds instead of waiting 10+ seconds for a complete answer.
2. **Progressive Rendering**: The client can display text as it arrives, creating a more engaging "typing" effect.
3. **Better Resource Utilization**: No need to buffer large responses in memory at the API layer.
4. **Natural Backpressure**: If the client is slow to consume data, the stream naturally slows down, preventing memory issues.

#### Implementation Pattern for Streaming

Here's how the `/chatOverStream` endpoint forwards chunks in real-time:

```csharp
[HttpPost("conversations/{conversationId}/chatOverStream")]
public async Task ChatOverStream(string conversationId, [FromBody] ChatRequest request)
{
    // 1. Validate authentication and retrieve Foundry threadId
    var conversation = await GetAndValidateConversation(conversationId);
    
    // 2. Set up Server-Sent Events response
    Response.ContentType = "text/event-stream";
    Response.Headers.Add("Cache-Control", "no-cache");
    Response.Headers.Add("Connection", "keep-alive");
    
    try
    {
        // 3. Call AI Foundry agent in streaming mode
        var agentStream = await _agentClient.GetResponseAsync(
            threadId: conversation.FoundryThreadId,
            message: request.Message,
            metadata: new { product = request.Product },
            stream: true  // Enable streaming
        );
        
        // 4. Forward chunks as they arrive
        await foreach (var chunk in agentStream)
        {
            var sseEvent = new {
                id = conversationId,
                messages = new[] {
                    new {
                        id = chunk.MessageId,
                        text = chunk.Delta,  // Incremental text fragment
                        createdDateTime = DateTime.UtcNow
                    }
                }
            };
            
            // Immediately write to response stream
            await Response.WriteAsync($"data: {JsonSerializer.Serialize(sseEvent)}\\n\\n");
            await Response.Body.FlushAsync();  // Force immediate send
        }
        
        // 5. Send completion event
        await Response.WriteAsync($"event: end\\ndata: {JsonSerializer.Serialize(new { conversationId = conversationId, messages = Array.Empty<object>() })}\\n\\n");
    }
    catch (Exception ex)
    {
        // 6. Send error event if stream fails
        await Response.WriteAsync($"event: error\\ndata: {JsonSerializer.Serialize(new { code = \"InternalError\", message = ex.Message })}\\n\\n");
    }
}
```

#### Synchronous vs. Streaming: Same Agent, Different Modes

| Aspect | `/chat` (Synchronous) | `/chatOverStream` (Streaming) |
|--------|----------------------|------------------------------|
| Agent Call | Same agent, `stream: false` | Same agent, `stream: true` |
| API Behavior | Waits for complete response | Forwards chunks immediately |
| Response Format | Single JSON object | Server-Sent Events (SSE) |
| Client Perception | Higher latency, all-at-once | Lower latency, progressive |
| Use Case | Batch processing, simple UIs | Interactive chat, real-time UIs |
| Memory Usage | Must buffer full response | Minimal buffering |

#### Error Handling in Streaming

Errors can occur at two stages:

**Before Stream Establishment:**
- Standard HTTP error response (401, 403, 404, etc.)
- Connection never upgrades to SSE

**During Streaming:**
- An `event: error` is sent with error details
- Connection is closed after the error event
- Client must handle the error and potentially retry or create a new conversation

#### Heartbeat Mechanism

To prevent connection timeouts during long pauses in agent generation, the API should send periodic heartbeat events:

```csharp
// Send keepalive every 15 seconds during long waits
await Response.WriteAsync("event: keepalive\\ndata: {}\\n\\n");
await Response.Body.FlushAsync();
```

Clients should ignore these events and not treat them as content.

### 4.3 Endpoint Implementation Details

This section describes the high-level logic for each REST API endpoint.

#### `POST /conversations`

This endpoint initiates a new chat session.

1.  **Authentication/Authorization**: Validate the incoming JWT bearer token. The token must be valid and contain the `api://irma/chat.write` scope.
2.  **Create Conversation State**:
    -   The API generates a new, unique `conversationId` (UUID).
    -   It calls the AI Foundry to create a new "thread" and receives a `FoundryThreadId`.
    -   It persists a new record in the Conversation Store, mapping the `conversationId`, `FoundryThreadId`, and the user's `UserId` (from the JWT).
3.  **Response**: Return a `201 Created` response with the newly created `Conversation` object, including the `conversationId`.

#### `POST /conversations/{conversationId}/chat` (Synchronous)

This endpoint sends a message in an existing conversation.

1.  **Authentication/Authorization**: Validate the JWT and required `api://irma/chat.write` scope.
2.  **State Retrieval and Validation**:
    -   Retrieve the conversation record from the Conversation Store using the `conversationId`.
    -   **Security Check**: Verify that the `UserId` from the store matches the `UserId` from the current JWT. If not, return `404 Not Found`.
    -   Retrieve the corresponding `FoundryThreadId`.
3.  **Call AI Foundry**:
    -   Construct a request to the **Routing Agent** in Azure AI Foundry, including the `FoundryThreadId`.
4.  **Response Handling**:
    -   Await the complete response from the AI Foundry.
    -   Update state (e.g., `turnCount`) and format the response.
    -   Return a `200 OK` with the JSON payload.

#### `POST /conversations/{conversationId}/chatOverStream` (Streaming)

This endpoint sends a message and streams the response in real-time.

1.  **Authentication and Validation**: Same as the synchronous endpoint, including the critical security check to match the `UserId`.
2.  **Establish SSE Connection**: Set the response `Content-Type` to `text/event-stream`.
3.  **Call AI Foundry (Streaming)**:
    -   Make a streaming call to the **Routing Agent** with `stream: true`, using the `FoundryThreadId`.
4.  **Stream Processing and Real-Time Forwarding**:
    -   The API acts as a non-buffering proxy, immediately forwarding data chunks from the agent to the client, wrapped in the SSE format.

### 4.4 Secure State Management and Authorization

Properly managing conversation state is critical for both functionality and security. The most significant security risk in a multi-user chat system is one user gaining access to another user's conversation. This section outlines the architecture designed to prevent this.

#### 4.4.1 The Core Problem: Conversation Hijacking

A naive implementation might trust a `threadId` sent from the client. As noted in the Azure AI Foundry Baseline sample, this is a major security flaw:

> "// TODO: [security] Do not trust client to provide threadId. Instead map current user to their active threadid in your application's own state store. Without this security control in place, a user can inject messages into another user's thread."

Our architecture directly addresses this by never trusting the client with internal state identifiers and by binding every conversation to a specific user.

#### 4.4.2 The Solution: A Persistent Conversation Store

The Irma Web API layer is responsible for managing the mapping between the public-facing `conversationId`, the internal Azure AI Foundry `threadId`, and the authenticated user's identity (`UserId`). This is achieved using a persistent data store, referred to as the "Conversation Store."

**Pluggable Architecture:**
To ensure flexibility, the Conversation Store is implemented behind an interface (e.g., `IConversationStore`). This allows the underlying data storage technology to be swapped without changing the core application logic. The initial and recommended implementation will use **Azure SQL Database**.

**Data Model:**
The store will maintain a simple record for each conversation with the following schema:

| Column | Type | Description |
| --- | --- | --- |
| `ConversationId` (PK) | `string` (UUID) | The public, unique identifier for the conversation. |
| `FoundryThreadId` | `string` | The internal identifier for the corresponding thread in Azure AI Foundry. |
| `UserId` | `string` | The stable, unique identifier for the user (from the JWT `sub` or `oid` claim). |
| `CreatedDateTime` | `datetime` | Timestamp of when the conversation was created. |
| `State` | `string` | The current state of the conversation (e.g., `active`). |

#### 4.4.3 Technology Choice: Azure SQL over Alternatives

- **Why Azure SQL?**
  - **Data Integrity:** As a relational database, Azure SQL enforces the uniqueness of `ConversationId` via a primary key, preventing data corruption.
  - **Query Flexibility:** While the primary lookup is by `ConversationId`, SQL allows for efficient administrative queries, such as finding all conversations for a user.
  - **Maturity and Tooling:** The .NET ecosystem has excellent support for SQL databases through Entity Framework Core, which simplifies development, migrations, and testing.
- **Why Not Azure Table Storage?** While simple for key-value lookups, Table Storage lacks the strong data integrity guarantees and flexible querying capabilities of Azure SQL, making it less suitable for this relational mapping.
- **Why Not a Cache (e.g., Redis)?** A caching layer is not necessary initially. Lookups on an indexed primary key in Azure SQL are extremely fast (typically single-digit milliseconds) and will not be a performance bottleneck. Adding a cache would introduce unnecessary complexity at this stage.

#### 4.4.4 Secure Authorization Flow

This flow is executed on **every** request to an existing conversation (e.g., `POST /conversations/{conversationId}/chat`).

1.  **Authenticate:** The API validates the incoming JWT bearer token.
2.  **Extract `UserId`:** A stable user identifier (e.g., the `oid` claim) is extracted from the validated token.
3.  **Lookup Conversation:** The API queries the Conversation Store using the `conversationId` from the URL.
    ```sql
    SELECT FoundryThreadId, UserId FROM Conversations WHERE ConversationId = @conversationId
    ```
4.  **Authorize:**
  - **If no record is found:** The `conversationId` is invalid. The API **must** return an `HTTP 404 Not Found` response.
  - **If a record is found:** The API **must** compare the `UserId` from the database record with the `UserId` extracted from the token.
    - **If they match:** The user is authorized. The request proceeds using the retrieved `FoundryThreadId`.
    - **If they do not match:** This is an authorization failure. The API **must** return an `HTTP 404 Not Found` to avoid revealing that the conversation ID is valid but belongs to another user.This ensures that even if a `conversationId` is leaked, it is useless without a valid JWT for the user who created it.

#### 4.4.5 Handling De-synchronized State (Lost Foundry Thread)

A critical edge case is when the AI Foundry thread is deleted or lost, but the conversation record still exists in the Irma API's database.

**Recommended Implementation:**

1.  The API calls the AI Foundry with the stored `FoundryThreadId`.
2.  The AI Foundry returns an error indicating the thread was not found.
3.  The Irma API should then return an `HTTP 409 Conflict` error to the client.
4.  The error response body should clearly state that the conversation has expired and a new one must be created.

```json
{
  "code": "Conflict",
  "message": "The conversation context has expired or been lost. Please start a new conversation.",
  "target": "conversationId",
  "traceId": "..."
}
```

This approach forces the client to handle the error and guide the user to start a new conversation, which is the correct way to manage the loss of state.

### 4.5 Example Chat Workflow

This workflow demonstrates a complete interaction, from creating a conversation to sending a message.

**Actors:**
- **Device App**: The client application.
- **Irma API**: The .NET REST API.
- **AI Foundry**: The agent system.

#### Step 1: Device App Initiates a New Conversation

The user opens their device app and wants to start a chat.

1.  **Device App -> Irma API**: The app sends a request to create a conversation.
    ```http
    POST /v1/irma/conversations HTTP/1.1
    Host: api.irma.example.com
    Authorization: Bearer {jwt_for_user_A}
    Content-Type: application/json

    {}
    ```
2.  **Irma API (Internal Logic)**:
    -   Validates the JWT for User A and extracts `user_A_id`.
    -   Calls the AI Foundry to create a new thread, receiving `foundry-thread-123` back.
    -   Generates a new `conversationId`: `conv-abc-456`.
    -   Stores the mapping in the Conversation Store: `(conv-abc-456, foundry-thread-123, user_A_id)`.
3.  **Irma API -> Device App**: The API returns the new conversation details.
    ```http
    HTTP/1.1 201 Created
    Content-Type: application/json

    {
      "conversationId": "conv-abc-456",
      "createdDateTime": "2025-10-29T10:00:00.000Z",
      "state": "active",
      "turnCount": 0
    }
    ```

#### Step 2: User Sends a Message

The user asks a question about their "Ixx/1.0" camera.

1.  **Device App -> Irma API**: The app sends the message to the chat endpoint using the `conversationId`.
    ```http
    POST /v1/irma/conversations/conv-abc-456/chat HTTP/1.1
    Host: api.irma.example.com
    Authorization: Bearer {jwt_for_user_A}
    Content-Type: application/json

    {
      "message": "Is the temperature reading normal?",
      "additionalContext": [
        { "text": "Current temperature: 42°C" }
      ],
      "product": "Ixx/1.0"
    }
    ```
2.  **Irma API (Internal Logic)**:
    -   Validates the JWT for User A and extracts `user_A_id`.
    -   Looks up `conv-abc-456` in the Conversation Store.
    -   It finds the record and confirms the owner is `user_A_id`, which matches the token.
    -   It retrieves the `FoundryThreadId`: `foundry-thread-123`.
    -   It calls the AI Foundry's Routing Agent, passing the thread ID, message, context, and product metadata.
3.  **AI Foundry (Internal Logic)**:
    - The Routing Agent receives the request for `foundry-thread-123`.
    - It inspects the metadata and sees `"product": "Ixx/1.0"`.
    - It routes the request to the **Ixx Product Agent**.
    - The Ixx agent processes the question ("Is 42°C normal?") and generates a response.
4.  **AI Foundry -> Irma API**: The Foundry returns the complete response text: "A temperature of 42°C is above the normal operating range of 20-35°C. You should check the device for proper ventilation."
5.  **Irma API -> Device App**: The API formats the final response and sends it back.
    ```http
    HTTP/1.1 200 OK
    Content-Type: application/json

    {
      "conversationId": "conv-abc-456",
      "turnCount": 1,
      "messages": [
        { "messageId": "msg-1", "text": "Is the temperature reading normal?", ... },
        { "messageId": "msg-2", "text": "A temperature of 42°C is above the normal operating range...", ... }
      ],
      ...
    }
    ```

#### Step 3: Conversation Ends

The user closes the chat window.

-   **No Explicit End**: The conversation is implicitly "over." There is no `DELETE` or `END` endpoint.
-   **State**: The conversation `conv-abc-456` remains `active` in the database. If the user returns later, they can continue the conversation, and the context will be preserved in the AI Foundry via `foundry-thread-123`.
-   **Cleanup (Optional)**: A background job could be implemented to archive or delete conversations that have been inactive for an extended period (e.g., > 30 days) to manage resources in both the API database and the AI Foundry.

---

## 5. API Reference

### 5.1 Error Handling

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

### 5.2 Endpoints

#### 5.2.1 Create Conversation

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
| `conversationId` | String | The unique identifier for the conversation. Use this in subsequent chat requests. |
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
  "conversationId": "0d110e7e-2b7e-4270-a899-fd2af6fde333",
  "createdDateTime": "2025-10-29T10:00:00.000Z",
  "displayName": "",
  "state": "active",
  "turnCount": 0
}
```

---

#### 5.2.2 Chat (Synchronous)

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
| `conversationId` | String | The unique identifier for the conversation. |
| `createdDateTime` | String | The date and time the conversation was created, in UTC. |
| `displayName` | String | The display name of the conversation (auto-generated from first message). |
| `state` | String | The state of the conversation. |
| `turnCount` | Integer | The number of turns in the conversation. |
| `messages` | Array | An array of message objects representing the full conversation history. |

**Message Object Schema:**

| Property | Type | Description |
| --- | --- | --- |
| `messageId` | String | The unique identifier for the message. |
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
  "conversationId": "0d110e7e-2b7e-4270-a899-fd2af6fde333",
  "createdDateTime": "2025-10-29T10:00:00.000Z",
  "displayName": "Weather in Stockholm",
  "state": "active",
  "turnCount": 1,
  "messages": [
    {
      "messageId": "cc211f56-1a5e-0af0-fec2-c354ce468b95",
      "text": "What is the weather like in Stockholm?",
      "createdDateTime": "2025-10-29T10:05:00.000Z"
    },
    {
      "messageId": "3fe6b260-c682-4f8e-a201-022ccb300742",
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

#### 5.2.3 Chat Over Stream (SSE)

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

Same as the synchronous chat endpoint. See [Chat (Synchronous)](#522-chat-synchronous) for details.

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
| `conversationId` | String | The unique identifier for the conversation. |
| `messages` | Array | An array containing one or more message objects with text chunks. |

**Message Object in Stream:**

| Property | Type | Description |
| --- | --- | --- |
| `messageId` | String | The unique identifier for the message (consistent across chunks). |
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
  "conversationId": "0d110e7e-2b7e-4270-a899-fd2af6fde333",
  "messages": [
    {
      "messageId": "3fe6b260-c682-4f8e-a201-022ccb300742",
      "text": "The weather in Stockholm is sunny",
      "createdDateTime": "2025-10-29T10:05:05.000Z"
    }
  ]
}

data: {
  "conversationId": "0d110e7e-2b7e-4270-a899-fd2af6fde333",
  "messages": [
    {
      "messageId": "3fe6b260-c682-4f8e-a201-022ccb300742",
      "text": " with a temperature of 15°C.",
      "createdDateTime": "2025-10-29T10:05:05.000Z"
    }
  ]
}

event: end
data: {
  "conversationId": "0d110e7e-2b7e-4270-a899-fd2af6fde333",
  "messages": []
}

```

*Example with Heartbeat and Error:*

```http
HTTP/1.1 200 OK
Content-Type: text/event-stream

data: {
  "conversationId": "0d110e7e-2b7e-4270-a899-fd2af6fde333",
  "messages": [
    {
      "messageId": "3fe6b260-c682-4f8e-a201-022ccb300742",
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

### 5.3 Data Models

#### 5.3.1 Conversation

Represents a chat conversation with Irma.

```json
{
  "conversationId": "string",
  "createdDateTime": "string (ISO 8601)",
  "displayName": "string",
  "state": "active | disengagedForRai",
  "turnCount": 0
}
```

#### 5.3.2 Message

Represents a single message in a conversation.

```json
{
  "messageId": "string",
  "text": "string",
  "createdDateTime": "string (ISO 8601)"
}
```

#### 5.3.3 Context Message

Represents additional context for a conversation.

```json
{
  "text": "string",
  "description": "string (optional)"
}
```

#### 5.3.4 Error

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

## 6. Best Practices

### 6.1 Error Handling

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

### 6.2 Security

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

### 6.3 Streaming Best Practices

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

### 6.4 Performance

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

### 6.5 Observability

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

## 7. Appendix

### 7.1 Changelog

#### v1 (2025-10-29)
- Initial release
- Added conversation creation endpoint
- Added synchronous chat endpoint
- Added streaming chat endpoint
- Azure Entra ID and B2C authentication support

### 7.2 Support

**Reporting Issues:**

When reporting issues, please include:
- The `traceId` from the error response
- Request timestamp
- HTTP status code
- Request payload (with sensitive data redacted)

**Contact:**

- Email: support@irma.example.com
- Documentation: https://docs.irma.example.com

### 7.3 Future Enhancements

The following features are under consideration for future API versions:

- **List Conversations:** `GET /conversations` to retrieve all user conversations
- **Get Conversation:** `GET /conversations/{conversationId}` to retrieve specific conversation details
- **Delete Conversation:** `DELETE /conversations/{conversationId}` to remove conversations
- **Conversation Search:** Search within conversation history
- **Webhooks:** Proactive notifications for device events
- **Batch Operations:** Process multiple messages in a single request

### 7.4 Related Resources

- [Microsoft 365 Copilot Chat API Overview](https://learn.microsoft.com/en-us/microsoft-365-copilot/extensibility/api/ai-services/chat/overview)
- [Azure Entra ID Documentation](https://learn.microsoft.com/en-us/entra/identity/)
- [Server-Sent Events Specification](https://html.spec.whatwg.org/multipage/server-sent-events.html)
- [OAuth 2.0 RFC](https://datatracker.ietf.org/doc/html/rfc6749)

---

**End of API Specification**
