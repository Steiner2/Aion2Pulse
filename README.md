# Aion2Pulse

A DPS and healing meter for AION 2 with automatic encounter tracking,
customizable overlays and combat history. Combat values come from passive
network packet analysis using WinDivert.

## Features

- Damage, DPS, healing and HPS, including a combined four-column view.
- Automatic encounters for mob packs and bosses. Finished results stay frozen
  between fights; the next pull starts a new encounter.
- Three overlay designs: Raid Classic, Focus Glass and Combat Studio.
- Compact title bar, class colors, transparent backgrounds and interface scaling.
- A separate settings window and configurable hotkeys.
- Searchable combat history with split, list and card layouts.
- Player and skill breakdowns with original skill icons.
- Overall totals for the current map visit, excluding travel and waiting time.
- Connection latency from protocol samples, with a passive TCP estimate fallback.

DPS is the default view. Use the title-bar mode button to cycle through DPS, HPS
and both, or enable **Show DPS and HPS together** under **Settings → Overlay**.
The selected view persists between starts.

## Requirements and setup

- Windows x64.
- Administrator privileges for the WinDivert capture driver.

Extract the complete Windows package and launch **Aion2Pulse.exe** as
administrator. The package includes the .NET runtime; Npcap is not required.
Start the meter before entering the game. If the character is not recognized,
return to character selection and enter again.

## Combat history

The last 50 encounters are saved in the `history/` folder next to the application,
including player totals and skill breakdowns. Keep this folder when updating.
Overall totals cover the current map visit and reset on a detected map change
or application restart. **New total** starts a fresh total manually.

Both DPS and HPS use shared encounter duration. Healing is the amount reported
by captured packets; it does not represent verified effective healing or overheal.
Unknown or expired latency measurements show **— ms**. A TCP-derived latency
value is identified in the tooltip.

## Build from source

Install the .NET 10 SDK on Windows, then run:

```powershell
./build.ps1 -Test -Publish
```

Source and tests are in `meter/`. The build produces a self-contained Windows
package under `artifacts/` with Native AOT disabled. The included regression
suite uses synthetic inputs; raw gameplay captures are not distributed.

## Development status

Global support is under active development. Party/force tracking, boss phases,
same-target re-pulls, healing and latency still require further live validation
against the current client. Automatic encounter completion uses an inactivity
fallback when reliable combat status signals are unavailable.

## License and credits

Aion2Pulse is distributed under [GPL-3.0](LICENSE.txt) and is based on
[Aion2Flow](https://github.com/cloris-chan/Aion2Flow) by Cloris and contributors.
The imported revision and retained notices are documented in
[source provenance](meter/UPSTREAM.md).

Original game icons and their catalog come from the Aion2Flow foundation.
Game asset rights remain with their respective owners and are independent of
this project's source-code license.
