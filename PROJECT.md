# Electric Glider Mk II: current handoff

Updated October 30, 2026. **Current version:** `0.1.23`.

This is the canonical continuation context. Read [README.md](README.md) for controls/build usage and [VERIFICATION.md](VERIFICATION.md) for evidence and historical limits. Current user instructions, executable checks and current source take precedence over historical verification notes. Update this handoff after meaningful changes, distinguishing built, installed, loaded and playtested versions.

## Current state

- v0.1.23 adds original armor-inspired Mk II geometry (four deployment states), canonical armor/metal texture references and orange boost/lift entity light. VSPT's managed discovery sees the conditional light; no native rendering or game launch was performed. Model stills are geometry previews, not game screenshots. v0.1.23 is now installed after explicit user approval, including v0.1.21/.22 changes. Its installed hash matches the tested package; v0.1.20 was backed up before removal. Restart/loaded-version confirmation and rendered acceptance remain pending.

- v0.1.22 strengthens first-person flight visibility: actual camera mode replaces the cached renderer mode, and guarded base body/held-item draws cover calls that bypass player overrides. Optional VSPT camera exclusion is scoped to its entity-render callback and restored on exceptions. The user says clipping predates shaders; VSPT is not established as its cause. Exact live trigger and visual improvement remain unconfirmed. The previous client log confirmed v0.1.20 loaded; the v0.1.22 changes and doubled S thrust are now installed as part of v0.1.23, pending restart confirmation.

- Mod ID: `electricglidermk2`; target: Vintage Story 1.22.7, EP Equipment 3.2.3, Core 3.3.0 and Basics 3.3.1; .NET 10 SDK.
- v0.1.17 addresses a reproduced forward surge on boost release and removes startup descent damping. Coasting is rebased on actual speed; level/upward coasting cannot convert unused boost speed/climb momentum into a horizontal kick. Native descent runs immediately. 35/45 caps, directional thrust, four-block horizontal/upward V movement and energy rates remain. v0.1.19 adds weak reverse thrust while holding S during gliding. The user crash report confirms v0.1.19 loaded and a NullReferenceException in FlightStepPrefix when deploying during a fall. v0.1.20 guards that hook and snapshots inventory once; this remains included in installed v0.1.23. v0.1.21 doubles S reverse thrust to 6, included in v0.1.22. The current client log confirms v0.1.20 loaded; rendered retesting of installed v0.1.23 remains necessary. The report identifies our hook but not the exact null reference. A controlled changing-inventory test reproduces the exception in the old code; the updated test passes. Exact installation evidence is in the private local handoff.
- v0.1.13 addressed the separate V-choppiness report with charge-only synchronization. Do not infer rendered smoothness from offline checks; verify the loaded version before assessing feedback.
- Earlier freezing/falling and roughly ten-block Space-launch reports prompted v0.1.11/0.1.12 corrections. They have offline regression coverage but limited user playtesting; do not mark all runtime behavior resolved.
- Exact machine paths, hashes and installation evidence are in ignored `.local/HANDOFF-LOCAL.md`. Keep that file and `.local/provenance.json` out of public commits.
- This folder is the mod's dedicated Git repository, using branch `main` and GitHub repository [tannerjriddle97/ElectricGliderMkII](https://github.com/tannerjriddle97/ElectricGliderMkII). Work here, preserve unrelated changes and immutable `releases/` snapshots. Older chat folders are recovery sources only.

## Behavior and invariants

