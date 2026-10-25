# CasCap.App.Server

The .NET 10 ASP.NET Core host for tutorizr. It runs Signal communications, learner-progress MCP
tools, or both, against external Redis and Agent Runtime services.

## Start Here

The [root local-run example](../../README.md#run-the-learner-tools-locally) starts MCP-only mode
with Redis and without Key Vault or Signal. For full chat, set
`CasCap:FeatureConfig:EnabledFeatures` to `Comms,Mcp` and supply:

- A Signal gateway's REST/gRPC addresses, subscriber name and exact chat/monitor group names.
- An accessible Agent Runtime with the selected agent definition, session and execution identity.
- A runtime MCP connection to this host, with matching Basic credentials and explicit permission
  for the learner-writing tools.

Use TLS outside loopback: this host registers Basic authentication but does not configure HTTPS
redirection or TLS termination itself. All members of the configured group share one learner profile;
there is no per-sender learner isolation.

## Public Surface

| Contract | Responsibility |
| --- | --- |
| [`AppHost`](AppHost.cs) | Configuration, Redis, feature registration, endpoints and application lifetime |
| [`TutorMcpQueryService`](Services/TutorMcpQueryService.cs) | Seven learner tools with structured results |
| [`ILearnerStore`](Abstractions/ILearnerStore.cs) | Learner persistence abstraction |
| [`RedisLearnerStore`](Services/RedisLearnerStore.cs) | Redis hashes and sorted sets for levels, vocabulary and mistakes |
| [`VocabularySchedule`](Services/VocabularySchedule.cs) | Term normalization and Leitner review scheduling |
| [`LearnerProfile`](Models/LearnerProfile.cs), [`VocabularyItem`](Models/VocabularyItem.cs), [`MistakeEntry`](Models/MistakeEntry.cs) | Tool result records |
| [`CefrLevel`](Models/CefrLevel.cs), [`MistakeCategory`](Models/MistakeCategory.cs) | Accepted level and mistake classifications |

## MCP Contract

The configurable MCP path defaults to `/mcp`. It uses stateless Streamable HTTP, requires Basic
authentication and rejects requests carrying an `Origin` header with 403. Only explicitly registered
tool types are exposed; there is no assembly-wide tool scan.

| Tool | Write semantics | Result |
| --- | --- | --- |
| `get_learner_profile` | Read-only | Target/estimated level, vocabulary/due counts and retained mistake count |
| `set_learner_level` | Idempotent write | Updated learner profile |
| `add_vocabulary` | Idempotent write | New or updated term; existing review progress is preserved |
| `get_due_vocabulary` | Read-only | Due terms, oldest due first |
| `record_vocabulary_review` | Non-idempotent write | Updated schedule; call once per answer |
| `record_mistake` | Non-idempotent write | Recorded mistake; call once per distinct mistake |
| `get_recent_mistakes` | Read-only | Newest mistakes, optionally filtered by category |

Both lists default to 10 items and accept 1–50. Required text must be nonblank and at most
500 characters; optional mistake explanations have the same maximum when supplied. An unknown
review term or invalid input fails with an MCP error rather than an empty success.
All seven tools declare non-destructive, closed-world hints and structured content.

New vocabulary starts in box 1 and is immediately due. Correct answers promote one box, capped at
5, and wait 2, 4, 8 or 16 days according to the resulting box. Incorrect answers reset to box 1
and are due after 10 minutes. Normalized term keys are lowercase with whitespace collapsed.
Mistakes retain the newest configured window (500 by default); learner keys have no expiry here.

The store accepts cancellation tokens but its Redis calls do not pass them through. Reviews are
read-modify-write operations, not concurrent-update-safe transactions across the entire tool call;
retrying a non-idempotent tool can record another review or mistake.

When Comms is enabled, [`MessagingMcpQueryService`](https://github.com/f2calv/signalizr/blob/b5ae4f04ff8a6f9f9f611da60a08872a41e34176/src/CasCap.Comms.AI/Services/MessagingMcpQueryService.cs)
adds `create_poll`, `close_poll` and `get_poll_status`. Poll state is process-local and lost on restart.
Tool annotations are metadata, not authorization: this host does not restrict write tools per client.
The external runtime owns its tool allowlist and execution policy.

`/healthz` and tagged `/healthz/{probe}` endpoints remain anonymous regardless of the feature set.
Their availability alone does not prove that a gateway or runtime is reachable.

## Configuration

The local [configuration bootstrap](Extensions/HostApplicationBuilderExtensions.cs) adds the shared
[configuration chain](https://github.com/f2calv/CasCap.Common/blob/main/src/CasCap.Common.Configuration/Extensions/ConfigurationBuilderExtensions.cs):
base JSON, environment JSON, local JSON, environment-local JSON, optional test-local JSON, assembly
User Secrets and environment variables, in that order. JSON is read from the output directory;
the project copies the root base/Development and existing Local files there. User Secrets are
loaded by this chain even outside Development.

Key Vault is added last when enabled. Its selector maps this host's execution-identity prefix into
the runtime-auth section and excludes other applications' runtime identities. Set
`AppConfig:KeyVaultName` to `skip` for the local no-vault path. Environment variables use `__`
in place of `:`.

| Section | Defining contract and host behavior |
| --- | --- |
| `AppConfig` | [`AppConfig`](Models/_AppConfig.cs): MCP path and telemetry; [`AzureAuthConfig`](https://github.com/f2calv/CasCap.Api.Azure/blob/main/src/CasCap.Api.Azure.Auth/Models/_AzureAuthConfig.cs): Key Vault and host Azure credential |
| `CasCap:FeatureConfig` | [`FeatureConfig`](Models/_FeatureConfig.cs): comma-separated, case-insensitive `Comms` and/or `Mcp`; empty or unknown selections fail |
| `CasCap:TutorConfig` | [`TutorConfig`](Models/_TutorConfig.cs): target `B1`, four Redis keys, `MaxMistakes=500` (1–100,000); used by Mcp |
| `CasCap:ApiAuthConfig` | [`ApiAuthConfig`](https://github.com/f2calv/CasCap.Common/blob/main/src/CasCap.Common.Abstractions/Models/_ApiAuthConfig.cs): MCP Basic credentials |
| `CasCap:CachingConfig` | Redis connection required even in MCP-only mode |
| `CasCap:CommsConfig` | [`CommsConfig`](https://github.com/f2calv/signalizr/blob/b5ae4f04ff8a6f9f9f611da60a08872a41e34176/src/CasCap.Comms/Models/_CommsConfig.cs): groups, agent/session selection, communications stream and monitor behavior |
| `CasCap:SignalizrClientConfig` | [`SignalizrClientConfig`](https://github.com/f2calv/signalizr/blob/main/src/CasCap.Signalizr.Client/Models/_SignalizrClientConfig.cs): gateway REST/gRPC addresses and subscriber |
| `CasCap:AgentRuntimeClientOptions` | [`AgentRuntimeClientOptions`](https://github.com/f2calv/agentizr/blob/13aa4a19180b9dbd0f14fa08fbf95f67039b69f0/src/CasCap.AgentRuntime.Client/Models/_AgentRuntimeClientOptions.cs): runtime address and request timeout |
| `CasCap:AgentRuntimeAzureAuthConfig` | [`AgentRuntimeAzureAuthConfig`](https://github.com/f2calv/CasCap.Api.Azure/blob/eaaffef87e366fa62ddc5328dd6f71add9b6397c/src/CasCap.Api.Azure.Auth/Models/_AgentRuntimeAzureAuthConfig.cs): optional Entra authentication for runtime calls |
| `CasCap:SpeechToTextConfig`, `CasCap:TextToSpeechConfig` | [`SpeechToTextConfig`](https://github.com/f2calv/CasCap.Api.Voice/blob/main/src/CasCap.Api.Voice/Models/_SpeechToTextConfig.cs), [`TextToSpeechConfig`](https://github.com/f2calv/CasCap.Api.Voice/blob/main/src/CasCap.Api.Voice/Models/_TextToSpeechConfig.cs): disabled by default |

Do not reuse learner Redis keys across independent deployments. See the
[Comms guide](https://github.com/f2calv/signalizr/blob/b5ae4f04ff8a6f9f9f611da60a08872a41e34176/src/CasCap.Comms/README.md)
for group matching, duplicate suppression and queue behavior, the
[AI responder guide](https://github.com/f2calv/signalizr/blob/b5ae4f04ff8a6f9f9f611da60a08872a41e34176/src/CasCap.Comms.AI/README.md)
for chat commands/session behavior, and the
[voice guide](https://github.com/f2calv/CasCap.Api.Voice/blob/main/src/CasCap.Api.Voice/README.md)
for backend requirements and limits.

## Configuration Examples

The root example is the minimal local MCP configuration. This additional merge fragment selects
full chat and a separate learner-key namespace; it does not supply Basic credentials, runtime
identity, a deployed agent definition or speech backends:

```json
{
  "CasCap": {
    "FeatureConfig": { "EnabledFeatures": "Comms,Mcp" },
    "CommsConfig": {
      "GroupName": "Example Study Group",
      "MonitorGroupName": "Example Monitor Group",
      "AgentName": "ExampleTutor",
      "AgentSessionId": "example-session"
    },
    "SignalizrClientConfig": {
      "BaseAddress": "https://gateway.example.com",
      "GrpcAddress": "https://gateway.example.com:5001",
      "SubscriberName": "example-tutor"
    },
    "AgentRuntimeClientOptions": { "BaseAddress": "https://runtime.example.com" },
    "TutorConfig": {
      "TargetLevel": "B1",
      "ProfileKey": "example:snapshot:profile",
      "VocabularyKey": "example:snapshot:vocabulary",
      "VocabularyDueKey": "example:series:vocabulary-due",
      "MistakesKey": "example:series:mistakes",
      "MaxMistakes": 500
    }
  }
}
```

## Development

This executable targets `net10.0` and is not a NuGet package. Debug/non-Release references use
adjacent `CasCap.Common`, `CasCap.Api.Azure` and `signalizr` projects; their dependency graph also
requires adjacent `CasCap.Api.Voice` and `agentizr` checkouts. Keep those repositories at compatible
revisions. The Debug solution includes those sources.

Release references pin the published shared hosting, caching, configuration, Azure authentication
and Comms AI packages, so the Release build and the root Dockerfile need no adjacent checkouts.

The direct MCP package is `ModelContextProtocol.AspNetCore` `2.3.0`; other package versions are
centralized in [Directory.Packages.props](../../Directory.Packages.props). See the
[test project](../CasCap.App.Server.Tests/README.md) for offline checks and their limits.
