# Electric Glider Mk II — 0.1.23

An add-on for Vintage Story 1.22.7 and Electrical Progressive Equipment 3.2.3, Core 3.3.0, Basics 3.3.1, and their dependencies. Install on both client and server.

For project continuation, start with [PROJECT.md](PROJECT.md). It records current decisions, validation gates and the distinction between installed and loaded versions. See [VERIFICATION.md](VERIFICATION.md) for coverage limits.

## Source and licensing

Source repository: [tannerjriddle97/ElectricGliderMkII](https://github.com/tannerjriddle97/ElectricGliderMkII). No open-source license has been selected. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for dependency and asset attribution. Game/mod dependencies and private previews are not included in this repository.

## Appearance and thrust light

The Mk II has its own armor-inspired model: blue panels matching EP's electric armor, a steel chassis, zinc edging, copper power feeds, twin vented thrusters and swept reinforced wings with paired Mk II bars. Four original shapes cover the stowed pack, both deployment steps and full flight. Wing sections fold beside the pack, and every visible panel has thickness. The same model appears on the player, in inventory and when held/stored.

Charged Shift boost and powered lift add warm orange native entity light (HSV 4, 6, 11). It stops on release, depletion, unequipping or ineligible movement; passive glide and S braking alone do not light it. Stronger existing lights remain. Small nozzle faces have a low permanent glow for readability; they do not cast permanent light. No torch, world block, extra entity or inventory item is created.

VSPT's installed entity-light discovery recognizes this light. Canonical steel/zinc/copper texture references allow installed material packs to identify them; the wings avoid paper-thin/transparent surfaces. Actual roughness/reflections depend on the active material pack. Offline checks verify shape loading, attachment and managed light discovery; visual quality, shadowing and brightness need an in-game review.

## Flight behavior

During Mk II gliding or powered lift, the local first-person body, arms and held items are hidden to keep them out of the camera while banking. Normal on-foot rendering, third person, other players and shadows are preserved. v0.1.22 checks the actual camera mode and covers direct body/item draw calls; it also applies camera exclusion to the optional VSPT renderer when its supported hooks are present. This addresses rendering coverage gaps; the reported clipping still needs an in-game retest.

The Mk II has a **35 blocks/second normal total speed cap**, rising to **45 while continuously boosting with charge**. A lower EP speed setting still takes precedence. Level coasting gradually slows toward 12 blocks/second; dives build speed toward 35, and unpowered pull-ups shed speed. Dive momentum carries into level flight and gradually bleeds off. Holding Shift builds from 12 to 45 in about 3.7 seconds. Releasing Shift immediately restores the 35 cap, then level coasting sheds about 2.5 blocks/second each second.

The Mk II advances its native airspeed scalar by elapsed time and applies directional thrust and speed limits after the complete force pipeline, before terrain collision. Vanilla and EP retain movement, steering, banking, camera effects, sounds and particles. The original electric glider is unaffected.

**Shift adds thrust in the direction you face**, including upward, downward and sideways headings. The added acceleration is 9 blocks/second squared along your look vector. It adds to existing motion; it does not set a vertical speed or force a climb angle. Gravity, native flight forces and the total speed cap still affect the resulting path.

Deployment uses native downward motion immediately, subject only to the total speed cap. There is no startup downward-acceleration guard, hover period or separate descent-speed cap. A separate two-second safeguard prevents passive deployment from creating an upward launch; it does not limit downward acceleration or slow descent.

Releasing Shift starts coasting from actual momentum rather than an unused higher boost-speed setting. Level/upward coasting cannot gain a sudden horizontal impulse from releasing or leveling out after a climb. If horizontal speed is below 12, it can recover gradually at up to 6 blocks/second squared. Boosting again or aiming into a dive restores the usual acceleration behavior.

Equipping the Mk II in a backpack slot prevents fall damage, even while folded or out of charge. Other damage types still apply. Carrying it in ordinary inventory does not provide protection.

The original electric glider is unchanged. The Mk II retains six storage slots, 4,000 charge units / 80,000 J (four times the base original), and the agreed upgrade recipe.

## Controls

Equip the Mk II in a backpack slot. **Space** deploys it while airborne. **Sneak/Shift** pushes along your look direction; aim up to climb or down to dive.

**Hold S** (your backward movement binding) while gliding for reverse thrust: 6 blocks/second squared opposite your look direction, two-thirds of Shift thrust. It acts as a brake and lets you slow below the normal cruising speed. Release S to recover cruise speed gradually. S opposes Shift when both are held. During lift mode, S remains ordinary backward positioning. Braking adds no separate battery charge; normal boost/lift billing is unchanged.

Your first-person arms and held items are hidden while gliding with the Mk II or using lift. They reappear as soon as flight ends. This also hides the local body in immersive first-person mode during flight. Third-person views, other players, shadows and the original glider keep their normal rendering. No graphics settings are changed.

**Hold Shift+Space** for the separate vertical-lift function: rise at up to four blocks per second, with upright WASD positioning at up to four blocks per second. Forward/backward and left/right match normal walking controls. Lift keeps the glider folded. Release either key to stop powered ascent; release and press Space without Shift to deploy afterward. Lift can be rebound under Settings > Controls > Mk II glider: vertical lift (hold). Existing saved bindings are preserved; if yours still shows V, change it there or reset that binding to its new default. Use a keyboard binding in this version.

Lift requires a live, unmounted player outside liquids, climbing and creative free-flight. It stops on release, loss of mouse capture, loss of charge, or unequipping. Multiplayer lift requests expire without renewed input.

## Crafting

The Mk II handbook page shows the game's native 3x3 recipe grid under **Created by → Crafting**, with ingredient quantities and links to their handbook entries.

| | | |
|---|---|---|
| Linen x4 | Accumulator (128V) | Linen x4 |
| Steel plates x2 | Glider schematic (reusable) | Steel plates x2 |
| Rotor | Empty | Rotor |

The glider schematic is required but is not consumed, matching the standard glider recipe. All other ingredients are consumed. A 32V accumulator cannot substitute. Existing charge from the 128V accumulator is transferred, capped at 80,000 J. Excess charge is lost; the accumulator need not be charged. The new glider starts with six empty storage slots. Existing crafted Mk II gliders keep their contents and charge when updating.

## Energy and charging

At EP's default GliderDurabilityLossAmount of 15, powered level flight consumes 22.5 charge units / 450 J per second, climbing scales up to 30 units / 600 J per second, and V lift consumes 45 units / 900 J per second. V replaces boost cost while active. Passive gliding consumes no charge. These rates scale with EP's energy setting. A server-side fractional meter bills the Mk II; EP's original meter continues billing original gliders without double-charging the upgrade.

Charge changes use small server-to-owner messages instead of repeated backpack inventory updates. One initial inventory update establishes a per-stack identity; later charge messages update only charge/fraction/sequence attributes, avoiding the client's bag reload callbacks. Delayed messages cannot update a different glider or roll back a newer charge value. Normal item/storage changes still use the native inventory protocol.

Charge in the standard EP charging station. An empty Mk II remains usable as an ordinary glider, subject to EP's existing empty-battery behavior.

## Installation and updating

Place `electricglidermk2_0.1.23.zip` in the game's Mods folder alongside EP. Remove the older Mk II ZIP, keep the new file zipped, then restart the game. Update client and server together; use matching versions. Existing crafted Mk II gliders keep their charge and contents. Handbook name: **Electric Glider Mk II**; code: `electricglidermk2:glider-mk2`.

The add-on ships original Mk II shapes and references installed game/EP textures and boost sound. It does not redistribute those textures, sound or third-party models.

## Changes in 0.1.23

- Original armor-inspired pack, twin thruster pods and folding reinforced wings across all four deployment states.
- Conditional orange entity light during charged boost/lift, including VSPT's existing entity-light discovery.
- Includes pending v0.1.22 camera-visibility improvements and v0.1.21's doubled backward thrust.

## Changes in 0.1.22

- Uses actual camera mode and guards direct player/base body and held-item draws during first-person flight. Includes optional renderer camera exclusion without changing shader settings.

## Changes in 0.1.21

- Doubles S reverse thrust and braking from 3 to 6 blocks/second squared. Holding Shift+S now produces net forward thrust/airspeed acceleration of 3.

## Changes in 0.1.20

- Guards the glide-physics hook against missing backpack/item/API/mod-system state. Uses one checked item snapshot instead of repeating inventory lookups and dereferencing the result.
- Leaves native flight running if the add-on context is unavailable, then resumes with a fresh session when it returns. Flight tuning and controls are unchanged.

## Changes in 0.1.19

- Adds weak reverse thrust while holding the backward movement key (S by default) during gliding, at one-third of Shift thrust.
- Lets braking reduce speed below cruise; releasing S recovers speed gradually. Holding Shift+S reduces net thrust and acceleration.
- Lift mode retains ordinary backward positioning; other flight limits and energy rates remain unchanged.

## Changes in 0.1.18

- Changes the default lift hotkey from V to Shift+Space, registered before ordinary Space handling. Either Shift key works; releasing either required key stops lift. Existing saved rebindings are preserved.
- Includes the pending v0.1.17 boost-release and startup-descent fixes.

## Changes in 0.1.17

- Removes the startup downward-acceleration guard; native descent begins immediately. Passive upward-launch prevention remains separate.
- Rebases coasting on actual speed when boost ends and prevents release/climb-leveling from adding a sudden horizontal kick.
- Preserves 35/45 caps, directional Shift thrust, V movement, fall immunity, charge/storage and energy rates.

## Changes in 0.1.16

- Raises the normal total speed cap to 35 blocks/second and continuous boost to 45.
- Doubles V horizontal positioning from 2 to 4 blocks/second; diagonal input shares that horizontal cap. Upward V speed remains 4.
- Retains directional Shift thrust, coasting, startup gravity guard, fall immunity and energy costs.
## Changes in 0.1.15

- Replaces special upward climb assistance with added thrust along the full look direction. Removes the separate 10-block upward speed target/cap and forced climb-angle handling.
- Allows immediate descent on deployment, preserving existing fall speed within the total cap. Removes the hover period and separate 8-block downward cap.
- Keeps a two-second downward-acceleration guard at current game gravity, including downward boost.
- Preserves 25/30 speed caps, coasting/dive progression, V lift, charge/storage and fall immunity.

## Changes in 0.1.14

- Raises the normal cap to 25 blocks/second and continuous boost to 30.
- Adds time-based airspeed progression: level coasting toward 12, acceleration from dives/boost, and speed loss on unpowered pull-ups.
- Reduces Shift climb acceleration from 12 to 6 blocks/second squared and caps powered ascent at 10 blocks/second. V lift remains unchanged at 4.
- Retains fall immunity, two-second descent protection, charge sync, storage and energy costs.

## Changes in 0.1.13

- Replaces repeated charge-driven backpack updates with lightweight charge synchronization, removing the client's bag reload work from sustained V lift.
- Preserves V lift speed, acceleration, energy cost and all existing glide behavior.
- Rejects stale/wrong-stack charge messages and synchronizes depletion immediately.
- Adds offline lift, heartbeat, billing and client-handler regressions. Rendered smoothness still requires a playtest.

## Changes in 0.1.12

- Prevents passive Space deployment from generating an upward launch during the first two seconds.
- Allows existing upward momentum to slow normally instead of being held by downward protection.

## Changes in 0.1.11

- Restores vanilla/EP flight movement instead of replacing the air controller and erasing EP's velocity changes.
- Retains the native forward launch impulse; deployment protection affects downward motion only.
- Applies speed limits, two-second dive protection and powered climb assistance once after the complete physics force pipeline.
- Adds offline regression checks through the actual Harmony-patched vanilla/EP physics methods, including fall protection and the original glider.

## Changes in 0.1.10

- Dive protection now lasts only for the first two seconds after each deployment. The total speed cap, fall immunity and climbing thrust remain unchanged.

## Changes in 0.1.9

- Limits total glide speed to 20 blocks/second and downward speed to 8 blocks/second.
- Arrests falls on deployment and eases into descent instead of stacking vanilla and EP downward acceleration.
- Adds powered thrust for sustained 60° climbs.
- Prevents fall damage while equipped, regardless of battery charge or deployment.

## Changes in 0.1.8

- Crafting uses the reusable glider schematic instead of the electric glider, which cannot be placed in the crafting grid.
- The real crafting-slot insertion rules are checked for every ingredient.
- New gliders transfer charge from the 128V accumulator and start with six empty storage slots.
- Flight controls, lift and upright animation behavior are unchanged.

## Build and tests

Requires a .NET 10 SDK and matching game installation. No NuGet packages are required; game and dependency assemblies are not redistributed.

```powershell
./build.ps1 -DotnetPath 'C:\path\to\dotnet.exe' -GamePath 'E:\Games\Vintagestory' -OutputPath 'C:\path\to\output'
```

The script builds, runs glide/climb/descent/lift/energy tests, validates JSON and produces mod/source ZIPs. It does not install or launch the game.

To also run offline checks through the actual patched game/EP physics methods, provide `-PhysicsReferencesPath 'C:\path\to\private-ep-dlls'`. That directory must contain the matching installed EP Core, Basics and Equipment DLLs with their original filenames. These local dependencies are not included in source or release ZIPs. The checks use controlled inventory/world test doubles and do not start a game/server or load a save.

`probe/` contains an optional server-only test mod. Build it and install its DLL and modinfo only in a separate test profile's Mods/glidermk2probe directory. Set `GLIDER_MK2_TEST_REPORT` to an absolute JSON path and start the dedicated server against that profile. The probe compares the original glider with the unmodified EP method and checks Mk II motion isolation, crafting and V controls, writes results and shuts down. Do not install the probe in a normal play profile. See VERIFICATION.md for coverage limits.
