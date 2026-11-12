#!/usr/bin/env bash
# Builds a Release APK of the voice client; see build-apk.ps1.
set -euo pipefail

exec pwsh -NoProfile -File "$(dirname "$0")/build-apk.ps1" "$@"