| Setting / contract | Current behavior |
| --- | --- |
| `MaxGlideBlocksPerSecond = 35` | Normal total 3D speed cap; a lower EP cap takes precedence. |
| `MaxBoostBlocksPerSecond = 45` | Charged sustained Shift boost cap; release/depletion restores the normal cap. |
| `CoastBlocksPerSecond = 12` | Level coast target; excess speed bleeds off at 2.5 blocks/second squared. Dives raise the target toward 35; unpowered pull-ups lower it. |
| `BoostAcceleration = 9` | Added Shift thrust in blocks/second squared along the full look vector; also the airspeed progression rate while boosting. |
| `ReverseThrustAcceleration = 6` | S/backward applies opposing look-vector thrust while gliding. Without Shift, the airspeed ceiling falls at 6 blocks/second squared below cruise; with Shift, net thrust and airspeed acceleration are 3. No separate energy debit; lift positioning is unaffected. |
| `LaunchSafetySeconds = 2` | Passive upward-launch prevention only. No downward-acceleration damping or descent cap at startup. |
| Boost release | Rebase speed setting to actual momentum; level/upward coasting cannot gain horizontal speed above its existing speed, except gradual recovery toward 12 at up to 6 blocks/second squared. Boost or a downward aim beyond about 3° clears this release state. |
| Powered climbing | Hold Sneak/Shift with charge to add thrust where facing. No special upward-speed target/cap, horizontal-speed trim or forced 60° climb angle. Native forces and total cap still affect trajectory. |
| Space deployment | Native downward acceleration immediately, subject to total cap. No hover period, downward damping or separate descent cap. Passive flight cannot generate an upward launch for the first two seconds; existing jumps can slow normally. |
| Fall damage | An equipped Mk II backpack slot blocks Gravity damage, including while folded or empty. Other damage and carried-only inventory items are unaffected. |
| Lift binding | Default Shift+Space, either Shift key; saved user bindings remain. Registered first so the combo can take precedence over ordinary Space. Release either required key to stop. |
| `PositioningBlocksPerSecond = 4` | Existing separate mode: 4 blocks/second upward, 4 blocks/second upright WASD positioning, folded glider; release lift, then manually deploy with Space. |
| Existing items | Six storage slots and 4,000 charge units / 80,000 J; preserve storage, charge, recipe and first-person rendering behavior. |
| Original EP glider | Retains its own physics, speed cap and damage behavior. |
| Visual design | Original steel/blue/copper pack, twin thrusters, zinc trim, swept folding wings; all four shapes attach to UpperTorso. Installed game/EP textures are referenced, not bundled. |
| Thrust lighting | Native entity HSV 4/6/11 during charged Shift boost or lift; no extra torch/entity/block. Stronger existing light wins. No light for passive glide/S alone, dead/ineligible players or empty/unequipped packs. Nozzle face glow 32 is a small permanent visual accent only. |

Normal gliding must retain native vanilla/EP movement. Do not reinstate the superseded replacement `StepGlide` controller or restore velocity from before EP's flight pass. Keep ordinary gravity and drag active during normal gliding; their bypass remains limited to V lift. v0.1.14 deliberately manages the native `GlideSpeed` scalar with an elapsed-time progression and final speed ceiling: EP's boost increment is otherwise per physics call. This is not a replacement for native forces, heading or collision integration.

## Source map

- [src/ThrustLight.cs](src/ThrustLight.cs): postfix on EntityPlayer.LightHsv, preserving existing light/state. VSPT uses this native light in its existing entity path; no new VSPT patch is required for thrust light.
- [scripts/Build-VisualAssets.py](scripts/Build-VisualAssets.py): deterministic original four-stage geometry and Mk II shape references. [scripts/Build-VisualPreview.py](scripts/Build-VisualPreview.py) and [scripts/Render-VisualPreview.py](scripts/Render-VisualPreview.py) create private HTML/still previews from actual assets; Python plus Pillow/numpy for stills. Generated shapes are source; ordinary build does not require Python.
- [tests/physics/VisualChecks.cs](tests/physics/VisualChecks.cs): native shape deserialization/attachment and actual patched light-getter checks, plus optional installed VSPT managed light-discovery checks.

- [src/FlightVisibility.cs](src/FlightVisibility.cs): local first-person flight gate, base/player body and item draw guards, optional renderer compatibility. Optional hooks are discovered at client startup; unsupported APIs produce a warning and retain vanilla guards. [tests/physics/RenderChecks.cs](tests/physics/RenderChecks.cs) checks actual patched methods without a GPU or game startup.

- [src/Mk2System.cs](src/Mk2System.cs): Harmony registration, equipped-item detection, input/network leases, energy and rendering. `FlightStepPrefix` / `FlightStepPostfix` wrap `EntityBehaviorControlledPhysics.MotionAndCollision` once around the complete force pipeline, before collision integration. `FallDamagePrefix` gates Gravity damage. `AirApplyPrefix`, `GravityPostfix` and `DragPrefix` provide the separate V-lift path.
- `ChargeUpdate`, `SyncCharge`, `OnChargeUpdate` and `ApplyChargeUpdate` in the same file: one initial native inventory update establishes a stack ID; charge-only messages then avoid client `DidModifyItemSlot`/bag reloads. Sequence and stack-ID checks protect against stale/replaced-slot updates. Server billing remains authoritative; normal inventory operations keep native synchronization. Client/server must both use v0.1.13 or later matching builds.
- [src/FlightModel.cs](src/FlightModel.cs): `StepSpeedLimit` advances coast/dive/boost airspeed; `LimitFlightStep` adds look-direction thrust, prevents passive launch and limits total speed. No startup descent limiter remains. `StepLift`, `DrainPerSecond` and `Debit` retain lift/energy math. `MaximumClimbRadians` only shapes energy cost. `GlideSession` tracks speed and boost release; its horizontal post-limit prevents a release surge without replacing native forces/steering. Deployment resets release state; boost/dive clears it.
- [src/ItemMk2Glider.cs](src/ItemMk2Glider.cs): native glider item, crafting charge transfer and six-slot initialization.
- [tests/Program.cs](tests/Program.cs): pure flight-limit/lift/energy checks.
- [tests/physics/Program.cs](tests/physics/Program.cs): actual Harmony-patched native/EP pipeline using controlled world/player/inventory doubles; no game/server startup or save loading.
- [probe/OriginalFlightRegression.cs](probe/OriginalFlightRegression.cs): optional server integration harness. Compilation is not evidence that it was run.
- [build.ps1](build.ps1): builds/packages; offline pipeline checks run only with `-PhysicsReferencesPath`. It never installs or launches the game.

