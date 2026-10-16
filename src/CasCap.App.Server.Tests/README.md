# CasCap.App.Server.Tests

Credential-free unit tests for the tutorizr host.

## Tests

| Class | Methods | Cases | Trait |
| --- | --- | --- | --- |
| `VocabularyScheduleTests` | 4 | 9 | `Scheduling` |
| `TutorMcpQueryServiceTests` | 8 | 10 | `Tools` |
| `TutorMcpToolAnnotationTests` | 2 | 2 | `Tools` |
| **Total** | **14** | **21** | |

## Traits

| Category | Covers |
| --- | --- |
| `Scheduling` | Leitner box promotion, reset and term normalisation |
| `Tools` | MCP tool behaviour against an in-memory store, and pinned tool hints |

## Skipped Tests

None.

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
