# ClickToMove

Click to move for FFXIV via Dalamud. Hold Shift and left-click the 3D ground
to walk there. Uses vnavmesh pathfinding when available,
straight line fallback otherwise.

## Install

1. Install vnavmesh for pathfinding (optional but recommended):
   add `https://puni.sh/api/repository/veyn` as a custom plugin repository
   in Dalamud settings, then install vnavmesh from it.
2. Build this plugin in Debug and find it in
   `%APPDATA%\XIVLauncher\devPlugins\ClickToMove\`
   (the build copies it there automatically), or add the DLL path under
   Dalamud Settings > Experimental > Dev Plugin Locations.
3. Enable it in the Plugin Installer under Dev Tools > Installed Dev Plugins.
4. Hold Shift and left-click the ground. Your character walks there.

## Usage

- `Shift + Left-click` on 3D ground: move to that spot.
  Plain clicks without the modifier behave exactly like vanilla targeting.
- `Esc` or pressing `WASD` cancels a Direct move.
- New click replaces the current move.

## Slash commands

- `/ctm` or `/ctm config` - open settings
- `/ctm on` - enable the plugin
- `/ctm off` - disable the plugin
- `/ctm stop` - stop movement (both engines)
- `/ctm status` - print vnavmesh state and current destination

## Config

- World Click: enable world clicks and pick Direct or
  Pathfind. Pathfind falls back to Direct automatically when
  vnavmesh is missing or the mesh is not ready (toggleable).
- Modifier key: Shift (default), Ctrl, Alt, or None. Require modifier is
  on by default so normal target selection is untouched.
- Movement and Safety: stop on WASD, jump, cast; optional cancel in
  combat; arrival tolerance; Direct stop distance; fly support.
- Feedback: vanilla style ground reticle that follows the cursor while
  armed and pins to the destination while moving, chat notices for
  fallback and errors.

## How it works

- World clicks are polled on the draw thread: mouse released edge,
  modifier gate, ImGui and game UI guards, then `ScreenToWorld`.
  No input hooks, so patches are less likely to break it.
- Movement picks Pathfind when vnavmesh reports ready, else Direct.
  Direct hooks the game's own movement input readers and steers toward
  the target, with stuck detection, timeout, and arrival radius.

## Attribution

Movement idea and IPC endpoint list adapted from
Jaksuhn ffxiv-bundleoftweaks (BSD-3-Clause) and its clib helpers,
plus vnavmesh by awgil and contributors.
Direct movement here is a clean room implementation on top of public
Dalamud APIs, not a port of the hook based override.

## Disclaimer

Movement automation is third party and unsupported. Use at your own
risk, keep it private, and be extra careful in PvP or high end duties.

## Build

- Requires .NET SDK (see global packages, 8 and 10 installed) and the
  Dalamud.NET.Sdk 15.0.0 restore.
- `dotnet build` bumps the revision in the csproj automatically and
  copies the DLL, deps json, and manifest json to devPlugins.
- Close the game or unload the plugin before rebuilding, or the copy
  fails because the game locks the DLL.
