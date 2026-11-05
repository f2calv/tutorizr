# Architecture

This drilldown opens the tutorizr host from the [System Context](../README.md#system-context) into its
containers and components, then follows one learner turn and the vocabulary review lifecycle.
Teaching behaviour lives in the externally supplied Agent Runtime definition, not in this host.

## Containers

One image provides the host. `CasCap:FeatureConfig:EnabledFeatures` selects the `Comms` role, the
`Mcp` role or both; the [chart](../charts/tutorizr/README.md) runs a single replica so only one Comms
subscriber exists. Redis holds the learner state and the communications pipeline's delivery state.

```mermaid
flowchart LR
    classDef container fill:#dbeafe,stroke:#1d4ed8,color:#1e3a8a
    classDef store fill:#e5e7eb,stroke:#4b5563,color:#111827
    classDef person fill:#ede9fe,stroke:#6d28d9,color:#2e1065
    classDef external fill:#fef3c7,stroke:#d97706,color:#78350f

    learners([Learners in a Signal group]):::person <-->|Signal| gateway[Signal gateway<br/>signalizr]:::external
    subgraph tutorizr [tutorizr]
        host[Tutor host<br/>ASP.NET Core; Comms and Mcp roles]:::container
        redis[(Redis<br/>learner state, delivery deduplication)]:::store
        host -->|hashes and sorted sets| redis
    end
    gateway -->|"gRPC deliveries"| host
    host -->|"REST replies"| gateway
    host -->|"streamed agent runs"| runtime[Agent Runtime]:::external
    runtime -->|"MCP tool calls, Basic auth"| host
    host -.->|"optional transcription and speech"| speech[Speech providers]:::external
```

## Host Components

The host owns the learner tools and their store. The communications pipeline and poll tools come
from the `CasCap.Comms.AI` package published by signalizr; this repository only registers them.
`AppHost` is the composition root that wires the components below by feature.

```mermaid
flowchart TB
    classDef component fill:#dbeafe,stroke:#1d4ed8,color:#1e3a8a
    classDef packaged fill:#dcfce7,stroke:#15803d,color:#14532d
    classDef store fill:#e5e7eb,stroke:#4b5563,color:#111827
    classDef external fill:#fef3c7,stroke:#d97706,color:#78350f

    subgraph host [Tutor host]
        mcp[MCP endpoint<br/>Basic auth, /mcp]:::component
        tools[Learner tools<br/>seven MCP tools]:::component
        schedule[Vocabulary schedule<br/>Leitner boxes]:::component
        store[Learner store<br/>ILearnerStore]:::component
        comms[Comms pipeline<br/>ICommsResponder]:::packaged
        polls[Poll tools<br/>Comms role only]:::packaged
        mcp --> tools
        mcp --> polls
        tools --> schedule
        tools -->|ILearnerStore| store
    end
    store --> redis[(Redis)]:::store
    comms -->|deduplication| redis
    comms <-->|"gRPC and REST"| gateway[Signal gateway]:::external
    polls -->|"REST polls"| gateway
    comms -->|"streamed runs"| runtime[Agent Runtime]:::external
    runtime -->|"tool calls"| mcp
```

Blue components are implemented here; green components are registered from the `CasCap.Comms.AI`
package.

## Code Map

| Component | Responsibility | Code location | Provides | Requires |
| --- | --- | --- | --- | --- |
| MCP endpoint | Stateless Streamable HTTP with Basic authentication; rejects browser origins | `src/CasCap.App.Server/AppHost.WebApi.cs`, `AppHost.Endpoints.cs` | `/mcp`, `/healthz` | `ApiAuthConfig` |
| Learner tools | Validates and bounds tool input, returns structured learner results | `Services/TutorMcpQueryService.cs`, `Models/` | Seven `[McpServerTool]` methods | `ILearnerStore`, Vocabulary schedule, `TimeProvider` |
| Vocabulary schedule | Normalizes terms and computes Leitner review transitions | `Services/VocabularySchedule.cs` | `CreateNew`, `Review`, `Normalise` | None |
| Learner store | Persists level, vocabulary and a bounded mistake window | `Abstractions/ILearnerStore.cs`, `Services/RedisLearnerStore.cs` | `ILearnerStore` | Redis |
| Comms pipeline | Subscribes to the chat group, deduplicates, runs agent turns and queues replies | `CasCap.Comms.AI` package, registered in `AppHost.Features.cs` | `ICommsResponder` | Signal gateway, Agent Runtime, Redis |
| Poll tools | Creates and reads chat-group polls for the agent | `CasCap.Comms.AI` package, registered in `AppHost.Features.cs` when `Comms` is enabled | Poll `[McpServerTool]` methods | Signal gateway |

## Runtime Scenarios

### Learner Turn with a Tool Call

The Agent Runtime decides which learner tools to call; the host only persists and returns data.

```mermaid
sequenceDiagram
    autonumber
    actor learner as Learner
    participant gateway as Signal gateway
    participant comms as Comms pipeline
    participant runtime as Agent Runtime
    participant tools as Learner tools
    participant store as Learner store

    learner->>gateway: group message
    gateway->>comms: gRPC delivery
    comms->>runtime: streamed agent run
    runtime->>tools: record_vocabulary_review over MCP
    tools->>store: ILearnerStore.GetVocabularyAsync
    alt term is known
        tools->>store: ILearnerStore.SaveVocabularyAsync
        tools-->>runtime: updated schedule
    else unknown term
        tools-->>runtime: McpException
    end
    runtime-->>comms: response text
    comms->>gateway: REST send
    gateway->>learner: reply in the group
```

## Vocabulary Lifecycle

A term moves through five Leitner boxes. The interval waited after a correct answer depends on the
box the term reaches.

```mermaid
stateDiagram-v2
    [*] --> Box1: add_vocabulary, due now
    Box1 --> Box2: correct, due in 2 days
    Box2 --> Box3: correct, due in 4 days
    Box3 --> Box4: correct, due in 8 days
    Box4 --> Box5: correct, due in 16 days
    Box5 --> Box5: correct, due in 16 days
    Box2 --> Box1: incorrect, due in 10 minutes
    Box3 --> Box1: incorrect, due in 10 minutes
    Box4 --> Box1: incorrect, due in 10 minutes
    Box5 --> Box1: incorrect, due in 10 minutes
    Box1 --> Box1: incorrect, due in 10 minutes
```

## Publication Flow

The [CI workflow](../.github/workflows/ci.yml) validates pull requests without publishing images
or charts. On the default branch it publishes both artifacts before creating the immutable release.
Publication does not install the chart or deploy the host.

```mermaid
flowchart TD
    Source[Pull request or default-branch source] --> Version[Resolve release version]
    Source --> Lint[Repository lint]
    Source --> Helm[Chart lint and schema checks]
    Version --> Build[Release build and offline tests]
    Build --> Image[Build image on every supported architecture]
    Version --> Image
    Image --> Chart[Package and validate umbrella chart]
    Version --> Chart
    Chart --> Mode{Default branch?}
    Mode -->|No| Validation[Pull-request validation only]
    Mode -->|Yes| Published[Image and OCI chart published]
    Lint --> Release[Create missing immutable release tag]
    Helm --> Release
    Published --> Release
```
