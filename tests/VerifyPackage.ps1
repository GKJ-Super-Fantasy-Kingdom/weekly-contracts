param(
    [string]$GameDir = 'D:\SteamLibrary\steamapps\common\Super Fantasy Kingdom',
    [string]$ProfileDir = "$env:APPDATA\Thunderstore Mod Manager\DataFolder\SuperFantasyKingdom\profiles\Test - Mods"
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$core = Join-Path $ProfileDir 'BepInEx/core'
$managed = Join-Path $GameDir 'Super Fantasy Kingdom_Data/Managed'
Add-Type -Path (Join-Path $core 'Mono.Cecil.dll')
Add-Type -AssemblyName System.IO.Compression.FileSystem
$reader = New-Object Mono.Cecil.ReaderParameters
$resolver = New-Object Mono.Cecil.DefaultAssemblyResolver
$resolver.AddSearchDirectory($managed)
$resolver.AddSearchDirectory($core)
$reader.AssemblyResolver = $resolver
$manifest = Get-Content -LiteralPath (Join-Path $root 'thunderstore/manifest.json') -Raw | ConvertFrom-Json
$zip = [IO.Compression.ZipFile]::OpenRead((Join-Path $root "dist/WeeklyContracts-$($manifest.version_number).zip"))
try {
    $buffer = [IO.MemoryStream]::new()
    $stream = $zip.GetEntry('plugins/WeeklyContracts.dll').Open()
    try { $stream.CopyTo($buffer) } finally { $stream.Dispose() }
    $buffer.Position = 0
    $assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($buffer, $reader)
    if ($assembly.Name.Name -ne 'WeeklyContracts' -or $assembly.Name.Version.ToString(3) -ne $manifest.version_number) {
        throw 'Unexpected packaged assembly identity.'
    }
    $plugin = $assembly.MainModule.GetType('WeeklyContracts.Plugin')
    $attr = @($plugin.CustomAttributes | Where-Object { $_.AttributeType.Name -eq 'BepInPlugin' })
    if ($attr.Count -ne 1 -or $attr[0].ConstructorArguments[0].Value -ne 'sfk.weeklycontracts' -or
        $attr[0].ConstructorArguments[2].Value -ne $manifest.version_number) { throw 'Invalid BepInEx metadata.' }
    $refs = @($assembly.MainModule.GetMemberReferences() | Where-Object {
        $_.DeclaringType.Scope.Name -match '^(Assembly-CSharp|Unity|BepInEx|0Harmony)'
    })
    foreach ($ref in $refs) {
        if ($null -eq $ref.Resolve()) { throw "Unresolved packaged reference: $ref" }
    }
    $game = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $managed 'Assembly-CSharp.dll'), $reader)
    $calendar = $game.MainModule.GetType('SuperFantasyKingdom.UI.UICalendar')
    foreach ($name in 'textCycle','firstDay') {
        if ($name -notin $calendar.Fields.Name) { throw "Missing native calendar field $name." }
    }
    if ('OnEnable' -notin $calendar.Methods.Name) { throw 'Missing calendar hook.' }
    $position = $game.MainModule.GetType('SuperFantasyKingdom.GameDataManager').Methods |
        Where-Object Name -eq 'PositionWorldBuildings'
    $manager = $game.MainModule.GetType('SuperFantasyKingdom.SaveManager')
    $raw = @($manager.Methods | Where-Object { $_.Name -eq 'Load' -and !$_.HasGenericParameters -and
        $_.ReturnType.FullName -eq 'System.String' })
    $tavern = $game.MainModule.GetType('SuperFantasyKingdom.Tavern.TavernSaveManager')
    $load = $tavern.Methods | Where-Object Name -eq 'Load'
    if ($raw.Count -ne 1 -or $position.Body.Instructions.Operand -notcontains $raw[0] -or
        !($load.Body.Instructions.Operand -match 'SaveManager::LoadGameData\(') -or
        'm_GameData' -notin $position.DeclaringType.Fields.Name) { throw 'Native checkpoint hooks changed.' }
    "Verified packaged plugin $($assembly.Name.Version), $($refs.Count) runtime references, calendar hook and checkpoint hook."
} finally {
    if ($game) { $game.Dispose() }
    if ($assembly) { $assembly.Dispose() }
    if ($buffer) { $buffer.Dispose() }
    $zip.Dispose()
    $resolver.Dispose()
}
