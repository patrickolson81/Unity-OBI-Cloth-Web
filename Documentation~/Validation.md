# Extraction validation

The target baseline is Unity 6000.3.10f1, Obi Cloth 7.2, Input System 1.18.0, Burst 1.8.30, Collections 2.6.8, Mathematics 1.3.3 and Jobs 0.70.0-preview.7. The consumer project uses a small, independently generated URP 17.3.0 profile. The profile also satisfies the assembly references in this installed Obi copy and avoids excessive unused particle-shader variants seen with the built-in renderer.

The package is verified through a separate generated consumer project installed with a local Package Manager dependency. Only the user's local Obi installation, this package, its imported sample and the verification harness are present. The saved sample retains the two pins, particle count, camera and solver settings from the original experiment. A small self-contained shader replaces the source project's URP materials.

Local verification output is generated under `Reports~`. Browser captures and measurements are local evidence, excluded from the package's source distribution. The browser test must confirm finite particles, fixed pins, visible cloth movement, sphere grabbing and release, and no console errors while interacting.

Low-end phone performance and physical touch remain unmeasured. The overlay reports whole-player frame duration rather than solver-only CPU time. Keep these limits attached to any measured frame rates.


## Acceptance checks

The independent editor run passes at 360×640, 768×1024, 800×800 and 2560×1080. It verifies 81 finite particles, two fixed corners, visible sphere and cloth rendering, mouse/touch dragging, final release position, full sphere visibility, and cancellation on resize. At 800×800 it also verifies off-center grabs toward both viewport edges without a pickup jump. Particle and pin validity are checked after interaction as well as before it.

The CPU build guard accepts the saved CPU scene, rejects a non-web target and a saved Compute solver, preserves compute variants outside its explicit scope, strips the intended Obi compute variants within scope, and leaves an unrelated compute shader alone.

The runtime-prefab extension accepts a solver-free scene with an explicitly declared CPU prefab containing an inactive solver. It rejects missing declarations, inactive nested Compute solvers, solver-free prefabs, nulls, scene instances and prefab child objects. Rejected builds preserve the existing graphics selection and shader-strip scope.

The verification runner rejects overlapping package, Obi-source and consumer directories before writing. Each run archives prior reports and requires new PASS/Succeeded result files, so old screenshots or success messages cannot stand in for a failed run.


## Browser result on 29 October 2026

The independent URP WebGL2 development build succeeds with zero reported build errors. Its uncompressed build report is 119,960,803 bytes; this is a development artifact, not an optimized release download.

The standalone browser test passes 12 grabs, including 360×640, 768×1024, 800×800 and 2560×1080 viewport checks. The canvas fills each viewport and the sphere remains interactive. The desktop run reports approximately 16.6 ms per dragging frame, with 81 finite particles, two fixed pins and no browser warnings/errors. These are whole-player, vsync-influenced frame times. They do not establish physical phone performance.

Version 0.1.1 supports explicitly declared runtime solver prefabs as well as serialized scene solvers. Undeclared dynamically loaded solvers remain the consumer's responsibility. The sample's fixed corners are blueprint inverse-mass pins; there is no authored hand attachment or car contact adapter in this package.
