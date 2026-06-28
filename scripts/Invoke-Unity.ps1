param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Compile', 'GenerateUI', 'GenerateMainMenu', 'ValidateUI', 'ValidateMainMenu', 'ValidateData', 'FeatureMatrix', 'TestEditMode', 'TestPlayMode')]
    [string]$Action
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot 'Civic'
$versionFile = Join-Path $projectPath 'ProjectSettings\ProjectVersion.txt'
$versionLine = Get-Content -LiteralPath $versionFile | Where-Object { $_ -like 'm_EditorVersion:*' } | Select-Object -First 1
if (-not $versionLine) {
    throw "Cannot determine Unity version from $versionFile"
}

$version = ($versionLine -split ':', 2)[1].Trim()
$candidates = @(
    $env:UNITY_EDITOR,
    "X:\Unity\$version\Editor\Unity.exe",
    "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
) | Where-Object { $_ }

$unityEditor = $candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
if (-not $unityEditor) {
    throw "Unity $version was not found. Set UNITY_EDITOR to the exact Unity.exe path."
}

$outputRoot = Join-Path $projectPath 'Logs\Codex'
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$logPath = Join-Path $outputRoot "$Action-$stamp.log"
$arguments = @('-batchmode', '-nographics', '-projectPath', $projectPath, '-logFile', $logPath)
$resultPath = $null

switch ($Action) {
    'Compile' {
        $arguments += '-quit'
    }
    'GenerateUI' {
        $arguments += @('-quit', '-executeMethod', 'Civic.Editor.UI.UiPrefabGenerator.GenerateAll')
    }
    'GenerateMainMenu' {
        $arguments += @('-quit', '-executeMethod', 'Civic.Editor.UI.MainMenuPrefabGenerator.GenerateAll')
    }
    'ValidateUI' {
        $arguments += @('-quit', '-executeMethod', 'Civic.Editor.UI.UiPrefabValidator.ValidateAll')
    }
    'ValidateMainMenu' {
        $arguments += @('-quit', '-executeMethod', 'Civic.Editor.UI.MainMenuPrefabValidator.ValidateAll')
    }
    'ValidateData' {
        $arguments += @('-quit', '-executeMethod', 'Civic.Editor.UI.CivicDataValidator.ValidateAll')
    }
    'FeatureMatrix' {
        $arguments += @('-quit', '-executeMethod', 'Civic.Editor.UI.CivicFeatureMatrixValidator.ValidateAll')
    }
    'TestEditMode' {
        $resultPath = Join-Path $outputRoot "EditMode-$stamp.xml"
        $arguments += @('-runTests', '-testPlatform', 'EditMode', '-testResults', $resultPath)
    }
    'TestPlayMode' {
        $resultPath = Join-Path $outputRoot "PlayMode-$stamp.xml"
        $arguments += @('-runTests', '-testPlatform', 'PlayMode', '-testResults', $resultPath)
    }
}

Write-Output "Unity: $unityEditor"
Write-Output "Project: $projectPath"
Write-Output "Action: $Action"

$process = Start-Process -FilePath $unityEditor -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
if ($process.ExitCode -ne 0) {
    if (Test-Path -LiteralPath $logPath) {
        Get-Content -LiteralPath $logPath -Tail 80
    }
    throw "Unity action $Action failed with exit code $($process.ExitCode). Log: $logPath"
}

if ($resultPath) {
    if (-not (Test-Path -LiteralPath $resultPath)) {
        throw "Unity did not create test results: $resultPath"
    }

    [xml]$results = Get-Content -Raw -LiteralPath $resultPath
    $failed = [int]$results.'test-run'.failed
    if ($failed -gt 0) {
        throw "Unity $Action reported $failed failed test(s). Results: $resultPath"
    }
    Write-Output "Results: $resultPath"
}

Write-Output "Log: $logPath"
Write-Output "UNITY_ACTION_OK=$Action"
