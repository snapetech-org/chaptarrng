param(
    [Parameter(Mandatory = $true)]
    [string] $BackupPath,

    [Parameter(Mandatory = $true)]
    [string] $DataDirectory
)

$ErrorActionPreference = 'Stop'
$BackupPath = [IO.Path]::GetFullPath($BackupPath)
$DataDirectory = [IO.Path]::GetFullPath($DataDirectory)
$backupBin = Join-Path $BackupPath 'bin'
$backupReadarrConsole = Join-Path $backupBin 'Readarr.Console.exe'

if (-not (Test-Path -LiteralPath $backupBin -PathType Container) -or
    -not (Test-Path -LiteralPath $backupReadarrConsole -PathType Leaf) -or
    -not (Test-Path -LiteralPath (Join-Path $BackupPath 'readarr.db') -PathType Leaf)) {
    throw "The BookshelfNG rollback backup is incomplete: $BackupPath"
}

function Stop-And-DeleteService([string] $Name) {
    $service = Get-Service -Name $Name -ErrorAction SilentlyContinue
    if (-not $service) { return }

    Write-Host "Stopping $Name service..."
    if ($service.Status -ne 'Stopped') {
        Stop-Service -Name $Name -Force
        $service = Get-Service -Name $Name -ErrorAction Stop
        $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
    }

    Write-Host "Removing $Name service registration..."
    & sc.exe delete $Name | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "sc.exe delete $Name failed with exit code $LASTEXITCODE."
    }

    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    while ((Get-Service -Name $Name -ErrorAction SilentlyContinue) -and [DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 250
    }
    if (Get-Service -Name $Name -ErrorAction SilentlyContinue) {
        throw "$Name is still registered after a delete request."
    }
}

function Assert-ServiceTargetsDataDirectory([string] $Name) {
    $keyPath = "HKLM:\SYSTEM\CurrentControlSet\Services\$Name"
    $serviceKey = Get-ItemProperty -Path $keyPath -ErrorAction SilentlyContinue
    if (-not $serviceKey) { return }

    $imagePath = [Environment]::ExpandEnvironmentVariables([string] $serviceKey.ImagePath)
    if ($imagePath.IndexOf($DataDirectory, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
        throw "Refusing to remove $Name because its service command does not target $DataDirectory."
    }
}

Write-Host "Restoring BookshelfNG from $BackupPath"
Assert-ServiceTargetsDataDirectory 'Chaptarr'
Assert-ServiceTargetsDataDirectory 'Readarr'
Stop-And-DeleteService 'Chaptarr'
Stop-And-DeleteService 'Readarr'

$currentBin = Join-Path $DataDirectory 'bin'
if (Test-Path -LiteralPath $currentBin) {
    Remove-Item -LiteralPath $currentBin -Recurse -Force
}
Copy-Item -LiteralPath $backupBin -Destination $currentBin -Recurse -Force
$readarrConsole = Join-Path $currentBin 'Readarr.Console.exe'
if (-not (Test-Path -LiteralPath $readarrConsole -PathType Leaf)) {
    throw "The restored BookshelfNG executable is missing from $currentBin."
}

foreach ($name in @('config.xml', 'readarr.db', 'readarr.db-journal', 'readarr.db-wal')) {
    $current = Join-Path $DataDirectory $name
    $source = Join-Path $BackupPath $name
    if (Test-Path -LiteralPath $current) {
        Remove-Item -LiteralPath $current -Force
    }
    if (Test-Path -LiteralPath $source -PathType Leaf) {
        Copy-Item -LiteralPath $source -Destination $current -Force
    }
}

$currentBackups = Join-Path $DataDirectory 'Backups'
$savedBackups = Join-Path $BackupPath 'Backups'
if (Test-Path -LiteralPath $currentBackups) {
    Remove-Item -LiteralPath $currentBackups -Recurse -Force
}
if (Test-Path -LiteralPath $savedBackups -PathType Container) {
    Copy-Item -LiteralPath $savedBackups -Destination $currentBackups -Recurse -Force
}

Write-Host 'Registering the restored Readarr service...'
& $readarrConsole '/i' "-data=$DataDirectory" '/nobrowser'
if ($LASTEXITCODE -ne 0) {
    throw "Readarr.Console.exe service installation failed with exit code $LASTEXITCODE."
}

$readarrServiceKey = 'HKLM:\SYSTEM\CurrentControlSet\Services\Readarr'
$serviceKey = Get-ItemProperty -Path $readarrServiceKey -ErrorAction SilentlyContinue
if (-not $serviceKey) {
    throw 'Readarr service registration was not created by the BookshelfNG installer.'
}

# BookshelfNG 0.4.21.71 registers its executable without quoting the image
# path. The restored executable and data directory may contain spaces, so
# correct the command line before asking the Service Control Manager to start
# the service. Register from the restored appdata bin directory rather than
# leaving the service dependent on a retained backup folder.
$quotedReadarrConsole = '"' + $readarrConsole + '"'
$quotedDataDirectory = '"' + $DataDirectory + '"'
$readarrImagePath = "$quotedReadarrConsole -data=$quotedDataDirectory /nobrowser"
Set-ItemProperty -Path $readarrServiceKey -Name ImagePath -Value $readarrImagePath -ErrorAction Stop

$serviceKey = Get-ItemProperty -Path $readarrServiceKey -ErrorAction Stop
if (-not [string]::Equals([string] $serviceKey.ImagePath, $readarrImagePath, [StringComparison]::Ordinal)) {
    throw 'The restored Readarr service command did not retain its quoted executable and data paths.'
}

$serviceAccount = [string] $serviceKey.ObjectName
if (-not [string]::Equals($serviceAccount, 'NT AUTHORITY\LocalService', [StringComparison]::OrdinalIgnoreCase)) {
    throw "The restored Readarr service uses unsupported account '$serviceAccount'."
}

$service = Get-Service -Name 'Readarr' -ErrorAction Stop
if ($service.Status -ne 'Stopped') {
    Stop-Service -Name 'Readarr' -Force -ErrorAction Stop
    $service = Get-Service -Name 'Readarr' -ErrorAction Stop
    $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
}

Write-Host 'Starting the restored Readarr service with its quoted executable path...'
Start-Service -Name 'Readarr' -ErrorAction Stop

$deadline = [DateTime]::UtcNow.AddSeconds(60)
do {
    $service = Get-Service -Name 'Readarr' -ErrorAction SilentlyContinue
    if ($service -and $service.Status -eq 'Running') { break }
    Start-Sleep -Seconds 1
} while ([DateTime]::UtcNow -lt $deadline)

if (-not $service -or $service.Status -ne 'Running') {
    throw 'The restored Readarr service did not reach RUNNING.'
}

try {
    Set-ItemProperty -Path 'HKLM:\SOFTWARE\ChaptarrNG\BookshelfMigration' -Name LastRollbackTime -Value (Get-Date -Format 'o') -ErrorAction Stop
} catch {
    Write-Warning "Could not record the rollback timestamp: $($_.Exception.Message)"
}

Write-Host 'BookshelfNG rollback completed and the Readarr service is running.'
