# Irma CLI Documentation

## 1. Overview

Irma CLI is the primary command-line companion for the Irma conversational platform. It is built with .NET and C#, compiled as a **Native AOT (ahead-of-time)** binary named `irma`. Native AOT enables a single, self-contained executable without a runtime dependency, so users can run `irma` directly on supported systems:

```bash
irma --help
```

The CLI is intended to be language-agnostic from a user perspective. Everything documented here applies equally to anyone building their own thin client, whether or not they use .NET internally.

## 2. Design Goals and Use Cases

- **Debugging and diagnostics** of Irma services in development and staging environments.
- **Demonstrations and manual testing** of the conversational experience.
- **Automation** from build servers or monitoring jobs that verify end-to-end availability.
- **Reference implementation** for other client teams (e.g., Android app) to understand expected REST calls and payload shapes.

## 3. Distribution and Runtime Expectations

- Binary name: `irma`.
- Packaging: Native AOT, self-contained for the targeted OS (no global .NET runtime required).
- Expected operating systems: Windows, macOS, and Linux (x64/arm64 builds as needed).
- REST client generation: The Irma Web API publishes `docs/REST-API-spec.yaml`; generate strongly typed clients from this OpenAPI document instead of hand-writing HTTP calls.
- Configuration and cache location: `$HOME/.irma/` (Linux/macOS) or `%USERPROFILE%\.irma\` (Windows). This directory stores login tokens (when allowed), default settings, and session metadata.

## 4. Authentication with Azure Entra ID B2C

Irma CLI authenticates against the Irma Web API using Azure Entra ID B2C-issued access tokens. The CLI should expose a dedicated login experience similar to `az login`.

### 4.1 Commands

- `irma login` – initiates authentication.
- `irma login --device-code` (default) – launches device code flow for interactive users. Steps:
  1. CLI requests a device code using the B2C authority configured in `irma settings`.
  2. User is instructed to open a browser, visit the verification URL, and enter the code.
  3. After sign-in, CLI exchanges the device code for an access token and refresh token.
  4. Tokens are cached in `${IRMA_HOME}/auth.json`.
- `irma login --client-id <id> --client-secret <secret> --tenant <tenant>` – client credentials flow for automation. Tokens are stored only in-memory unless `--persist` is explicitly requested.
- `irma logout` – clears cached tokens.

### 4.2 Token Management

- Tokens are refreshed automatically when allowed by the grant type.
- Expiration is surfaced to the user when manual reauthentication is required.
- The CLI always includes `Authorization: Bearer {token}` when calling the Web API.
- Required scopes: `api://irma/chat.read` and `api://irma/chat.write`.

### 4.3 Failure Modes

- `401 Unauthorized`: token expired or missing scopes. CLI prompts user to re-run `irma login`.
- `400 invalid_grant`: user did not complete device code flow. CLI times out (default 10 minutes) and exits with a non-zero status.
- `403 Forbidden`: current identity lacks access. CLI surfaces message and suggests verifying assignments in Azure Entra ID B2C.

## 5. Global CLI Conventions

- **Invocation shape:** `irma [command] [subcommand] [options]`.
- **Global flags:**
  - `--help`: Show contextual help.
  - `--version`: Print CLI version, Git commit, and target framework.
  - `--json`: Return raw JSON response for commands that normally render formatted output.
  - `--product <id>`: Override the default product identifier for a single command.
- **Default storage:** `irma defaults set` writes to `${IRMA_HOME}/defaults.json`.
- **Exit codes:** `0` for success, non-zero for failures. When possible, map HTTP errors to CLI exit codes (e.g., `3` for validation errors, `4` for authentication errors).
- **Logging:** Verbose logs go to `${IRMA_HOME}/logs/irma.log`. Users can opt in via `IRMA_LOG_LEVEL`.

## 6. Command Reference

### 6.1 `irma --help`

- **Purpose:** Show summary of commands and options.
- **REST usage:** None.
- **Notes:** `irma help <command>` should display detailed usage for subcommands.

### 6.2 `irma --version`

- **Purpose:** Provide CLI version metadata.
- **REST usage:** None by default. Optionally call `GET /version` if the Web API exposes server version for comparison.
- **Output:** Semantic version, Git commit SHA, build timestamp, runtime info.

### 6.3 `irma health`

