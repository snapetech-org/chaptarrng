param(
  [string] $AppDirectory = (Join-Path $PSScriptRoot '..\..\publish\Chaptarr')
)

$ErrorActionPreference = 'Stop'

$appRoot = (Resolve-Path -LiteralPath $AppDirectory).Path
$sourceConsole = Join-Path $appRoot 'Chaptarr.Console.exe'
if (-not (Test-Path -LiteralPath $sourceConsole -PathType Leaf)) {
  throw "The published Windows application is missing Chaptarr.Console.exe at $sourceConsole."
}

if (Get-Service -Name 'Chaptarr' -ErrorAction SilentlyContinue) {
  throw 'A Chaptarr service already exists on this runner; refusing to replace it.'
}

$testRoot = Join-Path $env:RUNNER_TEMP ("chaptarr-service-data-path-{0}" -f [guid]::NewGuid().ToString('N'))
$installRoot = Join-Path $testRoot 'Chaptarr Install With Spaces'
$dataDirectory = Join-Path $testRoot 'Readarr Data With Spaces'
$console = Join-Path $installRoot 'Chaptarr.Console.exe'

try {
  New-Item -ItemType Directory -Force -Path $installRoot, $dataDirectory | Out-Null
  Copy-Item -Path (Join-Path $appRoot '*') -Destination $installRoot -Recurse -Force

  & icacls.exe $installRoot /grant 'NT AUTHORITY\LOCAL SERVICE:(OI)(CI)RX' /T /C | Out-Null
  if ($LASTEXITCODE -ne 0) { throw 'Could not grant the Local Service account read access to the test application.' }
  & icacls.exe $dataDirectory /grant 'NT AUTHORITY\LOCAL SERVICE:(OI)(CI)M' /T /C | Out-Null
  if ($LASTEXITCODE -ne 0) { throw 'Could not grant the Local Service account access to the test data directory.' }

  & $console '/i' "-data=$dataDirectory" '/nobrowser'
  if ($LASTEXITCODE -ne 0) { throw "Chaptarr service installation returned exit code $LASTEXITCODE." }

  $service = Get-Service -Name 'Chaptarr' -ErrorAction Stop
  $deadline = [DateTime]::UtcNow.AddSeconds(90)
  while ($service.Status -ne 'Running' -and [DateTime]::UtcNow -lt $deadline) {
    Start-Sleep -Seconds 2
    $service = Get-Service -Name 'Chaptarr' -ErrorAction Stop
  }
  if ($service.Status -ne 'Running') { throw 'Chaptarr did not reach RUNNING after service installation.' }

  $serviceRecord = Get-CimInstance Win32_Service -Filter "Name='Chaptarr'"
  if (-not $serviceRecord -or $serviceRecord.PathName -notmatch [regex]::Escape($dataDirectory)) {
    throw 'The registered Windows service command does not contain the selected data directory.'
  }

  $configPath = Join-Path $dataDirectory 'config.xml'
  if (-not (Test-Path -LiteralPath $configPath)) {
    throw 'Chaptarr did not create config.xml in the selected data directory.'
  }
  [xml] $config = Get-Content -LiteralPath $configPath
  $port = 8789
  if ($config.Config.Port -and [int]::TryParse($config.Config.Port, [ref] $port) -eq $false) {
    throw "Chaptarr wrote an invalid HTTP port to config.xml: $($config.Config.Port)"
  }

  $ready = $false
  $deadline = [DateTime]::UtcNow.AddSeconds(90)
  while (-not $ready -and [DateTime]::UtcNow -lt $deadline) {
    try {
      $response = Invoke-WebRequest -Uri "http://127.0.0.1:$port/ping" -TimeoutSec 3
      $ready = $response.StatusCode -eq 200
    } catch {
      Start-Sleep -Seconds 2
    }
  }
  if (-not $ready) { throw 'The Chaptarr service did not pass its local health check.' }

  Write-Host 'Windows service install preserved the selected data directory and passed the local health check.'
} finally {
  $service = Get-Service -Name 'Chaptarr' -ErrorAction SilentlyContinue
  if ($service) {
    if ($service.Status -ne 'Stopped') {
      Stop-Service -Name 'Chaptarr' -Force
      $service = Get-Service -Name 'Chaptarr' -ErrorAction Stop
      $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
    }

    & sc.exe delete Chaptarr | Out-Null
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    while ((Get-Service -Name 'Chaptarr' -ErrorAction SilentlyContinue) -and [DateTime]::UtcNow -lt $deadline) {
      Start-Sleep -Milliseconds 500
    }
  }

  if (-not (Get-Service -Name 'Chaptarr' -ErrorAction SilentlyContinue) -and (Test-Path -LiteralPath $testRoot)) {
    Remove-Item -LiteralPath $testRoot -Recurse -Force
  } elseif (Test-Path -LiteralPath $testRoot) {
    Write-Warning "Chaptarr's test service is still registered; keeping its files at $testRoot."
  }
}