## Validation gates

v0.1.22 additionally exercises actual player/base render methods with all three cached render modes, first/third-person cameras, self/other players, shadow passes, on-foot restoration, lift and original-glider exclusion. Optional compatibility checks use `-RenderReferencesPath` pointing at a private directory containing the installed `VsRtx.Scene.dll`; they patch managed methods and invoke stopped callbacks only, never native rendering. These checks do not establish rendered success in the user's modpack.

Latest v0.1.23 suites: **3,218 pure checks + 21,730 offline physics/render/visual checks = 24,948 checks**, including eight optional-renderer checks. Without optional renderer references, the offline suite has 21,722 checks. New hook checks cover an inventory that disappears after its first read, unavailable mod system/API, empty slot, unchanged motion on fallback and recovery with a fresh session. S regressions cover reverse direction/strength, opposing Shift, braking below cruise and gradual release at 20/60/144 Hz. The new real-method release regression first failed against v0.1.16 behavior: a 0.2-second upward boost followed by release/leveling at 20 Hz jumped horizontal speed from 7.16 to 13.19 blocks/second in one step. It now passes at 20/60/144 Hz for brief/sustained boosts and level/climbing approaches, allowing only gradual below-cruise recovery. Startup descent checks now confirm native acceleration passes through instead of testing the removed gravity guard. Existing speed cycles, V/charge, fall protection and original-glider isolation remain covered.

For source/physics changes, run from this directory with a .NET 10 SDK and matching local game/EP assemblies:

```powershell
./build.ps1 -DotnetPath $dotnet10 -GamePath $gamePath -PhysicsReferencesPath './build/references'
./Verify-Handoff.ps1
```

`build/references/` must contain matching EP Core, Basics and Equipment DLLs with their original filenames. These are ignored local dependencies, not redistributable source. Local toolchain paths are in the workspace's ignored `.local/toolchain.json`. A build without `-PhysicsReferencesPath` runs only pure checks; do not report the full pipeline suite as passed for that build.

Runtime work requires current task authorization under the workspace agreement. Do not launch the game/server, run the server probe, change saves or reinstall packages merely to update documentation. Installation in this session was explicitly requested by the user.

## Open work

1. v0.1.23 is installed and hash-verified; v0.1.20 was backed up. Fully restart, confirm the loaded version, then test deployment/attachment, inventory/held/ground appearance and orange boost/lift light with and without VSPT, plus banked turns in plain/immersive first person; check arms return on landing and third-person/shadows remain. Also check stronger S braking and Space deployment during a fall. Existing saved V mappings may need changing/resetting to Shift+Space in Controls.
2. With runtime testing authorized, compare sustained V ascent before/after charge-sync changes. Client charge packets previously called native `InventoryNetworkUtil.UpdateFromPacket` → `DidModifyItemSlot` → backpack bag reloads on every debit. The new path removes that work; actual rendered improvement is unconfirmed. Existing logs also contain render-thread stalls, so do not assume every hitch is caused by this mod.
3. Check Space deployment while walking off a block and already falling: forward travel, native descent immediately, no freeze or passive upward launch. Test Shift aimed up/down/across headings, 35/45 caps, release, resuming boost and diving after release.
4. Check terrain collisions, fall protection/unequipping, V release/manual Space deployment, charge/storage preservation, first-person visuals and multiplayer. The charge message is server-to-owner; normal inventory synchronization remains responsible for stack/content changes.
5. Keep third-party notices and exclude game/mod binaries, profiles, saves, caches and provenance from source commits. No original-code license has been selected; do not add one without the author's choice. Reused game/EP media are referenced by asset location rather than bundled; private previews embed dependency textures and must remain excluded.

## Decisions, newest first

- **0.1.23:** user requested electric-armor styling and orange thrust illumination, with VSPT as a stretch goal. Original geometry uses steel/zinc/copper and EP's blue armor texture via references. Native LightHsv postfix avoids hidden torches, inventory spoofing, save edits or extra entities, and preserves stronger light. Installed VSPT discovers this light through its own managed entity path. Preview uses actual shape geometry with simple lighting, not VSPT. Actual appearance/material response remains a rendered acceptance step after installation approval.

