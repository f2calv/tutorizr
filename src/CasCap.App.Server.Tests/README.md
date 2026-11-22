# CasCap.App.Server.Tests

Credential-free .NET 11 tests for the tutorizr host, using xUnit v3 on
Microsoft.Testing.Platform. The tool and scheduling tests use an in-memory store without a running
web host, Redis instance, Signal gateway or Agentizr runtime. The separate host-startup test loads
application configuration and requires an isolated environment before execution. MCP integration
tests use a temporary loopback host, synthetic Basic credentials and an offline Redis command fixture.

## Tests

| Class | Methods | Cases | Trait |
| --- | --- | --- | --- |
| [`AppHostTests`](Tests/Unit/AppHostTests.cs) | 1 | 1 | `Hosting` |
| [`VocabularyScheduleTests`](Tests/Unit/VocabularyScheduleTests.cs) | 4 | 9 | `Scheduling` |
| [`TutorMcpQueryServiceTests`](Tests/Unit/TutorMcpQueryServiceTests.cs) | 8 | 10 | `Tools` |
| [`TutorMcpToolAnnotationTests`](Tests/Unit/TutorMcpToolAnnotationTests.cs) | 2 | 2 | `Tools` |
| [`TutorClientConfigTests`](Tests/Unit/TutorClientConfigTests.cs) | 2 | 6 | `Authentication` |
| [`LearnerMcpContextTests`](Tests/Unit/LearnerMcpContextTests.cs) | 3 | 7 | `Authentication` |
| [`LearnerMcpPersistenceTests`](Tests/Integration/LearnerMcpPersistenceTests.cs) | 2 | 2 | `Integration` |
| **Total** | **22** | **37** | |

These are source-declared counts, not execution results. Eight theories expand through 23
`InlineData` rows; the other fourteen methods are facts. There are no member/class data providers.

## Traits

| Category | Covers |
| --- | --- |
| `Hosting` | Unexpected startup failures return a non-success process result |
| `Authentication` | Device scope parsing and learner-context authentication, header and key-pattern validation |
| `Integration` | Fresh real MCP clients, per-login Redis command isolation and authentication failure without learner access |
| `Scheduling` | Leitner box promotion, reset and term normalisation |
| `Tools` | MCP tool behaviour against an in-memory store, and pinned tool hints |

## Skipped Tests

None: no skip attributes, conditional skip branches or credential gates are declared.

## Running

Use a stable .NET 11 SDK. Debug builds use the adjacent-source setup in the
[host development guide](../CasCap.App.Server/README.md#development). The project reference compiles
the host. From the repository root, this explicit selection runs the 21 in-memory tool, annotation
and scheduling cases without invoking host startup:

```powershell
dotnet test --project .\src\CasCap.App.Server.Tests\CasCap.App.Server.Tests.csproj --configuration Debug --filter-class CasCap.Tests.Unit.TutorMcpQueryServiceTests CasCap.Tests.Unit.TutorMcpToolAnnotationTests CasCap.Tests.Unit.VocabularyScheduleTests --minimum-expected-tests 21
```

For synthetic login-owned MCP persistence and its directly related regressions:

```powershell
dotnet test --project .\src\CasCap.App.Server.Tests\CasCap.App.Server.Tests.csproj --configuration Debug --filter-class CasCap.Tests.Integration.LearnerMcpPersistenceTests CasCap.Tests.Unit.LearnerMcpContextTests CasCap.Tests.Unit.TutorMcpQueryServiceTests CasCap.Tests.Unit.TutorMcpToolAnnotationTests --minimum-expected-tests 21
```

`global.json` selects Microsoft.Testing.Platform. The direct test dependencies are `xunit.v3`
`4.0.3` and `Microsoft.Testing.Extensions.CodeCoverage` `18.12.0`.
No cloud credentials, emulator or external services are needed. The project removes copied
`appsettings.Local*.json` files from its output; the selected in-memory fixtures do not load
application configuration. `AppHostTests` is not part of this selection: it invokes `AppHost.RunAsync`
and can reach configuration providers before rejecting the invalid cache setting.

## Coverage Boundaries

[`InMemoryLearnerStore`](Tests/InMemoryLearnerStore.cs) replaces persistence and
[`ManualTimeProvider`](Tests/ManualTimeProvider.cs) advances time deterministically. Tests cover term
normalization, due dates, review promotion/reset, vocabulary updates, mistake retention/filtering,
selected invalid inputs and the seven learner-tool annotations.

The MCP integration fixture exercises actual discovery, tool invocation and structured content
across fresh client connections. It checks correction fields, separate-login profile/vocabulary/mistake
isolation, missing identity and unauthenticated requests. Its shared Redis command fixture runs the
real `RedisLearnerStore`, but is not evidence of real Redis persistence or transaction atomicity.
No existing learner records, runtime definitions or retained conversations are accessed.
These tests do not validate real Redis transactions, concurrent writes, health endpoints, gateway
delivery, deployed runtime policy or speech providers. Live two-call verification remains separate.
The project links the platform-neutral MAUI configuration source to verify scope handling without
MAUI workloads or MSAL sign-in. It does not verify live tokens, consent or interactive authentication.
Focused verification passed all six `TutorClientConfigTests` cases, with zero failures or skips.
The MAUI Debug Android and Windows targets also built with zero warnings/errors. No sign-in,
client installation or inference was performed.

## Layout

```text
Tests/
├── InMemoryLearnerStore.cs
├── ManualTimeProvider.cs
├── RedisLearnerTestData.cs
├── Integration/
│   └── LearnerMcpPersistenceTests.cs
└── Unit/
    ├── AppHostTests.cs
    ├── LearnerMcpContextTests.cs
    ├── TutorMcpQueryServiceTests.cs
    ├── TutorMcpToolAnnotationTests.cs
    └── VocabularyScheduleTests.cs
```
