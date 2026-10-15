---
description: Run any Unity CLI command for this project.
agent: unity
---
Run the requested Unity CLI operation for the current project.

Prefer `unity status` first if the task touches the Editor, scene, prefabs, or assets. Then execute the appropriate `unity` command with `--format json` when the output needs to be parsed.

Args: $ARGUMENTS