- **0.1.22:** user reports arms/body entering the first-person camera during turns, predating shaders. Cover native renderer bypasses and use current camera mode rather than cached render mode. Optional VSPT compatibility reuses its camera-exclusion flag only within its entity render callback, with thread-local scope and finalizer cleanup; no shader settings or native buffers are changed. The direct Frame transpiler was rejected in offline testing because the game's Harmony cannot re-emit that method's exception filters; it is not shipped. No claim that either coverage gap proves the exact live cause.

- **0.1.21:** user requested doubling backward thrust. ReverseThrustAcceleration is now 6; this also doubles S braking progression. Shift remains 9, so Shift+S nets 3. Existing braking tests now reach about six blocks/second from twelve after one second instead of two.

- **0.1.20:** user crash report names our FlightStepPrefix in v0.1.19. Old prefix performed three inventory lookups and an unchecked final slot dereference, plus an unchecked mod-system lookup. A disappearing-inventory test reproduced the same exception in that method. Prefix now captures one checked item snapshot, guards missing API/system/item state and resets the session on fallback; Powered also captures/checks its item once. Do not claim the report proves which individual reference was null or a full live reproduction.

- **0.1.19:** user requested weak backward thrust while holding S. Uses the backward control binding, adds a 3-block/second-squared impulse opposite look direction, and lowers the unboosted airspeed ceiling so native cruise thrust cannot cancel braking. Shift+S combines opposing impulses and uses 6-block/second-squared acceleration. No new energy debit; lift S remains positioning.

- **0.1.18:** user requested Shift+Space as the default lift binding. Uses RegisterHotKeyFirst with Space and shiftPressed=true; existing KeyHeld modifier checks accept either Shift and require both keys. Saved rebindings are preserved. Includes the still-uninstalled v0.1.17 flight corrections.

- **0.1.17:** user reported release momentum and requested no startup descent damping. Reproduced a release/climb-leveling surge in real-method tests, rebased airspeed on release and bounded post-release horizontal recovery. Removed the downward gravity guard completely; retained only passive upward-launch safety. Do not reintroduce startup downward damping.
- **0.1.16:** user requested 35 normal / 45 boost and more horizontal movement in V mode. Doubled V positioning to 4 blocks/second with normalized diagonal input; upward V remains 4. Acceleration/energy rates stay unchanged, so 12-to-45 level boost takes about 3.7 seconds.

- **0.1.15:** user requested Shift thrust along the look direction and less startup vertical damping. Removed upward-speed targeting, climb-angle handling, fall cancellation, hover period and separate downward-speed cap. Added a 9-block/second-squared directional impulse; startup limits only added downward acceleration to current game gravity for two seconds. This supersedes v0.1.14's special climb limits.
- **0.1.14:** user requested 25 normal / 30 continuous-boost speed and clarified excessive vertical thrust was Shift climbing. Level coasting now settles toward 12; dive/boost regain speed and pull-ups lose it. Native `GlideSpeed` follows elapsed time because EP's fixed increment per physics call gave weak boost at 20 Hz. Shift upward acceleration is halved to 6 and ascent capped at 10; V remains 4. Do not describe steep climbs as full-cap flight.
- **0.1.13:** user isolated choppiness to V lift. Native inventory packets invoke client bag reloads even when the server merely queues a dirty slot. Charge-only messages bypass those callbacks while keeping server billing, lift motion and normal inventory operations unchanged. Do not reintroduce per-debit inventory packets as the normal sync path.
- **0.1.12:** passive deployment must not create a free upward launch. Protection no longer holds upward velocity against normal deceleration. Added pure and real-method regressions.
- **0.1.11:** restored native flight. The replacement air controller skipped vanilla movement and erased EP's motion output; limits now wrap the complete force pipeline. Added offline real-method checks, including original-glider and damage behavior.
- **0.1.10:** user limited controlled diving to the first approximately two seconds; the total cap remains afterward. This was the version last observed in the client log.
- **0.1.9 — superseded:** introduced the requested cap, fall immunity, descent protection and climbing, but replaced too much native flight handling. Pure math tests passed without establishing working integration. Do not revive this as a known-good baseline.
- **0.1.8 — recovered baseline:** original EP flight with 1.5× boost thrust; old full-modpack probe results belong to that version only. Its inherited speed/gravity/drag claims do not describe the current limiter/climb assistance.

## Recovery and artifact boundaries

Source/assets were recovered October 29, 2026. `releases/` and `.local/provenance.json` remain recovery evidence; new outputs belong under ignored `artifacts/`, `build/`, `bin/` and `obj/`. Superseded installed packages were backed up under `artifacts/install-backups/` before removal from Mods.

Do not overwrite delivered ZIPs to refresh documentation. v0.1.12 and earlier outputs remain preserved. From v0.1.13, the source ZIP includes this canonical handoff and `Verify-Handoff.ps1`; ignored local installation/provenance evidence stays excluded.
