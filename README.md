# Unity Web Cloth

A small Unity package for reusing the tested Obi CPU cloth effect in WebGL2 projects. It contains bounded mouse/touch sphere dragging, an explicit CPU WebGL builder, and an optional interaction sample with browser frame measurements.

Obi Cloth is a separate prerequisite. Its source, shaders and other vendor assets are not distributed by this repository. This is a private local package; no remote repository is configured.

## Install

1. Use a **Unity 6000.3.10f1 URP project** and install **Obi Cloth 7.2** in the consuming project first.
2. Open **Window → Package Manager → + → Install package from disk**, and select this repository's `package.json`. The manifest declares the tested Burst, Collections, Mathematics, Jobs and Input System versions.
3. Enable **Input System Package (New)** or **Both** under Player settings → Active Input Handling; restart Unity if prompted.
4. Select **Unity Web Cloth → Samples → Cloth Interaction → Import** in Package Manager.
5. Choose **Web Cloth → Sample → Open interaction scene**. Enter Play Mode and drag the orange sphere.
6. Choose **Web Cloth → Sample → Build WebGL2 development test**. Serve the generated `Builds/WebCloth` folder over HTTP and open it in a browser.

Unity imports an optional package sample into the consuming project's Assets folder, so it can be edited independently. See [Unity's sample workflow](https://docs.unity.com/en-us/engine/6000.3/manual/packages-list/cus-pkg-lp/cus-pkg-development/cus-samples).

## What is reusable

- `Runtime/SphereDrag.cs`: camera-facing dragging with an initial grab offset, viewport bounds, speed limiting, a configurable floor height, and cancellation on resize, focus loss or touch cancellation. Assign a camera, kinematic Rigidbody and SphereCollider in the Inspector or call `Init` before Start. Use a centered SphereCollider with SphereDrag and Rigidbody on the same GameObject. For cloth collision, add the Rigidbody before adding ObiCollider, or duplicate the configured sample sphere. The sample uses an orthographic camera.
- `Editor/CpuWebClothBuild.cs`: an explicit WebGL2 build entry point with CPU backend preflight and scoped removal of Obi compute variants. Ordinary Unity builds are unaffected.
- `Samples~/Cloth Interaction`: a saved scene, mesh, generated blueprint, materials and simple shader; two pinned corners, an 81-particle rag, floor and movable sphere. The sample also contains the optional browser diagnostics plugin and probe.

The sample uses two solver substeps, one maximum solver step per frame, no self collisions, no particle collisions/friction, and no aerodynamics. Sphere motion uses a kinematic Rigidbody with interpolation and `MovePosition`. These are starting settings for a tiny rag.

The sample shader is self-contained and has no game render-pipeline dependency. The tested web profile uses URP 17.3.0 with an independently generated renderer asset, MSAA disabled, no extra lights, shadows, HDR, depth texture or opaque texture. URP also strips unused vendor graphics variants; building this Obi installation with the built-in renderer caused excessive particle-shader variant preparation. Built-in and other pipelines are outside the verified web-build scope.

## Build an existing cloth scene

Call the build wrapper from an editor script:

```csharp
var report = WebCloth.Editor.CpuWebClothBuild.Build(new UnityEditor.BuildPlayerOptions
{
    scenes = new[] { "Assets/MyClothScene.unity" },
    target = UnityEditor.BuildTarget.WebGL,
    locationPathName = "Builds/MyCloth",
    options = UnityEditor.BuildOptions.Development
});
```

For solvers created from prefabs at runtime, pass every solver prefab explicitly as the second argument to `Build(options, runtimeSolverPrefabs)` or `Validate(options, runtimeSolverPrefabs)`. The list accepts saved root prefab assets and checks all their solvers, including inactive children and nested prefab contents. A selected scene can contain no solver if the declared prefabs provide one. Nulls, scene instances, child objects, solver-free declarations and Compute solvers are rejected. The original one-argument overloads remain available for scene-only builds.

Use this entry point only for a build in which **every Obi solver uses the CPU backend**, including solvers instantiated from prefabs, Resources or Addressables later. Preflight checks serialized solvers in the selected scenes and declared prefabs, including inactive objects. It does not discover undeclared Resources/Addressables prefabs or solvers constructed in code. Compute-based Obi effects cannot work in this build because their compute variants are removed.

The wrapper temporarily selects OpenGLES3/WebGL2, then restores graphics settings and shader-strip scope in `finally`. It does not edit Obi files. The sample's build command temporarily uses Unity's default template and disables compression for a simple local HTTP server, restoring both settings afterward. Its generated HTML gets a small CSS override so the test canvas fills the window while resizing; the generic build wrapper leaves your own template alone.

## Verify in an independent project

```sh
python3 "Tools~/verify.py" \
  --unity "/path/to/Unity.app/Contents/MacOS/Unity" \
  --obi-source "/path/to/your/Assets/Obi" \
  --web
```

This creates a dedicated consumer in the system temporary directory, copies your locally installed Obi there, installs this package by local file reference, and imports the sample. No game project, assets or utility scripts are required. It checks CPU build scoping, rejects non-web targets and Compute solvers, then checks cloth loading, pins, mouse/touch movement, final release and resize cancellation at phone, tablet, square and ultrawide sizes. Editor play runs unfocused using synthetic input and restores physical-device state on exit.

Results and screenshots are saved to ignored `Reports~`; the web player is saved to ignored `Builds~/WebCloth`. To serve it locally:

```sh
python3 -m http.server 8000 --bind 127.0.0.1 --directory "Builds~/WebCloth"
```

Open `http://127.0.0.1:8000` and drag the sphere. The overlay shows whole-player average, p95 and worst frame times over the last 120 sampled frames, and cumulative idle/drag averages since startup. Startup and focus-return stalls are excluded. These numbers include rendering, simulation, input and the probe itself; a vsync-capped average cannot isolate cloth CPU cost.

## Tested scope

The source experiment ran the same 81-particle interaction in WebGL2 on a desktop browser at approximately 60 FPS. Its settings and behavior are preserved here. Extraction verification results are recorded in [Documentation~/Validation.md](Documentation~/Validation.md).

A low-end phone benchmark is still required before using this as a device performance budget. Browser touch hardware, multiple cloth actors, more particles, release builds, other Obi/Unity versions and WebGPU are outside the tested scope. Obi's CPU and GPU implementations have different requirements; see the [Obi backend documentation](https://obi.virtualmethodstudio.com/manual/7.0/backends.html).

## Repository boundaries

The Unity package is at the repository root. `Samples~`, `Documentation~` and `Tools~` hold optional source assets, documentation and local verification tools. Generated projects remain outside the package; builds, logs and caches are ignored. No Obi vendor files or original game assets are tracked.

The initial source was extracted from the authored cloth prototype at source commit `5dd868f29`. It has a new namespace, assemblies and asset GUIDs, so the package can coexist with that experiment. Future changes should be verified here before being copied into game-specific integrations.
