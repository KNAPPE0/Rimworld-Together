# KMH 2.7 — `v26.5.22.1`

KMH is now fully caught up to the **RimWorld Together dev branch** (through May 20, 2026). Every functional fix and feature upstream shipped in that window is merged into KMH, with KMH-style hardening on top.

On top of that baseline, **2.7 is the biggest KMH-specific release to date** — the economy got a full rework, guilds became an actual gameplay system, leaderboards went live, the Discord bridge picked up command-driven gameplay, and every dialog got rebuilt.

---

## Synced from RWT dev branch (upstream changes inherited in 2.7)

- Local self-hosting from the main menu
- Discord Rich Presence on the player's own profile
- Animated "please wait" dialog
- Permadeath autosave-interval bypass
- Compatibility-check "Continue anyway" button
- Server browser info panel + Discord / Workshop / Report buttons
- Wealth-on-unsaved-maps NRE fix
- `MaxPacketSize` bump (8 MB → 16 MB) for KMH's larger saves
- Per-player cooldowns for road / pollution / NPC events
- Planet sync groundwork (hooks ready for a future feature)
- Adaptive-training-helper suppression in multiplayer sessions
- Server config publishing Discord + Steam Workshop URLs to the browser
- Hardened version-download path (async + TLS 1.2 + input validation + live progress)

These were all upstream features. KMH's job in 2.7 was merging them cleanly without breaking the KMH-specific systems below.

---

## NEW in KMH 2.7 (what KMH added on top of upstream)

### 🏛️ Guilds, reworked

- **Guild Hall** dialog as the central hub — Manage / Leaderboards / Treasury / Diplomacy / Members
- **Officer rank** alongside Leader / Member with per-rank permissions
- **Diplomacy system** — alliances, wars, neutrality, with packet-driven UI updates
- **Worker XP** — every site cycle earns each assigned colonist their stat XP, persisted server-side
- **Bonus distribution** — leader can split a silver pool among members
- **Guild chat** in-game via `/guild`, optionally bridged to a Discord channel
- **Guild treasury** with silver + item storage, deposit/withdraw, full transaction log

### 💰 Marketplace

- **Search the entire item catalog** when posting — type to filter across every `DefDatabase` entry
- **Quality + material variants** — list "Excellent Plasteel knife" or "Normal Steel knife" as separate stacks
- **Source from caravan OR treasury** — list items still on your colony, OR items already in the guild treasury (silver returns there too)
- **Want-To-Buy (WTB) listings** — post what you're looking for, sellers can match against it
- **Two-line listings** so long item names don't bleed into the seller column
- **Reflowed toolbar** — filters fit even on the 820px windowed layout
- **Treasury live-refresh** after every List / Cancel / Buy — silver + item counts update without reopen

### 📋 Quest board

- **Post quests** with rewards sourced from caravan OR treasury
- **Item picker on Post Quest** — no more typing raw `Steel_x500` defNames
- **Friendly item labels** in every quest row + reward summary
- **Accept / Complete / Cancel** flow with treasury-aware reward delivery

### 🏆 Live leaderboards (push-refresh)

- **Per-player leaderboard** (new in 2.7) — lifetime stats: hours played, sites built, deaths, silver donated, quests completed, kills
- **Per-guild leaderboard** (expanded in 2.7) — sites, alliances, tenure, total worker XP added on top of existing columns
- Both **push-update via the server** — open the dialog and watch ranks shift live (no manual refresh)
- **★ marker** on your own guild row
- **Sortable by every column** with friendly enum names in the dropdown
- **Top-3 badges** + own-row highlight + `● live` indicator
- **Discord auto-poster** — server can publish a live leaderboard embed to a channel (edit-in-place or daily rollover)

### 🤖 Discord bridge + commands

