---
description: Build the Unity project via the Unity CLI.
agent: unity
---
Build the current Unity project using `unity build`.

1. Determine the desired target platform from $1 (default to the current platform if not specified).
2. Run `unity build . --target <TargetPlatform> --format json` with any extra args from $ARGUMENTS.
3. Report the build result and log location.
