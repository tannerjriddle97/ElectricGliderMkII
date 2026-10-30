# Verification — 0.1.23

## Installation status

v0.1.23 was installed after explicit user approval. The installed SHA-256 matches the tested package; v0.1.20 was backed up and verified before removal, leaving exactly one Mk II ZIP. No game/server launch or rendered playtest was performed. Restart and loaded-version confirmation remain pending. Delivered archives are unchanged by this status update.

## Current source checks

v0.1.23 adds original geometry and thrust lighting. All four shapes deserialize using the game API, resolve their textures/faces and attach to the actual stock seraph UpperTorso using native step-parenting. Checks require positive thickness for every visible element and valid UVs/native texture paths. Stock seraph's pre-existing duplicate attachment-point warnings are tolerated during its own baseline resolution; Mk II resolution/attachment uses strict warning/error handling.

The actual Harmony-patched player LightHsv getter is exercised for boost, release, folded lift, empty battery, stronger existing light, death, water, original glider and unequipping. Optional tests invoke installed VSPT's managed OwnLight discovery and verify positive orange emission during thrust and no emission after release. No Fill/native GPU method, game/server or save is run. Static model previews were rendered directly from actual cuboids/textures; browser opening of local HTML was blocked, so interactive browser rendering was not verified.

v0.1.22 adds first-person rendering coverage. The no-GPU harness invokes actual patched player/base body and held-item methods with cached FirstPerson, ImmersiveFirstPerson and ThirdPerson modes while the real camera is first person. It checks shadow/other-player/third-person exclusions, restoration on foot, folded lift and original-glider isolation. Optional renderer tests load the locally installed scene assembly, install managed hooks, invoke an early-return stopped callback, and verify scope cleanup after normal return and a deliberately injected managed exception. They do not initialize the renderer, call its native frame, or launch the game.

The user confirms clipping existed before shaders. The original live trigger is still unproven. The coverage fix and optional compatibility require rendered retesting during turns; passing these offline checks is not proof the screenshot issue is resolved.

For continuation and outstanding playtests, read [PROJECT.md](PROJECT.md). v0.1.17 removes startup downward damping and fixes a reproduced horizontal surge after boost release. 35/45 caps, directional thrust, V movement and energy rates remain unchanged.

The v0.1.23 build passed 3,218 pure checks and 21,730 offline checks (24,948 total) with both `-PhysicsReferencesPath` and `-RenderReferencesPath`. With physics references alone, the offline suite has 21,722 checks; eight optional-renderer checks require the scene DLL. The build validates JSON and packages results. A build without that parameter does not run the offline pipeline suite. The optional server probe is compiled, not run.

## Deployment crash regression

The user report records NullReferenceException in Mk2System.FlightStepPrefix while deploying during a fall on v0.1.19. The report does not contain a source line or identify the exact null reference. A controlled test returning a backpack on the first lookup but no inventory on subsequent lookups reproduced NullReferenceException at the old final unchecked slot lookup. This establishes a concrete defect in the named method without proving the live trigger.

The updated hook uses one checked item snapshot and verifies API/mod-system/item state. Regression checks cover changing inventory, missing system/API, empty slot, unchanged velocity on fallback and resumed state after recovery. All existing physics, S braking, lift, charge and fall-immunity checks remain. The game was not launched, no save was loaded, and rendered reproduction is still pending.

## S reverse thrust

v0.1.19 pure checks compare reverse thrust against the forward look-vector impulse over seven pitches and four headings and verify Shift+S combines opposing forces. Real-method tests at 20/60/144 Hz brake from cruise toward six blocks/second over one second, then check gradual recovery on release without a surge. The existing lift positioning and billing regressions remain unchanged.

## Lift binding

v0.1.18 registers Shift+Space as the default with RegisterHotKeyFirst. Static inspection confirms the existing held-input path checks the mapped main key and either Shift key; release of either stops the combination. Saved controls remain authoritative, including the old V mapping if saved. No player settings were edited. Rendered Space/lift interaction still needs playtesting.

## Directional thrust, deployment and speed cycle

Pure checks cover equal-strength boost parallel to the look vector at seven pitches (including straight up/down) and four headings, preserving existing momentum below the speed cap. They verify undamped native descent at deployment and around the former two-second boundary, normal jump deceleration and passive upward-launch safety. The former downward-gravity-guard assertions have been retired.

