# CasCap.App.Server.Tests

Credential-free .NET 10 unit tests for the tutorizr host, using xUnit v3 on
Microsoft.Testing.Platform. They exercise learner tools against an in-memory store, not a running
web host, Redis instance, Signal gateway or Agent Runtime.

## Tests

| Class | Methods | Cases | Trait |
| --- | --- | --- | --- |
| [`VocabularyScheduleTests`](Tests/Unit/VocabularyScheduleTests.cs) | 4 | 9 | `Scheduling` |
| [`TutorMcpQueryServiceTests`](Tests/Unit/TutorMcpQueryServiceTests.cs) | 8 | 10 | `Tools` |
| [`TutorMcpToolAnnotationTests`](Tests/Unit/TutorMcpToolAnnotationTests.cs) | 2 | 2 | `Tools` |
| **Total** | **14** | **21** | |

These are source-declared counts, not execution results. Four theories expand through 11
`InlineData` rows; the other ten methods are facts. There are no member/class data providers.

## Traits

| Category | Covers |
| --- | --- |
| `Scheduling` | Leitner box promotion, reset and term normalisation |
| `Tools` | MCP tool behaviour against an in-memory store, and pinned tool hints |

## Skipped Tests

None: no skip attributes, conditional skip branches or credential gates are declared.

## Running

Use the .NET 10 SDK. Debug builds use the adjacent-source setup in the
[host development guide](../CasCap.App.Server/README.md#development). The project reference compiles
the host even though these tests do not start it. From the repository root, when explicitly choosing
to run tests:

```powershell
dotnet test --project .\src\CasCap.App.Server.Tests\CasCap.App.Server.Tests.csproj --configuration Debug
```

`global.json` selects Microsoft.Testing.Platform. The direct test dependencies are `xunit.v3`
`4.0.2` and `Microsoft.Testing.Extensions.CodeCoverage` `18.12.0`.
No cloud credentials, emulator or external services are needed. The project removes copied
`appsettings.Local*.json` files from its output; the unit fixtures do not load application
configuration.

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
    ├── TutorMcpQueryServiceTests.cs
    ├── TutorMcpToolAnnotationTests.cs
    └── VocabularyScheduleTests.cs
```