- **Purpose:** Verify reachability and readiness of the Irma Web API.
- **REST usage:** `GET /v1/irma/healthz`.
- **Payload:** None.
- **Response handling:**
  - `200 OK` → display status, service version, dependency checks.
  - `>= 500` → show error message from API and include `traceId` when available.
- **Documentation:** See `docs/irma-web-api-doc.md` and `docs/REST-API-spec.yaml` for the complete health payload schema.

### 6.4 `irma defaults` Subcommands

Defaults are cached client-side to reduce required command-line arguments.

- `irma defaults set product "Ixx/7.0"`
  - **Purpose:** Persist a default product identifier.
  - **REST usage:** None (local configuration only).
  - **Behavior:** Updates `${IRMA_HOME}/defaults.json`. Future commands use the stored product unless overridden with `--product`.
- `irma defaults list`
  - **Purpose:** Show all persisted defaults.
  - **REST usage:** None.
  - **Output:** Key/value table or JSON when `--json` is specified.
- `irma defaults clear product`
  - **Purpose:** Remove a stored default.
  - **REST usage:** None.

### 6.5 `irma new-conversation`

- **Purpose:** Request a new conversation ID.
- **REST usage:** `POST /v1/irma/conversations`.
- **Request body:**

  ```json
  {
    "product": "Ixx/7.0",
    "additionalContext": []
  }
  ```

- **Response:** `201 Created` with `{ "conversationId": "<guid>", "state": "active", "traceId": "..." }`.
- **Behavior:** The CLI caches the returned `conversationId` in `${IRMA_HOME}/sessions.json` as the current session unless the user opts out with `--no-cache`.

### 6.6 `irma ask`

- **Purpose:** Send a single prompt and print Irma's response.
- **Default mode:** Streaming (`/chatOverStream`).
- **Options:**
  - `--conversation-id <guid>`: Use an existing conversation.
  - `--no-stream`: Force synchronous `/chat`.
  - `--product <id>`: Override product for this request.
  - `--context <path>`: Attach additional context payload from file or STDIN.
  - `--json`: Emit raw API response.
- **REST usage:**
  - Streaming: `POST /v1/irma/conversations/{conversationId}/chatOverStream`
    - Request headers include `Accept: text/event-stream`.
  - Non-streaming: `POST /v1/irma/conversations/{conversationId}/chat`.
- **Request body:**

  ```json
  {
    "message": "What is the thermal resolution of the camera?",
    "product": "Ixx/7.0",
    "additionalContext": []
  }
  ```

- **Response handling:**
  - Streaming: Aggregate chunks unless `--json` is supplied, in which case return the full SSE transcript.
  - Non-streaming: Print `messages[].text` in order, respect formatting (tables, bullet lists).
- **Error paths:** Handle `404 Not Found` for stale conversation IDs, prompting user to run `irma new-conversation`.

### 6.7 `irma chat`

- **Purpose:** Provide an interactive shell that maintains context across turns.
- **Behavior:**
  - On start, ensure a valid conversation ID exists (create one if needed).
  - Each user message is sent via streaming endpoint.
  - `Ctrl+C` exits cleanly, printing the current conversation ID for reference.
  - `/newConversation` command clears the screen, creates a new conversation, and resets context.
  - `/exit` cleanly terminates without relying on SIGINT.
- **REST usage:** `POST /v1/irma/conversations/{conversationId}/chatOverStream` for each turn.
- **JSON mode:** `irma chat --json` writes raw JSON per response chunk to STDOUT (useful for tooling).
- **Transcript storage:** When enabled, save logs to `${IRMA_HOME}/transcripts/<conversationId>.json`.

### 6.8 Proposed Supporting Commands

- `irma conversations current` – Show the active conversation ID and metadata.
- `irma conversations list` – Display cached IDs (locally) along with last-used timestamps.
- `irma conversations close <id>` – Call `DELETE /v1/irma/conversations/{id}` (when API supports it). This is optional but prepares us for future lifecycle management.

## 7. End-to-End Flow

1. **Authenticate:** `irma login` (device code or client credentials).
2. **Configure defaults (optional):** `irma defaults set product "Ixx/7.0"`.
3. **Create conversation:** `irma new-conversation` → stores conversation ID locally.
4. **Engage in chat:**
   - Quick question: `irma ask "..."`.
   - Interactive session: `irma chat`.
