# Changelog

## 0.1.1

- Add explicit runtime-solver prefab declarations to the CPU WebGL build and validation APIs, preserving the original overloads.
- Validate inactive nested solvers in saved root prefab assets and reject invalid or solver-free declarations.
- Verify runtime-only scenes, Compute prefab rejection and graphics/scope preservation on rejected builds.

## 0.1.0

- Extract the working 81-particle Obi CPU cloth interaction into an independent Unity package.
- Add reusable mouse/touch sphere dragging and an optional sample with live browser frame timings.
- Add an explicitly scoped WebGL2 CPU build wrapper with solver preflight and graphics-setting restoration.
- Add an isolated consumer verification workflow without game dependencies.
