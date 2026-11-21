# ADR 0001: Windows-first voice validation

- **Status:** Proposed
- **Date:** 2026-11-20
- **Scope:** Tutorizr realtime voice development and pull-request validation.

## Context

The [MAUI project](../src/CasCap.App.Maui/CasCap.App.Maui.csproj) has Android and Windows targets.
Both use the [shared call page and media path](./architecture.md#voice-call-lifecycle). A Windows
baseline gives developers fast feedback without waiting for Android packaging and installation.
It does not establish Android lifecycle or device behavior.

## Proposed Decision

Use Windows for the first live voice check and for fast iteration. Windows can lead during
development, but before opening a PR the two targets must use the same call behavior, media
asset, and runtime contract. Keep OS-specific permissions, sign-in redirects, and lifecycle
hooks in platform code. For PRs that change shared call behavior, build both targets and smoke-test
the changed path on both; documentation-only and definition-only changes do not need that matrix.

Testing both platforms on every iteration would catch device differences earlier but slow the
inner loop. Treating Windows results as release evidence for both would miss those differences.
If Android-only regressions recur, bring the affected device check forward rather than forking
the client.

## First Validation: Windows Baseline

Build the Windows target and record its result separately from live-call validation. Use the
[Windows run instructions](../README.md#run-the-voice-client) and keep configuration private.
The build does not require a live test agent.

For an automated live check, use a voice-capable agent with learner tools disabled or backed
only by synthetic data. Leave conversation retention off and use neutral test phrases.
Retention off does not disable tools or prevent tool results from reaching the voice provider.
If the live target is not ready, record the build result and leave the call checks pending.
Changing a deployed definition or provisioning a test target is separate from this baseline.

- [ ] Sign-in completes and the call connects.
- [ ] The tutor hears the learner and the learner hears the tutor.
- [ ] Speaking over the tutor interrupts playback.
- [ ] Mute suppresses microphone input; unmute restores it.
- [ ] End closes the call, releases the microphone, and returns the UI to an idle state.

Record the Tutorizr revision, Agentizr version if known, and pass, fail, or not run for each
check. Do not include transcripts, credentials, or private endpoint details. Report the first
blocker and stop; fixes, Android acceptance, learner tools, and lesson design are separate tasks.
No live validation has been recorded yet.
