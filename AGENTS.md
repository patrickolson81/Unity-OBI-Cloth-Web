# Unity Web Cloth development

Keep this a small standalone Unity package. Runtime and editor code must not depend on any consuming game's assemblies, scenes, input helpers, UI or verification framework. Keep the 81-particle interaction example under Samples~ and its diagnostics optional.

Obi Cloth is an external dependency. Never commit Obi source, shaders or vendor assets. Generated consumer projects stay outside the package. Reports~ and Builds~ are local outputs and must stay ignored. Use fresh asset GUIDs for authored package additions.

Run the independent consumer checks with `python3 "Tools~/verify.py" --unity <Unity executable> --obi-source <installed Obi folder> --web` after changing simulation, input or build handling. Inspect browser behavior as well as build results. Report actual device/browser limits; desktop viewport sizing is not a phone benchmark.

Keep verification Play Unfocused. Never foreground Unity or call GameView.Focus. Use the local GameView helper and synthetic input, restore device/settings state, and avoid consuming the user's physical input.

Compute stripping is opt-in for the whole build through CpuWebClothBuild.Build. Validate CPU solvers first, leave ordinary builds untouched, preserve graphics and scope settings on failure, and document that dynamically loaded solvers must also use CPU.

Use a codex/ branch for changes, make coherent commits, and keep the local dev branch as the integration baseline. Do not publish or configure a remote unless requested.
