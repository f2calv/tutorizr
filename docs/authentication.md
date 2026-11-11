# Authentication

tutorizr has three authenticated connections: the live voice client signs a learner in and calls the
Agentizr runtime for them, the tutor host calls the runtime as itself, and the runtime calls the
host's learner tools. This page covers the tutorizr side of each. The runtime side, including how a
token is mapped to a tenant and which permission each endpoint needs, is described in
[Agentizr's authentication guide](https://github.com/f2calv/agentizr/blob/main/docs/authentication.md).
Every identifier on this page is a placeholder.

## Overview

```mermaid
flowchart LR
    classDef container fill:#dbeafe,stroke:#1d4ed8,color:#1e3a8a
    classDef person fill:#ede9fe,stroke:#6d28d9,color:#2e1065
    classDef external fill:#fef3c7,stroke:#d97706,color:#78350f

    learner([Learner]):::person
    subgraph tutorizr [tutorizr]
        client[Live voice client<br/>.NET MAUI]:::container
        host[Tutor host<br/>Comms and Mcp roles]:::container
    end
    entra[Microsoft Entra ID]:::external
    runtime[Agentizr runtime]:::external
    realtime[Azure OpenAI realtime model]:::external

    learner -->|"signs in once in the system browser"| client
    client -->|"interactive or silent sign-in"| entra
    client -->|"delegated token, agent.realtime"| runtime
    host -->|"client credentials, certificate"| entra
    host -->|"app token, agent.execute"| runtime
    runtime -->|"Basic auth on /mcp"| host
    client <-->|"WebRTC media, no token"| realtime
```

| Connection | Who authenticates | Credential | Held by |
| --- | --- | --- | --- |
| Voice client to runtime | The learner | Delegated access token for `agent.realtime` | MSAL token cache in the client process |
| Tutor host to runtime | The host application | Access token for the `agent.execute` application role, from a certificate | The host's secret store |
| Runtime to tutor host | The runtime | Basic credentials on `/mcp` | The host's `ApiAuthConfig` and the runtime's tenant MCP credential |

## Voice Client Sign-In

The client is a public client: it holds no secret, so the learner's sign-in is what authorizes it.
[`MsalAccessTokenProvider`](../src/CasCap.App.Maui/Services/MsalAccessTokenProvider.cs) requests only
the delegated `agent.realtime` scope of the runtime API. It opens the system browser rather than an
embedded web view and uses no authentication broker. The browser returns to the app on the
`http://localhost` loopback redirect on Windows and on `io.github.f2calv.tutorizr://auth`, the
application ID, on Android.
[`BearerTokenHandler`](../src/CasCap.App.Maui/Services/BearerTokenHandler.cs) attaches the token to
every request the typed Agentizr client sends.

```mermaid
sequenceDiagram
    autonumber
    actor Learner
    participant Page as Conversation page
    participant Msal as MsalAccessTokenProvider
    participant Browser as System browser
    participant Entra as Microsoft Entra ID
    participant Runtime as Agentizr runtime

    Learner->>Page: Start
    Page->>Msal: Token for the call request
    Msal->>Msal: Silent acquisition from the token cache
    alt No cached account or interaction required
        Msal->>Browser: Open authorize URL with the platform redirect
        Learner->>Browser: Sign in and consent
        Browser->>Entra: Credentials
        Entra-->>Msal: Authorization code on the redirect URI
        Msal->>Entra: Redeem code with PKCE
    end
    Entra-->>Msal: Delegated access token
    Msal-->>Page: Bearer token
    Page->>Runtime: Start call with SDP offer, Authorization: Bearer token
    Runtime-->>Page: SDP answer and call ID
    Note over Page: Media flows directly to Azure OpenAI over WebRTC
    Page->>Runtime: End call, Authorization: Bearer token
    Runtime-->>Page: Call summary
```

Only one interactive sign-in runs at a time; requests that wait for it retry the silent path first.
On Android MSAL keeps the token cache in app-private storage; on Windows the cache lives in memory, so
restarting the client prompts again.

The token identifies the learner and the client registration. The runtime maps the registration's
client ID to the tutor's tenant and applies the tutor definition; the client never sends a tenant,
instructions or tools, and never receives a provider key, the ephemeral session secret or the
provider's call ID.

### Client Configuration

[`TutorClientConfig`](../src/CasCap.App.Maui/Models/_TutorClientConfig.cs) and the runtime address are
validated when the first call starts. On Windows they are read from .NET user secrets or
`tutorizr__`-prefixed environment variables. On Android the learner enters them on the Settings tab,
which stores them in app preferences, so an APK built in public CI carries none of them. None of these
values is a secret, but they identify a directory and a registration, so keep them out of tracked files.

| Key | Purpose |
| --- | --- |
| `TutorClientConfig:TenantId` | Directory of the public client registration |
| `TutorClientConfig:ClientId` | Public client registration ID |
| `TutorClientConfig:ApiScope` | Delegated scope, for example `api://00000000-0000-0000-0000-000000000000/agent.realtime` |
| `TutorClientConfig:AgentName` | Agent whose definition offers realtime calls |
| `CasCap:AgentRuntimeClientOptions:BaseAddress` | Runtime base address, for example `https://runtime.example.com/` |

On Windows:

```powershell
dotnet user-secrets --project src/CasCap.App.Maui set "TutorClientConfig:TenantId" "00000000-0000-0000-0000-000000000000"
dotnet user-secrets --project src/CasCap.App.Maui set "TutorClientConfig:ClientId" "00000000-0000-0000-0000-000000000000"
dotnet user-secrets --project src/CasCap.App.Maui set "TutorClientConfig:ApiScope" "api://00000000-0000-0000-0000-000000000000/agent.realtime"
dotnet user-secrets --project src/CasCap.App.Maui set "TutorClientConfig:AgentName" "TutorAgent"
dotnet user-secrets --project src/CasCap.App.Maui set "CasCap:AgentRuntimeClientOptions:BaseAddress" "https://runtime.example.com/"
```

The public client registration needs a mobile and desktop platform with the `http://localhost` and
`io.github.f2calv.tutorizr://auth` redirect URIs, the delegated `agent.realtime` permission on the
runtime API, and its client ID added to the tutor tenant's caller list in the runtime.

A self-hosted runtime behind a private certificate authority needs that authority installed on the
phone as a user certificate. The Android app trusts user-installed authorities as well as system
ones so it can reach such a runtime; it accepts no cleartext traffic.

## Tutor Host To Runtime

When the `Comms` role relays a chat message, the host calls the runtime as itself through
`AddAgentRuntimeAzureAuthentication`. It uses the client credentials flow with a certificate from its
own app registration, which holds only the `agent.execute` application role. The settings bind from
`CasCap:AgentRuntimeAzureAuthConfig` (`Enabled`, `TenantId`, `ClientId`, `Certificate` and `Scope`);
tracked configuration leaves them disabled and empty, and a deployment supplies them from its secret
store.

```mermaid
sequenceDiagram
    autonumber
    participant Gateway as Signal gateway
    participant Host as Tutor host, Comms role
    participant Entra as Microsoft Entra ID
    participant Runtime as Agentizr runtime

    Gateway->>Host: Chat message delivery
    Host->>Entra: Client credentials signed with the host certificate<br/>scope: runtime API /.default
    Entra-->>Host: Access token, roles = agent.execute
    Host->>Runtime: Agent run, Authorization: Bearer token
    Runtime-->>Host: Streamed reply
    Host->>Gateway: Reply to the group
```

## Runtime To Tutor Host

The `Mcp` role exposes the learner tools at `/mcp`. The endpoint requires Basic authentication from
`CasCap:ApiAuthConfig` and rejects any request with an `Origin` header with `403`, because the
runtime sends none and a browser always does. The runtime holds the matching credential as a
tenant MCP credential and sends it only on calls to this host, both for chat runs and for realtime
call tools.

```mermaid
sequenceDiagram
    autonumber
    participant Runtime as Agentizr runtime
    participant Mcp as Tutor host /mcp
    participant Tools as Learner tools

    Runtime->>Mcp: Tool call, Authorization: Basic credentials
    alt Credentials invalid
        Mcp-->>Runtime: 401 Unauthorized
    else Origin header present
        Mcp-->>Runtime: 403 Forbidden
    else Authorized
        Mcp->>Tools: Invoke tool
        Tools-->>Mcp: Structured result
        Mcp-->>Runtime: Tool result
    end
```

The tools trust the runtime to have authorized the learner or the chat group; they never log
learner content, and they bound list sizes and text lengths themselves.
