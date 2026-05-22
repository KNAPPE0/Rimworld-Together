# KMH 26.5.22.1: Makefile shortcuts for the KMH build pipeline.
# Provides the same outputs as Scripts/build-release.ps1 for users
# on Linux/macOS who don't have PowerShell handy.
#
# Common entry points:
#   make build            -- build all C# projects (Release)
#   make publish-server   -- publish self-contained single-file binaries
#                            for win-x64, linux-x64, osx-x64, osx-arm64
#   make release          -- build + publish + stage Release/ folder
#                            (Steam-Workshop-ready)
#   make clean            -- nuke build artifacts
#
# Variables (override on the command line):
#   make release RELEASE_DIR=../KMH-Workshop    # custom output path
#   make publish RID=linux-x64                  # single-RID publish

# -------- Configuration -----------------------------------------
.DEFAULT_GOAL    := build
MOD_ROOT         := $(CURDIR)
SOURCE           := $(MOD_ROOT)/Source
LOCAL_SERVER     := $(MOD_ROOT)/LocalServer
RELEASE_DIR      ?= $(MOD_ROOT)/../KMH-Release
RIDS             := win-x64 linux-x64 osx-x64 osx-arm64

# Match Scripts/build-release.ps1 publish flags so Docker / Makefile /
# PowerShell all produce identical binaries.
PUBLISH_FLAGS    := -c Release \
                    --self-contained true \
                    -p:PublishSingleFile=true \
                    -p:IncludeNativeLibrariesForSelfExtract=true \
                    -p:EnableCompressionInSingleFile=true \
                    -p:DebugType=embedded \
                    -p:WarningLevel=0 \
                    --nologo

# -------- Per-project builds (matches Official's targets) -------
build-network:
	dotnet build $(SOURCE)/TCPNetwork/TCPNetwork.csproj --configuration Release -p:WarningLevel=0 --nologo

build-shared:
	dotnet build $(SOURCE)/Shared/Shared.csproj --configuration Release -p:WarningLevel=0 --nologo

build-server:
	dotnet build $(SOURCE)/Server/GameServer.csproj --configuration Release -p:WarningLevel=0 --nologo

build-client:
	dotnet build $(SOURCE)/Client/GameClient.csproj --configuration Release -p:WarningLevel=0 --nologo

build-synchronous:
	dotnet build $(SOURCE)/Synchronous/Synchronous.csproj --configuration Release -p:WarningLevel=0 --nologo

build: build-shared build-network build-synchronous build-server build-client

# -------- KMH release: per-RID self-contained publish ----------
# Generates LocalServer/<RID>/{GameServer.exe|GameServer} -- single-file,
# embeds the .NET 8 runtime. Matches Scripts/build-release.ps1.
publish-server-%:
	@echo ">> Publishing $* -> LocalServer/$*/"
	dotnet publish $(SOURCE)/Server/GameServer.csproj $(PUBLISH_FLAGS) -r $* -o $(LOCAL_SERVER)/$*
	@# Strip debug symbols + XML docs that the runtime doesn't need
	@find $(LOCAL_SERVER)/$* -type f \( -name '*.pdb' -o -name '*.xml' \) -delete 2>/dev/null || true

publish-server: $(addprefix publish-server-,$(RIDS))
	@echo "All RIDs published into $(LOCAL_SERVER)/"

# -------- Single-RID convenience target -------------------------
# Usage: make publish RID=linux-x64
publish: publish-server-$(RID)

# -------- Release staging ---------------------------------------
# Builds everything, publishes all RIDs, and stages a clean
# Steam-Workshop-ready folder at $(RELEASE_DIR). Mirrors what
# Scripts/build-release.ps1 does on Windows.
release: build publish-server
	@echo ">> Staging $(RELEASE_DIR)/"
	@rm -rf $(RELEASE_DIR)
	@mkdir -p $(RELEASE_DIR)
	@cp -r $(MOD_ROOT)/About            $(RELEASE_DIR)/
	@cp -r $(MOD_ROOT)/1.6              $(RELEASE_DIR)/
	@cp -r $(LOCAL_SERVER)              $(RELEASE_DIR)/LocalServer
	@cp -r $(MOD_ROOT)/Scripts          $(RELEASE_DIR)/
	@cp    $(MOD_ROOT)/LoadFolders.xml  $(RELEASE_DIR)/
	@cp    $(MOD_ROOT)/LICENSE          $(RELEASE_DIR)/
	@cp    $(MOD_ROOT)/README.md        $(RELEASE_DIR)/ 2>/dev/null || true
	@# Scrub dev artefacts that may have snuck into the copies
	@find $(RELEASE_DIR) -type d \( -name 'bin' -o -name 'obj' -o -name '.vs' \) -exec rm -rf {} + 2>/dev/null || true
	@find $(RELEASE_DIR) -type f \( -name '*.csproj' -o -name '*.sln' \) -delete 2>/dev/null || true
	@echo
	@echo "==============================================="
	@echo " RELEASE STAGED"
	@echo "==============================================="
	@echo "Folder: $(RELEASE_DIR)"
	@du -sh $(RELEASE_DIR) 2>/dev/null || true
	@# Warn about Official PublishedFileId.txt staying in About/
	@if [ -f $(RELEASE_DIR)/About/PublishedFileId.txt ] && [ "$$(cat $(RELEASE_DIR)/About/PublishedFileId.txt | tr -d '[:space:]')" = "3005289691" ]; then \
		echo; \
		echo "WARNING: About/PublishedFileId.txt still has Official's Workshop ID (3005289691)."; \
		echo "  Replace with your KMH ID, or delete to create a new Workshop entry."; \
	fi

# -------- Cleanup ------------------------------------------------
clean:
	@echo ">> Cleaning build artefacts"
	@rm -rf $(LOCAL_SERVER) $(RELEASE_DIR)
	@find $(SOURCE) -type d \( -name 'bin' -o -name 'obj' \) -exec rm -rf {} + 2>/dev/null || true

.PHONY: build build-network build-shared build-server build-client build-synchronous \
        publish publish-server release clean