At 20, 60 and 144 Hz, actual Harmony-patched vanilla/EP methods allow immediate native deployment descent, including acceleration greater than the removed gravity guard when aiming down. A new release regression covers 0.2/3-second boost holds at level/60-degree pitch followed by release and leveling. It failed before the fix at 20 Hz with horizontal speed jumping from 7.16 to 13.19 blocks/second in one step; now release cannot add horizontal speed above current momentum except gradual recovery toward cruise at up to 6 blocks/second squared. Pure V checks cover forward, sideways and diagonal horizontal speed at five headings; the real-module V test reaches four blocks/second diagonally while retaining four-block upward ascent and exact billing. They also reach 45 with sustained level boost, drop below 35 on release, coast toward 12, regain 45 after four seconds of boosting, reach 35 in an unpowered dive and shed speed on pull-up. Empty batteries cannot sustain boost; lower EP caps still apply.

Static local inspection found EP adds a fixed GlideSpeed increment per physics call while vanilla subtracts a dt-scaled amount. Merely raising the ceiling did not produce 30-speed boost at 20 Hz. The Mk II now advances the native GlideSpeed scalar using elapsed time; native force modules and steering remain active, with final motion limited afterward. The original glider is unaffected. These tests establish behavior in the offline pipeline, not rendered flight feel, collision behavior or multiplayer synchronization.

## V lift and charge synchronization

Static inspection of the installed game's InventoryNetworkUtil.UpdateFromPacket shows that ordinary slot updates invoke InventoryBase.DidModifyItemSlot. For an equipped bag, InventoryPlayerBackpacks.OnItemSlotModified reloads the bag inventory on the client. Queueing server dirty slots avoids a server player-data broadcast, but still sends the client through that inventory refresh path at each charge debit.

v0.1.13 establishes a stack identity with one native inventory update, then sends server-to-owner ChargeUpdate messages containing slot, stack ID, sequence, charge and fractional use. The client handler updates those attributes directly without dirtying or modifying the inventory slot. Stale sequences, replaced stacks and invalid values are rejected. Normal inventory changes still use native synchronization. Existing charge/storage data is preserved; server billing remains authoritative. Matching client/server versions are required.

The offline suite drives five seconds of actual Harmony-patched V physics with renewed lift requests and 100 server billing ticks. Each charge message is serialized/deserialized with the game's protobuf library and passed to the actual client handler using controlled player/world/inventory doubles. Checks verify monotonic ascent to four blocks/second, unchanged 45-unit/second billing, one initial dirty-slot update instead of per-debit updates, no heavy server/client inventory callbacks, matching client/server charge, stale/wrong-stack rejection and immediate depletion.

This removes a demonstrated source of repeated inventory work. It does not establish that every reported visual hitch is caused by charge synchronization. The live client log also contains render-thread stalls. No rendered v0.1.13 smoothness test has been performed by the agent.

## Other regression coverage and runtime status

The real-method suite retains checks for native forward launch, upward-facing boost, total speed, undamped startup descent, passive launch prevention, original EP glider behavior and equipped/folded/empty fall immunity. Pure checks retain flight-limit, lift and fractional energy coverage at multiple physics rates. Gravity and drag bypasses remain limited to V lift; normal glide limits wrap the complete native force pipeline before collision integration.

v0.1.21 doubles S reverse thrust to six and updates the existing direction/combined-thrust/braking expectations. v0.1.20 remains installed; v0.1.21 needs installation approval and rendered testing. The crash report confirms the older v0.1.19 loaded. Installation hashes and exact machine paths are maintained in ignored `.local/HANDOFF-LOCAL.md`. Delivered archives remain unchanged by this installation-status documentation update.

No game/server or save is started by the offline tests. Terrain collisions, the actual modpack's rendering and multiplayer remain outside their coverage. The preserved v0.1.10 DLL failing the forward-launch regression remains evidence for the earlier controller correction, not a full rendered reproduction of the reported freeze.

## Historical verification — 0.1.8

The following results describe the recovered 0.1.8 package. They do not validate the current revision.

Built against Vintage Story 1.22.7 and .NET 10.0.7. The test profile contains a copy of the installed modpack; existing play saves were not opened.