(Bot bridge is KMH-original — separate from upstream's client-side Rich Presence.)

- **Bidirectional chat** — in-game ↔ Discord channel
- **Per-server tag** (`[S1]`, `[PVP]`) so multi-server clusters can share one channel
- **Admin-channel relay** of server console with severity-coloured embeds
- **Linked accounts** — `!link RWT-XXXXXX` binds Discord ID ↔ KMH username; dialogs show Discord names everywhere
- **Discord commands** (linked players): `!market`, `!buy`, `!sell`, `!cancel`, `!find`, `!items`, `!treasury`, `!quests`, `!leaderboard`, `!profile`, `!history`, `!compare`, `!showcase`, `!wtb`
- **`!showcase` + `!wtb` forum channels** — each player's listings auto-publish to their own forum thread, edit-in-place + auto-pruned when stale

### 🛡️ Anti-cheat + admin tooling

- **Username sanitization** — strips path-traversal characters before any filesystem use
- **Wealth clamping** — server-side check vs the actual wealth-watcher value; client-reported wealth can't exceed it
- **Packet size validation** with auto-disconnect for oversized packets
- **IP banning** — `CMD_BanIP` / `CMD_BanIPList` / `CMD_PardonIP` / `CMD_KickIP`, persists across restarts
- **OptionsProfile enforcement** — server requires specific RimWorld settings (storyteller, scenario, difficulty); clients with mismatches see "Restore Original Configs" on disconnect
- **O(1) username → client lookup** index for Discord links, marketplace buys, chat broadcasts
- **Custom-site anti-cheat** — server-side rate-limit on site builds + ownership validation
- **More server commands** — `CMD_DeleteSite`, `CMD_DrainHousePool`, `CMD_Enforce`, `CMD_Enforcement`, `CMD_GCClear`, `CMD_Help` (admin-aware listing), `CMD_Motd`, `CMD_PlayerCount`, `CMD_Quit`, `CMD_Sites`

### 🛠️ Custom sites

- **Build your own outposts** beyond the upstream-stock site types
- **Worker assignment** with skill XP — colonists gain XP based on the site's production type
- **`BaseSkillLevel`** + per-colonist XP tracking, persisted server-side
- **`ManageWorker`** custom-site aware — workers reassign cleanly when sites change

### 🎨 UI overhaul

Every dialog rebuilt with KMH's new shared `DialogLayout` system:

- **Custom-rendered tight checkboxes** — the ☐ sits flush against the label (no more shoving the box to the right edge)
- **`DLG_Buttons` permanent overlap fix** — header + footer + per-button math properly accounted for; "Cancel" no longer hides "Leave" in Guild Management
- **`DLG_SiteMenu` KMH Feature tag** no longer cut off
- **Treasury widened (980 × 580)** with hard column boundaries — Recent Activity uses color chips + two-line rows + friendly item labels
- **Marketplace widened (1040 × 620)** with hard column boundaries and two-line listings
- **Friendly enum names** in sort dropdowns ("Most members" instead of `MemberCount`)
- **Item label cache** (`PM_ItemLabels`) — server learns defName → display label from every connected client, so Discord embeds and server-side messaging stop showing raw defNames
- **Auto-refresh** after every economy action — no more "close the dialog and reopen to see your new silver balance"

### 🖥️ Bundled multi-platform self-hosting (KMH extension of upstream's LocalHost feature)

Upstream RWT's local-host feature ships a Windows-only download path. KMH 2.7 extends it:

- **Per-RID server binaries shipped IN the mod** — Windows x64, Linux x64, macOS Intel, macOS Apple Silicon
- **Self-contained single-file binaries** — players don't install .NET, don't need a separate download, don't touch a terminal
- **One-click install** from the main menu — copies the bundle into `%AppData%` (away from Steam Workshop's read-only mod folder), sets the executable bit on Linux / macOS automatically
- **Edit configs from in-game** — main menu → Host Local Server → "Edit configs (ServerConfig, ActionConfig, …)" opens a sub-menu of the server's JSON files in your system editor. No more navigating to AppData.
- **Reinstall workflow** — when a server-side patch ships, one click refreshes your binary without touching your saves / configs
- **URL fallback** — if a player's platform isn't bundled, they can paste a URL to a `GameServer` zip in mod settings

---

## ⚠️ Upgrade notes from KMH 2.6

### `packageId` changed

2.7 ships with `packageId="nova.rimworldtogether.kmh"`. 2.6 used the upstream `nova.rimworldtogether`. RimWorld treats it as a different mod — you'll see a one-time **"missing mod"** notice loading old saves. Harmless:

1. Subscribe / install KMH 2.7
2. **Disable** the old KMH 2.6 entry in the mod manager
3. **Enable** "RimWorld Together (KMH)" (2.7)
4. Load your save — the warning goes away after the first save-with-2.7

Your colony, guild, treasury, marketplace listings — everything in your save file — is preserved. Only the mod's identity changed.

### RimWorld 1.5 support dropped

2.7's client assemblies are 1.6-only. If you're still on RimWorld 1.5, stay on KMH 2.6 until you update.

### Protocol incompatible with upstream RWT

KMH speaks a superset of upstream's packet protocol. **KMH 2.7 clients cannot connect to vanilla RWT servers, and vice versa.** Both your client and your server need to be on 2.7.

The upstream `nova.rimworldtogether` mod is now listed as `incompatibleWith` — RimWorld warns if both are enabled at the same time. Pick one.

---

## For self-hosters

Three ways to produce a release build:

```powershell
# Windows
.\Scripts\build-release.ps1
```

```bash
# Linux / macOS
make release
```

```yaml
# GitHub (automatic) — push a tag, publish a release on the web UI.
# .github/workflows/release-build.yaml produces per-RID server zips as
# release assets (win-x64.zip, linux-x64.zip, osx-x64.zip, osx-arm64.zip).
```

The Workshop bundle is ~150 MB total (4 × ~36 MB single-file binaries + client assets). If that's too heavy, set `LocalServerDownloadUrl` in mod settings to a hosted .zip and the bundle becomes opt-in download instead.

Docker is also supported — `docker compose up -d --build` produces an image equivalent to the linux-x64 bundle.

---

## Known caveats

- No connectivity with upstream RWT servers (protocol incompatible — see above)
- Workshop download grows by ~150 MB vs a client-only mod (the bundled per-platform binaries)
- Existing KMH 2.6 players need to re-enable the mod under the new entry
- macOS Apple Silicon server is included but less tested than the other three platforms

---

## Thanks

To **Nova and Company** for RimWorld Together and the continued upstream work — KMH stands on that codebase.

To the **KMH community** — every overlap screenshot, every Discord complaint, every "this looks wrong" report drove a fix in 2.7. The UI overhaul exists because you kept showing me the problems.

Have fun. 🚀

---

*KMH `v26.5.22.1` — built for RimWorld 1.6, .NET 8 server, .NET Framework 4.7.2 client.*
