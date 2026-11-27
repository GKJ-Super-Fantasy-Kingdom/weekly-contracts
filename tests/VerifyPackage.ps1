param(
    [string]$GameDir = 'D:\SteamLibrary\steamapps\common\Super Fantasy Kingdom',
    [string]$ProfileDir = "$env:APPDATA\Thunderstore Mod Manager\DataFolder\SuperFantasyKingdom\profiles\Test - Mods",
    [string]$ZipPath
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
if (!$ZipPath) { $ZipPath = Join-Path $root "dist/WeeklyContracts-$($manifest.version_number).zip" }
$zip = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $ZipPath).Path)
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
    $orderKinds = $assembly.MainModule.GetType('WeeklyContracts.OrderKind').Fields | Where-Object IsLiteral
    foreach ($artName in @('ContractFrame.png', 'ContractFocus.png') + @($orderKinds | ForEach-Object { "Backdrop$($_.Name).png" })) {
        $asset = $assembly.MainModule.Resources | Where-Object Name -eq "WeeklyContracts.Art.$artName"
        if ($asset -isnot [Mono.Cecil.EmbeddedResource]) { throw "Missing embedded offer card art $artName." }
        $source = [IO.File]::ReadAllBytes((Join-Path $root "src/Art/$artName"))
        if ([Convert]::ToBase64String($asset.GetResourceData()) -cne [Convert]::ToBase64String($source)) {
            throw "Packaged offer card art $artName differs from source."
        }
    }
    # Verify the actual strings compiled into the plugin, including native-font punctuation.
    $ellipsis = [char]0x2026
    $offerLabels = ($plugin.Methods | Where-Object Name -eq 'ShowOffers').Body.Instructions.Operand
    $deliveryLabels = ($plugin.Methods | Where-Object Name -eq 'Show').Body.Instructions.Operand
    if (("Decline" + $ellipsis) -notin $offerLabels -or ("Deliver {0}" + $ellipsis) -notin $deliveryLabels) {
        throw 'Contract action labels have unexpected punctuation in the packaged DLL.'
    }
    $refs = @($assembly.MainModule.GetMemberReferences() | Where-Object {
        $_.DeclaringType.Scope.Name -match '^(Assembly-CSharp|Unity|BepInEx|0Harmony)'
    })
    foreach ($ref in $refs) {
        if ($null -eq $ref.Resolve()) { throw "Unresolved packaged reference: $ref" }
    }
    $api = $assembly.MainModule.GetType('WeeklyContracts.CalendarApi')
    $query = @($api.Methods | Where-Object Name -eq 'TryGetActiveDelivery')
    if (!$api.IsPublic -or $query.Count -ne 1 -or !$query[0].IsPublic -or !$query[0].IsStatic -or
        $query[0].ReturnType.FullName -ne 'System.Boolean' -or
        (($query[0].Parameters | ForEach-Object { $_.ParameterType.FullName }) -join ',') -ne
            'System.String&,System.Int32&,System.String&,System.Int32&' -or
        @($query[0].Parameters | Where-Object { !$_.IsOut }).Count -gt 0 -or
        $assembly.MainModule.AssemblyReferences.Name -contains 'VisitorCalendar') { throw 'Calendar query contract changed.' }
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
    $writes = @($manager.Methods | Where-Object { $_.Name -eq 'Save' -and $_.ReturnType.FullName -eq 'System.Void' })
    $saveTarget = $null
    foreach ($signature in 'System.String,System.String,System.Boolean', 'System.String,System.String') {
        $saveTarget = $writes | Where-Object { ($_.Parameters.ParameterType.FullName -join ',') -eq $signature }
        if ($saveTarget) { break }
    }
    if (!$saveTarget) { throw 'Missing supported native SaveManager.Save overload.' }
    foreach ($writerType in $position.DeclaringType, $tavern) {
        $writer = $writerType.Methods | Where-Object { $_.Name -eq 'Save' -and !$_.HasParameters }
        if ($saveTarget.FullName -notin $writer.Body.Instructions.Operand.FullName) {
            throw "$($writerType.Name).Save no longer calls $($saveTarget.FullName)."
        }
    }
    $load = $tavern.Methods | Where-Object Name -eq 'Load'
    if ($raw.Count -ne 1 -or $position.Body.Instructions.Operand -notcontains $raw[0] -or
        !($load.Body.Instructions.Operand -match 'SaveManager::LoadGameData\(') -or
        'm_GameData' -notin $position.DeclaringType.Fields.Name) { throw 'Native checkpoint hooks changed.' }
    "Verified packaged plugin $($assembly.Name.Version), $($refs.Count) runtime references, frame, focus rim, $(@($orderKinds).Count) backdrops, optional calendar query, calendar hook and checkpoint hook ($($saveTarget.FullName))."
} finally {
    if ($game) { $game.Dispose() }
    if ($assembly) { $assembly.Dispose() }
    if ($buffer) { $buffer.Dispose() }
    $zip.Dispose()
    $resolver.Dispose()
}
