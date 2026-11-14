# CasCap.App.Server.Tests

Credential-free .NET 11 unit tests for the tutorizr host, using xUnit v3 on
Microsoft.Testing.Platform. The tool and scheduling tests use an in-memory store without a running
web host, Redis instance, Signal gateway or Agentizr runtime. The separate host-startup test loads
application configuration and requires an isolated environment before execution.

## Tests

| Class | Methods | Cases | Trait |
| --- | --- | --- | --- |
| [`AppHostTests`](Tests/Unit/AppHostTests.cs) | 1 | 1 | `Hosting` |
| [`VocabularyScheduleTests`](Tests/Unit/VocabularyScheduleTests.cs) | 4 | 9 | `Scheduling` |
| [`TutorMcpQueryServiceTests`](Tests/Unit/TutorMcpQueryServiceTests.cs) | 8 | 10 | `Tools` |
| [`TutorMcpToolAnnotationTests`](Tests/Unit/TutorMcpToolAnnotationTests.cs) | 2 | 2 | `Tools` |
| **Total** | **15** | **22** | |

These are source-declared counts, not execution results. Four theories expand through 11
`InlineData` rows; the other eleven methods are facts. There are no member/class data providers.

## Traits

| Category | Covers |
| --- | --- |
| `Hosting` | Unexpected startup failures return a non-success process result |
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

They do not validate Redis transactions, concurrent writes, HTTP Basic authentication, MCP transport,
health endpoints, gateway delivery, runtime policy or speech providers.

## Layout

```text
Tests/
├── InMemoryLearnerStore.cs
├── ManualTimeProvider.cs
└── Unit/
    ├── AppHostTests.cs
    ├── TutorMcpQueryServiceTests.cs
    ├── TutorMcpToolAnnotationTests.cs
    └── VocabularyScheduleTests.cs
```
