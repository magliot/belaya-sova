[CmdletBinding()]
param(
    [string] $Editor = 'C:\Program Files\Unity\Hub\Editor\6000.3.27f1\Editor\Unity.exe',
    [string] $Cache,
    [switch] $ImportOnly,
    [switch] $Verify,
    [switch] $Preview,
    [switch] $PackageOnly
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$sourceProject = Join-Path $root 'unity/OwlSlope'
if (!(Test-Path -LiteralPath $Editor)) { throw "Unity 6.3 LTS editor missing: $Editor" }
if (!$Cache) {
    $digest = [Security.Cryptography.SHA256]::Create().ComputeHash([Text.Encoding]::UTF8.GetBytes($root))
    $Cache = Join-Path ([IO.Path]::GetTempPath()) ('OwlRace-' + [BitConverter]::ToString($digest,0,5).Replace('-',''))
}
$Cache = [IO.Path]::GetFullPath($Cache)
if ($Cache -match '[^\u0020-\u007E]') { throw 'Pass -Cache with an ASCII path for the WebAssembly toolchain.' }
[void](New-Item -ItemType Directory -Path $Cache -Force)
foreach ($directory in 'Assets','Packages','ProjectSettings') {
    $target = (Resolve-Path (Join-Path $sourceProject $directory)).Path
    $link = Join-Path $Cache $directory
    if (Test-Path -LiteralPath $link) {
        $actual = (Get-Item -LiteralPath $link).Target
        if (!$actual -or [IO.Path]::GetFullPath([string]$actual) -ne $target) { throw "Cache already belongs to a different Unity project: $link" }
    } else {
        [void](New-Item -ItemType Junction -Path $link -Target $target)
    }
}
$log = Join-Path $Cache $(if($Verify) { 'mountain-checks.log' } else { 'mountain-build.log' })
$output = Join-Path $Cache 'Builds/MountainWeb'
$env:SOVA_RACE_OUTPUT = $output
$arguments = @('-batchmode','-projectPath',('"' + $Cache + '"'),'-buildTarget','WebGL','-logFile',('"' + $log + '"'))
if ($Verify) {
    $arguments += @('-executeMethod','CheckMountainGame.Run')
} else {
    $arguments += '-quit'
    if (!$ImportOnly) { $arguments += @('-executeMethod','BuildMountainRace.Web') }
}
if ($PackageOnly -and ($Verify -or $ImportOnly)) { throw '-PackageOnly packages an existing Web build.' }
if (!$PackageOnly) {
    $editorProcess = Start-Process -FilePath $Editor -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
    if ($editorProcess.ExitCode -ne 0) {
        Select-String -LiteralPath $log -Pattern 'error CS|Shader error|Exception:|Error building|Build failed|error:|Failed to' | Select-Object -Last 16
        throw "Unity exited with $($editorProcess.ExitCode). See $log"
    }
}
if ($Verify) {
    if (!(Select-String -LiteralPath $log -Pattern 'MOUNTAIN GAME CHECKS PASS' -Quiet)) { throw "Missing play-mode checks in $log" }
    if (!(Select-String -LiteralPath $log -Pattern 'CONTINUOUS LAP PASS' -Quiet)) { throw "Missing full ranked lap in $log" }
    Select-String -LiteralPath $log -Pattern 'MOUNTAIN (WORLD|GAME) CHECKS PASS|CONTINUOUS LAP PASS|SHOP HARVEST'
} elseif (!$ImportOnly) {
    if (!(Select-String -LiteralPath $log -Pattern 'MOUNTAIN WEB BUILT' -Quiet)) { throw "Missing build completion marker in $log" }
    $site = Join-Path $root $(if ($Preview) { 'docs/progression-test' } else { 'docs/unity-test' })
    [void](New-Item -ItemType Directory -Path $site -Force)
    # WebViews with the previous HTML must be able to finish their in-flight download during rollout.
    $entry = Join-Path $site 'index.html'
    $previous = if (Test-Path -LiteralPath $entry) {
        [regex]::Matches([IO.File]::ReadAllText($entry),'Build/([a-f0-9]{32,64}\.[\w.]+)') | ForEach-Object { $_.Groups[1].Value }
    }
    $html = [IO.File]::ReadAllText((Join-Path $output 'index.html'))
    $referenced = @([regex]::Matches($html,'Build/([^''"?#\s]+)') | ForEach-Object { $_.Groups[1].Value } | Select-Object -Unique)
    if ($referenced.Count -lt 4) { throw 'Could not identify all Web player artifacts in the generated index.' }
    $bin = Join-Path $site 'Build'
    [void](New-Item -ItemType Directory -Path $bin -Force)
    # Unity hashes decoded content. Gzip and Brotli can therefore have the same Unity filename
    # even though they need different decoders. Address the bytes actually served by Pages.
    $published = foreach($name in $referenced) {
        if ($name -notmatch '^[a-f0-9]{32}\.[\w.]+$') { throw "Unexpected Web player filename: $name" }
        $source = Join-Path (Join-Path $output 'Build') $name
        $digest = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant().Substring(0,32)
        $filename = $digest + $name.Substring(32)
        Copy-Item -LiteralPath $source -Destination (Join-Path $bin $filename) -Force
        $html = $html.Replace("Build/$name","Build/$filename")
        $filename
    }
    foreach($item in Get-ChildItem -LiteralPath $output) {
        if($item.Name -notin @('Build','index.html','Symbols','BackUpThisFolder_ButDontShipItWithYourGame')) {
            Copy-Item -LiteralPath $item.FullName -Destination $site -Force -Recurse
        }
    }
    [IO.File]::WriteAllText($entry,$html,[Text.UTF8Encoding]::new($false))
    # Rotate the previous player only on an actual change. Repackaging a release
    # must leave all files used by already open WebViews available.
    if (@($published | Where-Object { $previous -notcontains $_ }).Count) {
        foreach($file in Get-ChildItem -LiteralPath $bin -File) {
            if($published -notcontains $file.Name -and $previous -notcontains $file.Name -and $file.Name -match '^[a-fA-F0-9]{32,64}\.(data|wasm|framework\.js|loader\.js)(\.unityweb|\.gz|\.br)?$') {
                Remove-Item -LiteralPath $file.FullName
            }
        }
    }
    if ($Preview) {
        Copy-Item -LiteralPath (Join-Path $root 'server/preview/release.js') -Destination $site -Force
        & node (Join-Path $PSScriptRoot 'package-unity-shell.cjs') --preview
    } else {
        & node (Join-Path $PSScriptRoot 'package-unity-shell.cjs')
    }
    if ($LASTEXITCODE -ne 0) { throw 'Failed to package the Web player.' }
    Write-Output "Built $site. Editor log: $log"
}