### First-person flight rendering

The Mk II suppresses EntityPlayerShapeRenderer.DoRender3DOpaque and DoRender3DOpaqueBatched only for the local player in first-person or immersive first-person mode, during Mk II gliding or V lift, outside shadow passes. It skips the camera body/arms and held-item draw without changing graphics settings or animation data. Tests cover local/remote, both first-person modes, third person, shadows, immediate restoration after gliding, V lift and the original glider. The actual Harmony-patched renderer entry points are invoked on a GPU-free test renderer to verify hidden draws return before accessing graphics state. Visual presentation still requires a rendered client playtest.

### Original-flight comparison

The integration probe creates a Harmony reverse patch from the **original, unmodified EP ApplyFlyingPhysics method**. It compares that baseline with the installed patched method across three pitches, three headings, and boost on/off.

Verified:

- Original electric glider motion and boost increment match the original EP method.
- Mk II coasting motion matches the original EP method.
- Mk II powered flight gains additional thrust along the original EP thrust direction.
- The boost airspeed increment is unchanged.
- The normal player-air module has no replacement flight controller from this add-on.
- The original EP speed limiter remains effective across a sustained flight sequence.
- EP recognizes the Mk II, including alongside an original glider.
- EP's original energy meter does not double-charge the Mk II's separate fractional meter.

The only normal-flight modification is a conditional 1.5x multiplier at EP's existing scalar thrust-magnitude calculation. No camera-direction replacement, steering controller, velocity controller, banking replacement, gravity replacement or drag replacement runs during normal gliding/boosting. Gravity and drag overrides are limited to the separate V-lift mode.

### Schematic recipe regression

The handbook's native Created by selector requires the recipe output attributes to be a subset of the handbook stack's attributes. A forced durability=0 on the recipe output prevented that match. The recipe now leaves output attributes unset; OnCreatedByCrafting still explicitly initializes charge from the consumed battery, including zero for an empty battery. Integration checks use the actual GetHandBookStacks result and native Satisfies predicate, verify charged entries also match, and reproduce the old mismatch. This enables the game's native SlideshowGridRecipeTextComponent without a custom UI patch. A rendered client was not used to inspect the grid visually.

The center ingredient is the standard glider schematic. Every nonempty ingredient is checked with the actual InventoryCraftingGrid slot's CanHold rule, not only recipe matching. Tests verify that the old electric glider no longer matches, the proper 128V battery is required, materials are consumed while the schematic remains, and new gliders receive six empty slots with battery-only charge transfer and capacity clamping.

### Upright lift pose regression

Tests inject stale server-side glide flags, body pitch/roll and active first-/third-person glide animations while V lift is active. The pose helper clears those flags and animations. A separate check confirms normal flight flags and body pitch are untouched after V release and manual glider deployment. The game's animation tick runs the helper before selecting animations. Actual visual presentation still requires a rendered playtest.

### Charge synchronization regression

The game inventory code shows that ItemSlot.MarkDirty invokes InventoryPlayerBackpacks.OnItemSlotModified, which reloads bag contents and broadcasts player data on the server. Mk II charge deductions now call InventoryBase.MarkSlotDirty directly instead. Regression checks confirm that charge changes still enter the inventory synchronization queue, but repeated lift deductions never invoke the heavy item-slot-modified callback. This removes a concrete source of unnecessary player refreshes; a rendered playtest is still required to confirm the reported camera hitch is resolved.

### Lift, storage and crafting

The copied full-modpack integration run passed 335 checks, including repeated speed-cap assertions. These cover the comparisons above, native handbook recipe selection, the actual recipe and consumption, battery charge transfer, six empty saved storage slots, server billing, upright V movement in four directions at five headings, grounded takeoff applicability, V release without deploying or arming forward flight, manual Space deployment and menu focus.

Pure tests separately cover lift speed and distance at 20/60/144 tick rates, no automatic horizontal motion, normal walking directions, fractional charge accounting, mode costs and zero-charge clamping.

### Limits

The harness uses real loaded game/mod methods with controlled player, inventory and input test doubles. It does not run a rendered client, automate desktop input, inspect visual presentation, or fly through terrain. Normal playtesting is still needed for feel, terrain collision and network latency. Raw results accompany the deliverables.
