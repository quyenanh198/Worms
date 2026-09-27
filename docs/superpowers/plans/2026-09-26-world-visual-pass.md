# World Visual Pass Implementation Plan

> **For agentic workers:** Implement inline in this session, one task at a time. Verify each task before starting the next.

**Goal:** Bring the playable world's palette, terrain, sky, sea, and background depth closer to the user-approved cartoon reference in `docs/visuals/approved-cartoon-reference.png`.

**Architecture:** Keep the existing procedural rendering system and WebGL2-compatible URP shaders. Extend the theme data, tune terrain's large-scale graphic detail, and add inexpensive decorative shapes behind the gameplay plane. The simulation, terrain mask, and collision stay unchanged.

**Tech Stack:** Unity 6000.3.25f1, C# 9, URP Forward, HLSL, Shuriken, WebGL2.

## Global Constraints

- Keep the game playable on WebGL2 and native with the same visual content.
- Do not add paid or attribution-restricted assets. Record any new external asset in `assets-src/CREDITS.md`.
- Decorative geometry must not change simulation or cover active worms and projectiles.
- Low tier must preserve foreground clarity; avoid dense high-frequency shader detail.
- Do not import/save the project with Unity 6000.6 into the source tree; the project specifies 6000.3.25f1.

---

### Task 1: Palette and material control

**Files:** `client/Assets/Game/Render/Theme.cs`, `client/Assets/Resources/Shaders/Terrain.shader`, `client/Assets/Resources/Shaders/Water.shader`.

**Deliverable:** Meadow and Beach have stronger warm foreground versus cool background separation. Terrain detail uses two scales with controlled contrast, and the grass lip is a visible graphic band. Sea colors support the blue depth in the reference.

- [x] Record baseline shader colors and capture a baseline image if a Unity player is available.
- [x] Add explicit terrain detail parameters to `Theme` and set them through `Materials.Terrain`: grass-band width, broad mottling scale/strength, pebble strength. Set distinct Meadow and Beach values.
- [x] Update `Terrain.shader` so broad mottling changes color gently and sparse pebbles are anchored in world space. Keep crater cut faces shaded by the same depth logic.
- [x] Tune `Theme.Meadow` and `Theme.Beach` foreground, sky, hills, sea, sunlight, and fog colors against the reference; do not make both biomes identical.
- [x] Compile the Unity C# runtime check (`dotnet build tools/unity-check/Game`) and the .NET suite (`dotnet test Worms.slnx`). Review shader includes and properties because the compile check does not compile shaders.

### Task 2: Background depth and decorative silhouette

**Files:** `client/Assets/Game/Render/SceneBuilder.cs`, `client/Assets/Game/Render/Theme.cs`, `client/Assets/Game/Render/UrpSetup.cs` if tier settings need adjustment.

Additional files used after reviewing actual game renders: `client/Assets/Game/Core/SurfaceDecorMesher.cs`, `client/Assets/Game/Core/EmbeddedStoneMesher.cs`, `client/Assets/Game/Render/TerrainView.cs`, `client/Assets/Resources/Shaders/BackdropSprite.shader`, `client/Assets/Resources/Shaders/EmbeddedStone.shader`, `client/Assets/Resources/Backdrop/cloud-bank.png`, `client/Assets/Resources/Backdrop/distant-island.png`, `client/Assets/Resources/Backdrop/midground-island.png`, and focused .NET tests.

**Deliverable:** The world has an identifiable sea horizon and layers of cool cliffs or hills, with a few simple silhouette props outside the gameplay plane. The center remains open for projectile motion.

- [x] Keep three background depth layers with differentiated colors and heights. The fixed-seed desktop captures show the near/middle/far separation; camera extremes 9 and 60 remain to be checked.
- [x] Add two deterministic distant-island billboards containing painted cliffs, trees and ruins. Place them behind terrain and actors; disable shadows and collision. Primitive tower silhouettes were removed after visual review.
- [x] Ensure far props use shared materials/meshes where possible and do not add per-frame allocations.
- [x] Add a painted cloud bank matching the approved reference; keep real alpha, no collision, and one batched background mesh.
- [x] Add sparse grass tufts from exposed terrain cells and rebuild them with carved chunks. The focused test must fail before implementation and pass after it.
- [x] Add sparse stone inlays on solid front faces and one painted island between the distant hills and battlefield. Review fixed-seed Beach and Meadow camera captures without opening a visible game window.
- [x] Compile native and WebGL runtime checks if Unity assemblies are available. Inspect scene objects and shader property names; run `dotnet test Worms.slnx`.

### Task 3: Visual verification and hand-off update

**Files:** `docs/GRAPHICS_HANDOFF.md`; screenshots under `docs/visuals/` only if actual Unity renders are captured.

**Deliverable:** A before/after record for Meadow and Beach, with known limitations stated plainly.

- [x] Build a disposable copy in Unity 6000.6.3f1 and capture both biomes. The project's required 6000.3.25f1 editor remains unverified. Never label generated concept art as a game render.
- [ ] Inspect desktop 16:9 and a narrow mobile layout at Low and High tier for worm contrast, crater legibility, sky/sea separation, and distracting props. Desktop Low/High and a Windows portrait HUD were captured; browser mobile and repeated-crater states remain.
- [ ] Record FPS and GPU frame time if a mobile browser target is available. If not, leave mobile performance unverified in the hand-off.
- [x] Run `dotnet test Worms.slnx`, native/WebGL C# compile checks, and `git diff --check`. A disposable Unity 6000.6.3f1 Windows and WebGL build also succeeded; required Unity 6000.3.25f1 and browser runtime remain unverified.

**Next plan:** Character clarity and expressive worms, then HUD layout and menu polish. These are separate reviewable passes in `docs/GRAPHICS_HANDOFF.md`.
