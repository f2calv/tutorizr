# CasCap.App.Server

The tutorizr host: a single ASP.NET Core process that runs the `Comms` and `Mcp` features.

## Public Surface

| Type | Purpose |
| --- | --- |
| `AppHost` | Bootstraps configuration, Redis, telemetry, features and endpoints |
| `TutorMcpQueryService` | MCP tools for the learner profile, vocabulary practice and mistake log |
| `ILearnerStore` / `RedisLearnerStore` | Learner progress persisted in Redis |
| `VocabularySchedule` | Leitner spaced-repetition rules: five boxes at 1, 2, 4, 8 and 16 days; a wrong answer returns after 10 minutes |

## Endpoints

| Path | Feature | Authentication |
| --- | --- | --- |
| `/mcp` | `Mcp` | Basic; requests carrying an `Origin` header are rejected with 403 |
| `/healthz`, `/healthz/{probe}` | always | anonymous |

## Configuration

| Section | Type |
| --- | --- |
| `AppConfig` | `AppConfig`, `AzureAuthConfig` |
| `CasCap:FeatureConfig` | `FeatureConfig` |
| `CasCap:TutorConfig` | `TutorConfig` |
| `CasCap:ApiAuthConfig` | `ApiAuthConfig` |

The Comms, Signalizr client, Agent Runtime and voice sections are documented by their owning
packages.

## Dependencies

| Reference | Debug | Release |
| --- | --- | --- |
| `CasCap.Common.Hosting.AspNetCore`, `.Caching`, `.Configuration` | adjacent project | package |
| `CasCap.Api.Azure.Auth` | adjacent project | package |
| `CasCap.Comms.AI` (with `CasCap.Comms` and `CasCap.Api.Voice`) | adjacent project | not yet published |
| `ModelContextProtocol.AspNetCore` | package | package |
