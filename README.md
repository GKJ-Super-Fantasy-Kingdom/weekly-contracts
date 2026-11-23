# Weekly Contracts

Deliver six bread before the weekly boss night and choose one of three run relics. The first release offers **Bread for the watch** in kingdoms that have a native bread supply.

## Playing

At the first eligible dawn of each seven-day week, read the contract and choose **Accept**, **Decline…**, or **Later**. The deadline is nightfall on day 7, 14, 21 or 28. Accepting costs nothing. Later dismisses the card until dawn ends. To reopen an offer, deliver, or claim a reward, open the kingdom calendar and select **Contract** beside the cycle number. On controller the contract link takes focus when available. Down moves into the day grid and Up from day one returns to the link.

**Deliver 6…** shows your available bread before and after payment. The confirmation checks stock, the deadline and three eligible relics before charging. Reserved bread stays with its existing job. The native message overlay pauses the kingdom while you read. A missed deadline costs nothing.

**Collect reward** opens the first relic card. Read its effect, use **Next relic** to browse, and use **Choose… → Collect** to commit. You can leave with **Later** or the normal close-overlay action. The recorded options stay available after the deadline. If another reward makes an option unavailable, you can still choose another or use an eligible native upgrade. If all three become unavailable, you can keep the reward pending or take the six bread back. An uncollected reward carries over into later weeks and takes priority over new offers.

Contract state travels inside the game's normal kingdom save. Restoring a checkpoint restores its bread, relic inventory, deadline and contract state together. Earlier checkpoints return to their earlier decisions. The mod does not add a mid-day save command.

## Local Thunderstore import

1. With the shared test slot available, select **Super Fantasy Kingdom → Test - Mods** in Thunderstore Mod Manager.
2. In the profile settings, use **Import local mod** and select `dist/WeeklyContracts-0.1.0.zip`. Enable WeeklyContracts and the BepInEx dependencies listed in the manifest.
3. Launch modded and check the profile's `BepInEx/LogOutput.log` for `Weekly Contracts ready.`. Use a disposable **in-game** kingdom with a bread supply. The mod manager profile chooses plugins; the kingdom save is selected in game.

## Controller playtest

- At dawn, hold the opening input. Verify that the offer stays undecided until a fresh submit. Read the price, relic reward and day. Use Later, reopen via the calendar, then accept. On a separate week decline and confirm that the offer stays closed after a normal save and reload.
- At five free bread, verify that delivery is unavailable. At seven, review the change to one. Back out with all seven, reopen, confirm once and verify one remains. Try with some bread reserved for a worker. On a separate attempt, wait until the deadline night's start and verify no bread is taken.
- Earn a reward, leave the card, and reload **after the next native save**. Check the same three relics and the paid balance. Read all three effects, collect one, and reload after another save. Confirm one grant and no second claim. Test an older checkpoint separately.
- Verify focus, visible labels, native Back, normal calendar navigation, and mouse/keyboard input after switching away from the controller. Check long localized relic descriptions and pause/resume with another native overlay open.

## Build and verification

Requires the installed game's managed assemblies, a BepInEx 5 profile, .NET SDK 8+ and Python with Pillow. `WeeklyContracts.csproj` supplies this PC's Steam and Test - Mods paths. Override `GameDir` and `ProfileDir` as MSBuild properties or environment variables on another machine.

```powershell
dotnet run --project tests/ContractChecks.csproj -c Release
python package.py
./tests/VerifyPackage.ps1
```

The packager builds the Release DLL, opens the completed archive, compares every entry with its source, and decodes the 256 × 256 PNG. Output is `dist/WeeklyContracts-0.1.0.zip`. The ZIP contains only the plugin and its listing files.
