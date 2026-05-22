# KMH 26.5.22.1: Container image for the KMH RimWorld Together server.
#
# Two-stage build:
#   1. build-env uses the .NET 8 SDK to publish the server as a
#      self-contained single-file Linux x64 binary. Flags mirror
#      Scripts/build-release.ps1 so the container image is byte-for-byte
#      equivalent to the LocalServer/linux-x64/GameServer that ships in
#      the Steam Workshop bundle.
#   2. The runtime image carries only the binary and exposes the server
#      port + data volume.
#
# Build:
#   docker build -t kmh-server:26.5.22.1 .
#
# Run (data persisted to ./Data on host):
#   docker run -it --rm -v "$PWD/Data:/Data" -p 25555:25555 kmh-server:26.5.22.1
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /App

# Source-only copy keeps the build context lean (no LocalServer/, bin/,
COPY Source Source

# Publish as a self-contained single-file binary. Flags match
# Scripts/build-release.ps1 verbatim so this image and the Workshop
# bundle ship identical binaries.
#   --self-contained true                       : embed the .NET 8 runtime
#   -p:PublishSingleFile=true                   : collapse to one binary
#   -p:IncludeNativeLibrariesForSelfExtract=true: bundle native deps
#   -p:EnableCompressionInSingleFile=true       : shrink the bundle ~30%
#   -p:DebugType=embedded                       : pdb inside the binary
#   -p:WarningLevel=0                           : preserved from upstream
#                                                 to keep CI green on
#                                                 KMH's pre-existing
#                                                 third-party warnings.
RUN dotnet publish Source/Server/GameServer.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:EnableCompressionInSingleFile=true \
    -p:DebugType=embedded \
    -p:WarningLevel=0 \
    -o /App/publish

# Build runtime image. The single-file binary does NOT need the aspnet
# runtime (self-contained = runtime is embedded), but the aspnet base
# image is well-supported, fits in cache, and includes useful container
# debugging tools. Could be switched to debian:slim or distroless for a
# smaller footprint if size matters.
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /App

COPY --from=build-env /App/publish/GameServer /App/Server/GameServer

# Server writes its Configs/, Saves/, Logs/, Assets/ into the current
# working directory — mount /Data so they persist across container
# restarts.
WORKDIR /Data
VOLUME /Data

EXPOSE 25555/tcp

ENTRYPOINT ["/App/Server/GameServer"]
