# Weekly Contracts

Choose an optional delivery at dawn and finish it before the weekly boss night. Supply jobs pay coins. **Bread for the Watch** asks for six bread and lets you pick one of three eligible run relics.

![Weekly Contracts picker showing Mend the Wall and Bread for the Watch](https://raw.githubusercontent.com/GKJ-Super-Fantasy-Kingdom/weekly-contracts/main/docs/screenshots/0.1.0/contract-picker.png)

*At 1.5× UI scale on day 10, the board offers four stone for three coins or six bread for a relic choice. Both deliveries are due before nightfall on day 14.*

## Choose a contract

Select a row to review its terms. **Accept** takes one job at no cost. **Later** keeps the same choices available from the calendar for the rest of that morning. **Decline…** asks for confirmation before closing the week's board. An unanswered offer expires when morning ends.

Up to two supply jobs appear from materials you can already spend. When all six supplies are stocked, the board rotates through all six in three weeks. Kingdom resource equivalents use their in-game names.

| Delivery | Reward |
| --- | --- |
| 2 water | 1 coin |
| 3 wood or 3 fish | 2 coins |
| 4 stone or 4 wheat | 3 coins |
| 5 boards | 5 coins |
| 6 bread | Choose 1 of 3 eligible run relics |

![Bread for the Watch terms showing available stock, delivery, deadline and reward](https://raw.githubusercontent.com/GKJ-Super-Fantasy-Kingdom/weekly-contracts/main/docs/screenshots/0.1.0/bread-contract.png)

*The bread contract can be accepted with zero bread in stock. The six bread are due at delivery.*

## Deliver and collect

Open the calendar and select **Contract** beneath the day grid. **Deliver** previews your balance after payment. **Confirm** rechecks the available stock and charges once. Your deadline is nightfall on day 7, 14, 21 or 28. Missing it costs nothing. The cards support the game's controller navigation and confirm actions, keyboard navigation and mouse clicks.

**Collect Reward** pays the listed coins or opens the relic cards. Use **Next Relic** to read each effect, then **Choose…** and **Collect**. You can also leave the reward pending. It carries across the deadline and takes priority over the next week's offers.

![Choose Your Relic card showing Eagle Eye, its effect and Next Relic](https://raw.githubusercontent.com/GKJ-Super-Fantasy-Kingdom/weekly-contracts/main/docs/screenshots/0.1.0/relic-choice.png)

*Eagle Eye is the first of three recorded choices in this run. The final confirmation comes after Choose….*

Contract state travels in the normal kingdom checkpoint. Continue restores that checkpoint's inventory, deadline and contract decisions together. If a reward provider fails, the contract checks the purse or relic inventory. A missing payout can be retried from the calendar. A completed transfer stays claimed.

## Compatibility

The mod spends from the kingdom's designated store and leaves reserved packets and separate meal inventories alone. A recipe change is supported if the bakery still produces the kingdom's bread resource. A mod that keeps its own inventory or enforces spending restrictions exclusively in the shop's `SpendResource` replacement may need an integration.

The reward cards keep their three original identifiers. If another reward changes a choice, you can select a remaining relic or an eligible native upgrade. If all three become unavailable, **Refund** returns the six units of the material you paid. If its definition is missing, the reward stays pending until that material or an eligible relic returns.

The release build was playtested with all ten GKJ mods. A second isolated run added **ownlyme/BakeryUsesWheat 1.2.0**, **ownlyme/TavernFoodPriorities 1.7.0** and **ownlyme/MoreRelics 1.1.0** together. The bread delivery charged once and awarded one relic. With MoreRelics, the board honors availability and boss-chest-only restrictions, including on upgrades. The playtests used simulated gamepad input.

## Installation

Install **WeeklyContracts** for Super Fantasy Kingdom with a Thunderstore-compatible mod manager and enable the dependencies listed in the manifest. To use a local build, choose **Import local mod** in your profile settings and select `WeeklyContracts-0.1.0.zip`. Launch the game modded. BepInEx logs `Weekly Contracts ready.` when the plugin loads.

## Build and verification

Use the installed game's managed assemblies, a BepInEx 5 profile, .NET SDK 8+ and Python with Pillow. Set `GameDir` to your game installation and `ProfileDir` to your BepInEx profile when the project defaults differ from your machine.

```powershell
dotnet run --project tests/ContractChecks.csproj -c Release
python package.py
./tests/VerifyPackage.ps1 -GameDir $env:GameDir -ProfileDir $env:ProfileDir
```

Set both environment variables before using these commands, or omit the verifier arguments to use its defaults. The packager writes `dist/WeeklyContracts-0.1.0.zip`, checks every entry against its source and decodes the 256 × 256 icon.

For a controller playtest, use a disposable kingdom. Navigate between rows, select **Later**, reopen from the calendar and accept one offer. Back out of delivery once to verify stock stays intact. Confirm the delivery, collect the reward and Continue after the next native save to check the settled balance. Check the cards at 1× and 1.5×.

## Contributing and license

See the [contribution guide](https://github.com/GKJ-Super-Fantasy-Kingdom/weekly-contracts/blob/main/CONTRIBUTING.md) for bug reports and changes. Original mod code is available under the [MIT License](https://github.com/GKJ-Super-Fantasy-Kingdom/weekly-contracts/blob/main/LICENSE). Super Fantasy Kingdom, its artwork and third-party dependencies retain their respective licenses and ownership.
