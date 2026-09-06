# ClickToMove

Click where you wanna go! Hold Shift and left-click the 3D ground
to walk there. Uses vnavmesh pathfinding when available,
straight line fallback otherwise.

## Install (third party repo)

This is automation, so it cannot be on the official Dalamud repo.
Install it from the custom repo instead:

1. In game, open `/xlsettings` and go to the Experimental tab.
2. Under Custom Plugin Repositories, paste:
   `https://raw.githubusercontent.com/RaylaValdez/dalamudrepo/main/pluginmaster.json`
3. Enable the new entry, save, then open `/xlplugins`.
4. Search for ClickToMove and install it.
5. For pathfinding, also add `https://puni.sh/api/repository/veyn`
   as a custom repo and install vnavmesh. Without it, clicks fall
   back to straight line walking.

Alternatively, as a dev plugin: build in Debug and find the DLL in
`%APPDATA%\XIVLauncher\devPlugins\ClickToMove\`, then add that path
under Dalamud Settings > Experimental > Dev Plugin Locations.

## Usage

- `Shift + Left-click` on 3D ground: move to that spot.
  Plain clicks without the modifier behave exactly like vanilla targeting.
- A vanilla style ground reticle follows the cursor while the modifier
  is held (always, when modifier is None) and pins to the destination
  while moving. It clears on arrival or cancel.
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
  on by default so normal target selection is untouched. Releases from
  a camera drag never count as clicks.
- Movement and Safety: stop on WASD, jump, cast; optional cancel in
  combat; arrival tolerance; Direct stop distance; fly support.
- Feedback: placement preview reticle, chat notices for
  fallback and errors.

## How it works

- World clicks are polled on the draw thread: mouse released edge with
  drag detection, modifier gate, ImGui and game UI guards, then
  `ScreenToWorld`. No input hooks, so patches are less likely to break it.
- Movement picks Pathfind when vnavmesh reports ready, else Direct.
  Direct hooks the game's own movement input readers (same mechanism
  as vnavmesh and Jaksuhn clib) and steers toward the target, with
  stuck detection, timeout, and arrival radius.

## Attribution

Movement idea and IPC endpoint list adapted from
Jaksuhn ffxiv-bundleoftweaks (BSD-3-Clause) and its clib helpers,
plus vnavmesh by awgil and contributors.

## Disclaimer

Movement automation is third party and unsupported. Use at your own
risk, keep it private, and be extra careful in PvP or high end duties.

## Build

- Requires .NET SDK and the Dalamud.NET.Sdk 15.0.0 restore.
- `dotnet build` in Debug bumps the revision in the csproj automatically
  and copies the DLL, deps json, and manifest json to devPlugins.
  Release and ExportRelease builds keep a deliberate version for
  repo publishing: build ExportRelease, zip the DLL, deps json,
  manifest json, pdb, and icon.png into latest.zip.
- Close the game or unload the plugin before rebuilding, or the copy
  fails because the game locks the DLL.
