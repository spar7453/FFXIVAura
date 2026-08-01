[CmdletBinding()]
param(
    [string]$Path = (Join-Path $env:APPDATA 'XIVLauncherKR\pluginConfigs\FFXIVAura\performance-profile.csv'),
    [ValidateSet('Latest', 'Alliance')]
    [string]$Mode = 'Latest',
    [ValidateRange(1000, 500000)]
    [int]$TailLines = 60000
)

$ErrorActionPreference = 'Stop'

function Convert-Number([object]$Value) {
    if ([string]::IsNullOrWhiteSpace([string]$Value)) {
        return 0.0
    }

    return [double]::Parse([string]$Value, [Globalization.CultureInfo]::InvariantCulture)
}

function Convert-Integer([object]$Value) {
    if ([string]::IsNullOrWhiteSpace([string]$Value)) {
        return 0
    }

    return [long]::Parse([string]$Value, [Globalization.CultureInfo]::InvariantCulture)
}

function Get-Percentile([double[]]$Values, [double]$Percentile) {
    if ($Values.Count -eq 0) {
        return 0.0
    }

    $sorted = @($Values | Sort-Object)
    $index = [math]::Min(
        $sorted.Count - 1,
        [math]::Max(0, [math]::Ceiling($sorted.Count * $Percentile) - 1))
    return [double]$sorted[$index]
}

function Convert-Detail([string]$Detail) {
    $pairs = @{}
    foreach ($part in ($Detail -split ';')) {
        $separator = $part.IndexOf('=')
        if ($separator -gt 0) {
            $pairs[$part.Substring(0, $separator)] = $part.Substring($separator + 1)
        }
    }

    return $pairs
}

function Get-Timestamp([string]$Line) {
    $separator = $Line.IndexOf(',')
    if ($separator -gt 0) {
        return $Line.Substring(0, $separator)
    }

    return [string]::Empty
}

function Copy-SharedProfileSnapshot([string]$SourcePath) {
    $snapshotPath = [IO.Path]::GetTempFileName()
    $source = [IO.FileStream]::new(
        $SourcePath,
        [IO.FileMode]::Open,
        [IO.FileAccess]::Read,
        [IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete)
    try {
        $target = [IO.FileStream]::new(
            $snapshotPath,
            [IO.FileMode]::Create,
            [IO.FileAccess]::Write,
            [IO.FileShare]::Read)
        try {
            $remaining = $source.Length
            $buffer = [byte[]]::new(1MB)
            while ($remaining -gt 0) {
                $count = [int][math]::Min($buffer.Length, $remaining)
                $read = $source.Read($buffer, 0, $count)
                if ($read -le 0) {
                    break
                }

                $target.Write($buffer, 0, $read)
                $remaining -= $read
            }
        }
        finally {
            $target.Dispose()
        }
    }
    finally {
        $source.Dispose()
    }

    return $snapshotPath
}

if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
    throw "Profile file not found: $Path"
}

