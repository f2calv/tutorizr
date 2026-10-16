# Copilot Instructions

## Shared Instructions

Shared Copilot instructions, skills and prompts are maintained centrally in the [.github](https://github.com/f2calv/.github) repository, under `.github/instructions/`, `.github/skills/` and `.github/prompts/`. They are deliberately not copied into this repository, so a change there takes effect everywhere without a pull request here.

To load them, clone that repository and either add it to this VS Code workspace, or link its folders into `~/.copilot/`. Its README explains both.

If those shared files are not visible, stop and tell the user rather than guessing the conventions — this repository depends on them.

Everything below is specific to this repository.

## Repository Role

- tutorizr is a Comms host for one Agent Runtime tenant. It owns the tutor's MCP tools and its
  learner store; the Agent Runtime owns definitions, providers, sessions and credentials.
- Keep the tutor's teaching behaviour in its Agent Runtime definition, not in host code. Tools
  persist and return learner data; they do not generate exercises.
- Comms is the chat group's single gRPC subscriber. Keep the workload at one replica and never
  overlap pods during rollout.

## Tools

- Every tool sets explicit `ReadOnly`, `Destructive`, `Idempotent` and `OpenWorld` hints and
  `UseStructuredContent`. Update the pinned table in `TutorMcpToolAnnotationTests` with any change.
- Fail a tool with `McpException` rather than returning an empty default, and bound list sizes
  and text lengths server-side.
- Never log learner content: vocabulary, mistakes, transcripts or chat text.

## Content

- Do not commit copyrighted word lists or exam material, such as Goethe-Institut PDFs. Load them
  from private storage at runtime.
