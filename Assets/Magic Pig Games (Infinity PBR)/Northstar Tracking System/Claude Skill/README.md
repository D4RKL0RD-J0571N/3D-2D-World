# Northstar — AI Agent Skill

This folder bundles a **Claude Code / Cowork Skill** that teaches AI coding
agents how to use, set up, and extend **Northstar – Nav & Tracking System** —
its compass, radar, screen + edge overlay, navigation (compass) bar, tracked
targets, icon graphics, settings, distance text, and runtime API.

Once installed, you can ask your agent things like:

> "Add a compass and a screen overlay to my scene."
> "Track this enemy on the compass and show a distance label under its icon."
> "Make quest objectives show an off-screen edge arrow that fades with distance."
> "Turn the player's gold into a gold-colored marker on the navigation bar."
> "Write a custom distance text that says 'CLOSE / FAR' instead of meters."
> "Build a Berries-style pickup with a hold-to-collect progress bar on the icon."

…and it will know the Northstar architecture (the two parallel systems and their
tracked-target components), add the right components, wire the settings, drive
the Unity Editor to build it for real, override graphics, and write pool-safe
custom logic by subclassing the right base.

## What's in here

```
Claude Skill/
├── README.md               ← you are here
├── install.sh              ← copies the skill into your project's .claude/skills/
└── northstar/              ← the skill itself
    ├── SKILL.md            ← the entry point the agent reads
    ├── references/         ← deep-dive docs (architecture, setup, tracking, settings, displays, runtime, custom logic)
    └── scripts/            ← optional editor helper (NorthstarAgentTools)
```

The skill is plain Markdown. **Claude Code and Cowork** discover it automatically
once it lives in a `.claude/skills/` folder. **Other AI tools** (Cursor, Copilot,
etc.) can be pointed at `northstar/SKILL.md` and the files in
`northstar/references/` directly — it's all readable docs.

## Install

From a terminal **inside your Unity project**:

```bash
bash "Assets/Magic Pig Games (Infinity PBR)/Northstar Tracking System/Claude Skill/install.sh"
```

That copies `northstar/` into `<your project>/.claude/skills/` so Claude Code
picks it up for this project. To install it for **every** project on your machine
instead, add `--global` (installs to `~/.claude/skills/`):

```bash
bash ".../Claude Skill/install.sh" --global
```

Manual install (any OS): copy the `northstar/` folder into either
`<your project>/.claude/skills/` or `~/.claude/skills/`.

## Verify it's working

Start your agent in the project and ask: *"What can you do with Northstar?"* If the
skill is installed, it will describe the two tracking systems (Compass/Radar and
Screen Overlay/Edge/Navigation Bar), making objects trackable, overriding icon
graphics, and writing custom distance text / icon logic.

## Best results: connect the Unity MCP

The skill can build Northstar setups directly while the Unity Editor is open by
running editor C# through the **Unity MCP bridge** (it instantiates the compass /
radar / overlay prefabs, adds the tracked-target components, and creates settings
assets for real). It also has a no-bridge fallback (it writes a small editor
script you run from a menu), so the skill still works without it — the MCP just
makes the loop instant. See `northstar/references/setup-and-scene.md` for setup.
