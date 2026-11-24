# Contributing to Weekly Contracts

## Report a bug

Open a [GitHub issue](https://github.com/GKJ-Super-Fantasy-Kingdom/weekly-contracts/issues) with the mod, game, and BepInEx versions, your kingdom and day, and the steps to reproduce the problem. Include the relevant log excerpt. For interface or input issues, include the UI scale and whether you used a controller or keyboard and mouse.

## Submit a change

Fork the repository, create a branch, and follow the build and verification instructions below. Use the assemblies from your own game installation and BepInEx profile as build references. Keep game binaries, save files, and local build output out of commits.

Describe the player-visible change in your pull request. List the checks you ran and, where relevant, which controller actions and UI scales you tested.

## Commit messages

Use [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/) for each commit. Start the subject with a type, an optional scope, and a short description. Use an exclamation mark before the colon for a breaking change.

Examples include `feat(ui): show controller prompts`, `fix(save): restore the current day`, and `chore: update build tooling`.

Accepted types are `feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `build`, `ci`, `chore`, `revert`. Use the same format for pull request titles so GitHub can use the title as the merge or squash commit subject.

## Build and Verification

Use the installed game's managed assemblies, a BepInEx 5 profile, .NET SDK 8+ and Python with Pillow. Set `GameDir` to your game installation and `ProfileDir` to your BepInEx profile when the project defaults differ from your machine.

```powershell
dotnet run --project tests/ContractChecks.csproj -c Release
python package.py
./tests/VerifyPackage.ps1 -GameDir $env:GameDir -ProfileDir $env:ProfileDir
```

Set both environment variables before using these commands, or omit the verifier arguments to use its defaults. The packager writes `dist/WeeklyContracts-0.1.0.zip`, checks every entry against its source and decodes the 256 × 256 icon.

For a controller playtest, use a disposable kingdom. Navigate between rows, select **Later**, reopen from the calendar and accept one offer. Back out of delivery once to verify stock stays intact. Confirm the delivery, collect the reward and Continue after the next native save to check the settled balance. Check the cards at 1× and 1.5×.