5. **Handle errors:**
   - `404`: conversation expired → run `/newConversation`.
   - `401`: token invalid → `irma login`.
   - Network failures → CLI retries with exponential backoff (default 3 attempts).
6. **Automation:** Use `irma ask --json --no-stream` in scripts to capture structured responses.
7. **Cleanup:** `irma logout` (if necessary) and purge cached defaults with `irma defaults clear <key>`.

## 8. Error Handling and Troubleshooting

- **Expired conversation:** API returns `404` with `errorCode = ConversationNotFound`. CLI should surface guidance: _“The conversation has expired. Run `/newConversation` or `irma new-conversation`.”_
- **Policy disengagement:** If the API returns `state = disengagedForRai`, CLI should print a warning and include `traceId`.
- **Validation errors:** `400 Bad Request` typically includes details. In JSON mode the raw payload is emitted; otherwise the CLI formats the errors and provides remediation hints.
- **Timeouts:** Streaming calls should implement a configurable timeout (`IRMA_STREAM_TIMEOUT`). On timeout, CLI informs the user and suggests retrying.
- **Health checks:** `irma health` aids rapid detection of upstream outages. Consider re-running automatically when repeated errors occur.

## 9. Logging and Telemetry

- **Client logs:** Written to `${IRMA_HOME}/logs/irma.log`. Include timestamp, log level, correlation `traceId`, and command context.
- **Server trace correlation:** Every API response contains `traceId`. CLI should log it, expose it via `--json`, and show it in formatted output when errors happen.
- **Verbose mode:** `IRMA_LOG_LEVEL=Debug` enables HTTP request/response dumps (with secrets redacted).

## 10. Automation and CI/CD Guidance

- Use the client credentials login mode in unattended environments.
- Cache tokens securely (Azure Key Vault, GitHub Actions secrets) and supply them via environment variables (`IRMA_CLIENT_ID`, `IRMA_CLIENT_SECRET`, `IRMA_TENANT_ID`).
- Prefer `irma ask --json --no-stream` for deterministic output.
- Combine with `irma health` in monitoring scripts to ensure all services are reachable.

## 11. REST API Mapping

The Irma backend exposes an OpenAPI/Swagger specification at `docs/REST-API-spec.yaml`. Use this as the source for generating REST API clients (e.g., via `dotnet openapi`, NSwag, Autorest) to keep payload contracts in sync with the server.

| CLI Command | HTTP Method & Path | Notes |
|-------------|--------------------|-------|
| `irma --help`, `irma help <cmd>` | – | No API usage. |
| `irma --version` | Optional `GET /v1/irma/version` | Only if server version comparison is desired. |
| `irma login` | `POST https://login.microsoftonline.com/{tenant}/oauth2/v2.0/token` | Device code or client credentials flow via Azure Entra ID B2C. |
| `irma logout` | – | Local cache cleanup only. |
| `irma health` | `GET /v1/irma/healthz` | See `docs/irma-web-api-doc.md` for payload details. |
| `irma defaults set/list/clear` | – | Local configuration; no REST calls. |
| `irma new-conversation` | `POST /v1/irma/conversations` | Creates a new conversation. |
| `irma ask` (streaming) | `POST /v1/irma/conversations/{conversationId}/chatOverStream` | SSE endpoint; default behavior. |
| `irma ask --no-stream` | `POST /v1/irma/conversations/{conversationId}/chat` | Synchronous response. |
| `irma chat` | `POST /v1/irma/conversations/{conversationId}/chatOverStream` | Repeated per turn. |
| `/newConversation` (chat command) | `POST /v1/irma/conversations` | Issued from interactive mode. |

## 12. Open Questions and Follow-Ups

- Define retention policy for cached transcripts and tokens to align with security guidelines.
- Confirm whether the CLI should expose a `DELETE /v1/irma/conversations/{id}` command once the API supports it.
- Decide on default retry/backoff strategy for transient HTTP failures.
- Validate that `Native AOT` builds cover all target platforms (Windows, Linux, macOS) and produce consistent CLI behavior.

---

This document should serve as the canonical reference for how Irma CLI behaves, how it maps to the Irma Web API, and how client developers can interact with the system in both manual and automated scenarios.
