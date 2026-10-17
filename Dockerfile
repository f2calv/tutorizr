# syntax=docker/dockerfile:1
# Multi-architecture image, built from a single Dockerfile. Structure follows
# https://github.com/f2calv/multi-arch-container-dotnet
#
# One image serves every role; the enabled feature set (CasCap__FeatureConfig__EnabledFeatures)
# selects which services are registered and started at runtime.
#
# ------------------------------------------------------------------------------
# Stage 1 of 2: build
#
# Pinned to $BUILDPLATFORM and CROSS-COMPILES to $TARGETPLATFORM; emulating the
# target under QEMU instead is often an order of magnitude slower.
# ------------------------------------------------------------------------------
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo
COPY ["Directory.Build.props", "Directory.Packages.props", "global.json", "./"]

ARG WORKLOAD=CasCap.App.Server
ARG PROJECT=src/${WORKLOAD}/${WORKLOAD}.csproj
ARG CONFIGURATION=Release
ARG TARGET_FRAMEWORK=net10.0
ARG VERSION=0.0.0-local
ARG GIT_REPOSITORY=n/a
ARG GIT_BRANCH=n/a
ARG GIT_COMMIT=n/a
ARG GITHUB_WORKFLOW=n/a
ARG GITHUB_RUN_ID=0
ARG GITHUB_RUN_NUMBER=0

# -- Dependency layer ----------------------------------------------------------
# Cached until a csproj/props or package version changes. Restore every runtime identifier so each
# platform's publish runs offline with --no-restore.
COPY --parents src/**/*.csproj ./
RUN --mount=type=cache,target=/root/.nuget/packages,sharing=locked \
    dotnet restore "$PROJECT" \
    -p:Configuration="$CONFIGURATION" \
    "-p:RuntimeIdentifiers=\"linux-x64;linux-arm64;linux-arm\""

# -- Compile layer -------------------------------------------------------------
COPY --exclude=src/*.Tests . .

ARG TARGETARCH
ARG TARGETVARIANT
RUN --network=none --mount=type=cache,target=/root/.nuget/packages,sharing=shared <<EOF
set -eux
# https://learn.microsoft.com/dotnet/core/rid-catalog
case "${TARGETARCH}${TARGETVARIANT}" in
    amd64) RID=linux-x64   ;;
    arm64) RID=linux-arm64 ;;
    armv7) RID=linux-arm   ;;
    *) echo "unsupported platform: linux/${TARGETARCH}/${TARGETVARIANT}" >&2; exit 1 ;;
esac
dotnet publish "$PROJECT" \
    --configuration "$CONFIGURATION" \
    --framework "$TARGET_FRAMEWORK" \
    --output /app/publish \
    --runtime "$RID" \
    --self-contained false \
    --no-restore \
    -p:Version="$VERSION" \
    -p:SourceRevisionId="$GIT_COMMIT" \
    -p:GitRepository="$GIT_REPOSITORY" \
    -p:GitBranch="$GIT_BRANCH" \
    -p:BuildWorkflow="$GITHUB_WORKFLOW" \
    -p:BuildRunId="$GITHUB_RUN_ID" \
    -p:BuildRunNumber="$GITHUB_RUN_NUMBER"
ln -s "$WORKLOAD.dll" /app/publish/entrypoint.dll
EOF

# ------------------------------------------------------------------------------
# Stage 2 of 3: runtime
#
# The full image is required because the voice pipeline converts audio with ffmpeg, which a
# chiseled image cannot install.
# ------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# -- Runtime dependencies ------------------------------------------------------
RUN <<EOF
set -eux
apt-get update
apt-get install -y --no-install-recommends ffmpeg
rm -rf /var/lib/apt/lists/*
ffmpeg -version
EOF

COPY --link --from=build /app/publish .

# -- Provenance ----------------------------------------------------------------
# Supplied by the CI workflow (.github/workflows/ci.yml) or by build.ps1/build.sh.
ARG WORKLOAD=CasCap.App.Server
ARG GIT_REPOSITORY=n/a
ARG GIT_BRANCH=n/a
ARG GIT_COMMIT=n/a
ARG GIT_TAG=n/a

ARG GITHUB_WORKFLOW=n/a
ARG GITHUB_RUN_ID=0
ARG GITHUB_RUN_NUMBER=0

EXPOSE 8080

# https://github.com/opencontainers/image-spec/blob/main/annotations.md
LABEL org.opencontainers.image.title="$WORKLOAD" \
    org.opencontainers.image.description="German language tutor over Signal" \
    org.opencontainers.image.source="https://github.com/f2calv/tutorizr" \
    org.opencontainers.image.licenses="Unlicense" \
    org.opencontainers.image.version="$GIT_TAG" \
    org.opencontainers.image.revision="$GIT_COMMIT"

USER $APP_UID

# Exec form makes dotnet PID 1 so it receives SIGTERM for a clean shutdown.
ENTRYPOINT ["dotnet", "entrypoint.dll"]

# ------------------------------------------------------------------------------
# Stage 3 of 3: final
# ------------------------------------------------------------------------------
FROM runtime AS final