$snapshotPath = Copy-SharedProfileSnapshot $Path
$Path = $snapshotPath
try {
$header = (Get-Content -LiteralPath $Path -TotalCount 1) -split ','
$selectedRows = @()

if ($Mode -eq 'Latest') {
    $tailRows = @(Get-Content -LiteralPath $Path -Tail $TailLines |
        ConvertFrom-Csv -Header $header |
        Where-Object { $_.timestampUtc -and $_.timestampUtc -ne $header[0] })
    $tailFrames = @($tailRows | Where-Object scope -eq 'frame' | Sort-Object { [datetime]$_.timestampUtc })
    if ($tailFrames.Count -eq 0) {
        throw 'No frame rows were found in the selected tail.'
    }

    $segmentStart = 0
    for ($index = 1; $index -lt $tailFrames.Count; $index++) {
        $previousTimestamp = [datetime]$tailFrames[$index - 1].timestampUtc
        $timestamp = [datetime]$tailFrames[$index].timestampUtc
        $sampleReset = [long]$tailFrames[$index].sampleFrameCount -lt [long]$tailFrames[$index - 1].sampleFrameCount
        $largeGap = ($timestamp - $previousTimestamp).TotalSeconds -gt 60
        if ($sampleReset -or $largeGap) {
            $segmentStart = $index
        }
    }

    $startTimestamp = [datetime]$tailFrames[$segmentStart].timestampUtc
    $selectedRows = @($tailRows | Where-Object { [datetime]$_.timestampUtc -ge $startTimestamp })
}
else {
    $timestamps = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($line in [IO.File]::ReadLines($Path)) {
        if ($line.Contains(',diagnostic,partyCooldown,Party Cooldown,') -and
            $line.Contains('rosterSource=Alliance')) {
            $null = $timestamps.Add((Get-Timestamp $line))
        }
    }

    if ($timestamps.Count -eq 0) {
        throw 'No alliance party cooldown snapshots were found.'
    }

    $selectedLines = [Collections.Generic.List[string]]::new()
    foreach ($line in [IO.File]::ReadLines($Path)) {
        $timestamp = Get-Timestamp $line
        if (-not $timestamps.Contains($timestamp)) {
            continue
        }

        if ($line.Contains(',frame,') -or
            $line.Contains(',section,') -or
            $line.Contains(',window,') -or
            $line.Contains(',diagnostic,partyCooldown,') -or
            $line.Contains(',diagnostic,aura,') -or
            $line.Contains(',diagnostic,plugin,') -or
            $line.Contains(',diagnostic,tooltip,')) {
            $selectedLines.Add($line)
        }
    }

    $selectedRows = @($selectedLines | ConvertFrom-Csv -Header $header)
}

$frames = @($selectedRows | Where-Object scope -eq 'frame' | Sort-Object { [datetime]$_.timestampUtc })
if ($frames.Count -eq 0) {
    throw 'No matching frame rows were found.'
}

$frameMilliseconds = [double[]]@($frames | ForEach-Object { Convert-Number $_.currentMs })
$allocatedBytes = [double[]]@($frames | ForEach-Object { Convert-Number $_.allocatedBytes })
$startLocal = ([datetime]$frames[0].timestampUtc).ToLocalTime()
$endLocal = ([datetime]$frames[-1].timestampUtc).ToLocalTime()

[pscustomobject]@{
    Mode = $Mode
    StartLocal = $startLocal.ToString('yyyy-MM-dd HH:mm:ss')
    EndLocal = $endLocal.ToString('yyyy-MM-dd HH:mm:ss')
    Snapshots = $frames.Count
    AverageMs = [math]::Round(($frameMilliseconds | Measure-Object -Average).Average, 3)
    P95Ms = [math]::Round((Get-Percentile $frameMilliseconds 0.95), 3)
    P99Ms = [math]::Round((Get-Percentile $frameMilliseconds 0.99), 3)
    SampledMaxMs = [math]::Round(($frameMilliseconds | Measure-Object -Maximum).Maximum, 3)
    AverageAllocatedBytes = [math]::Round(($allocatedBytes | Measure-Object -Average).Average)
    P95AllocatedBytes = [math]::Round((Get-Percentile $allocatedBytes 0.95))
} | Format-List

'Top sections:'
$selectedRows |
    Where-Object scope -eq 'section' |
    Group-Object id |
    ForEach-Object {
        $values = [double[]]@($_.Group | ForEach-Object { Convert-Number $_.currentMs })
        [pscustomobject]@{
            Id = $_.Name
            AverageMs = [math]::Round(($values | Measure-Object -Average).Average, 4)
            P95Ms = [math]::Round((Get-Percentile $values 0.95), 4)
            MaxMs = [math]::Round(($values | Measure-Object -Maximum).Maximum, 3)
        }
    } |
    Sort-Object AverageMs -Descending |
    Select-Object -First 10 |
    Format-Table -AutoSize

'Overlay windows:'
$selectedRows |
    Where-Object scope -eq 'window' |
    Group-Object id |
    ForEach-Object {
        $values = [double[]]@($_.Group | ForEach-Object { Convert-Number $_.currentMs })
        [pscustomobject]@{
            Id = $_.Name
            Label = $_.Group[-1].label
            AverageMs = [math]::Round(($values | Measure-Object -Average).Average, 4)
            P95Ms = [math]::Round((Get-Percentile $values 0.95), 4)
            MaxMs = [math]::Round(($values | Measure-Object -Maximum).Maximum, 3)
        }
    } |
    Sort-Object AverageMs -Descending |
    Format-Table -AutoSize

$partyRows = @($selectedRows | Where-Object id -eq 'partyCooldown')
if ($partyRows.Count -gt 0) {
    $party = @($partyRows | ForEach-Object {
        $detail = Convert-Detail $_.detail
        [pscustomobject]@{
            Source = $detail.rosterSource
            Members = Convert-Integer $detail.frameMembers
            DisplayMembers = Convert-Integer $detail.displayMembers
            AllianceMembers = Convert-Integer $detail.allianceMembers
            GroupA = Convert-Integer $detail.allianceA
            GroupB = Convert-Integer $detail.allianceB
            GroupC = Convert-Integer $detail.allianceC
            LocalGroup = $detail.localAllianceGroup
            OrderHash = $detail.rosterOrderHash
            RosterCacheHits = Convert-Integer $detail.rosterCacheHits
            RosterCacheMisses = Convert-Integer $detail.rosterCacheMisses
            ActiveTimerRefreshes = Convert-Integer $detail.activeTimerRefreshAccepted
            ActiveTimerSuppressions = Convert-Integer $detail.activeTimerStaleSuppressed
            LocalLogsSkipped = Convert-Integer $detail.logLocalPlayerSkippedTotal
            LocalOwnedObjectLogsSkipped = Convert-Integer $detail.logLocalOwnedObjectSkippedTotal
        }
    })

    $lastParty = $party[-1]
    [pscustomobject]@{
        RosterSources = (($party | Group-Object Source | ForEach-Object { "$($_.Name):$($_.Count)" }) -join ', ')
        MaxMembers = ($party.Members | Measure-Object -Maximum).Maximum
        MaxDisplayMembers = ($party.DisplayMembers | Measure-Object -Maximum).Maximum
        MaxAllianceMembers = ($party.AllianceMembers | Measure-Object -Maximum).Maximum
        AllianceGroups = (($party | ForEach-Object { "$($_.GroupA)/$($_.GroupB)/$($_.GroupC)" } | Sort-Object -Unique) -join ', ')
        LocalGroups = (($party.LocalGroup | Where-Object { $_ } | Sort-Object -Unique) -join ', ')
        LastRosterOrderHash = $lastParty.OrderHash
        RosterCacheHits = $lastParty.RosterCacheHits
        RosterCacheMisses = $lastParty.RosterCacheMisses
        ActiveTimerRefreshes = $lastParty.ActiveTimerRefreshes
        ActiveTimerSuppressions = $lastParty.ActiveTimerSuppressions
        LocalLogsSkipped = $lastParty.LocalLogsSkipped
        LocalOwnedObjectLogsSkipped = $lastParty.LocalOwnedObjectLogsSkipped
    } | Format-List
}
}
finally {
    Remove-Item -LiteralPath $snapshotPath -Force -ErrorAction SilentlyContinue
}
