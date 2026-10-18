# tutorizr

Umbrella chart for tutorizr, a tutor (German in the proof of concept) that answers a Signal group
through the CasCap Agent Runtime and serves its learner tools over MCP.

## Overview

The chart aliases the shared `workload` chart once:

| Alias | Purpose |
| --- | --- |
| `tutorizr` | Single replica running the `Comms` and `Mcp` features |

The alias defaults to zero replicas. Comms is the chat group's single gRPC subscriber, so the
schema caps the replica count at one and rollouts never overlap pods.

## Usage

```yaml
tutorizr:
  replicaCount: 1
  image:
    repository: ghcr.io/example/tutorizr
    tag: example
  envVars:
    CasCap__CachingConfig__RemoteCacheConnectionString: redis.example.svc:6379,abortConnect=false
    CasCap__SignalizrClientConfig__BaseAddress: http://signalizr.example.svc
    CasCap__SignalizrClientConfig__GrpcAddress: http://signalizr.example.svc:5001
    CasCap__AgentRuntimeClientOptions__BaseAddress: http://agent-runtime.example.svc
```

Never place connection-string credentials, Basic authentication values, certificates or API keys
in chart values. Supply them from Kubernetes Secrets or Azure Key Vault.

## Dependencies

| Chart | Version | Alias |
| --- | --- | --- |
| `oci://ghcr.io/f2calv/charts/workload` | `1.1.0` | `tutorizr` |
