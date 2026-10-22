param(
    [Parameter(Mandatory = $true)]
    [string] $BackupPath,

    [Parameter(Mandatory = $true)]
    [string] $DataDirectory,

    [string] $ChaptarrConsolePath,

    [switch] $RestorePostgresDatabase,

    [int] $BookshelfServiceStartType = 0,

    [int] $BookshelfServiceWasRunning = -1
)

$ErrorActionPreference = 'Stop'
$BackupPath = [IO.Path]::GetFullPath($BackupPath)
$DataDirectory = [IO.Path]::GetFullPath($DataDirectory)
$backupBin = Join-Path $BackupPath 'bin'
$backupReadarrConsole = Join-Path $backupBin 'Readarr.Console.exe'
$postgresDump = Join-Path $BackupPath 'postgres-main.dump'
$migrationRecordPath = 'HKLM:\SOFTWARE\ChaptarrNG\BookshelfMigration'
$migrationRecord = Get-ItemProperty -LiteralPath $migrationRecordPath -ErrorAction SilentlyContinue
$savedStartType = if ($BookshelfServiceStartType -in @(2, 3, 4)) {
    $BookshelfServiceStartType
} elseif ($null -ne $migrationRecord -and $null -ne $migrationRecord.BookshelfServiceStartType) {
    [int] $migrationRecord.BookshelfServiceStartType
} else {
    2
}
$bookshelfWasRunning = if ($BookshelfServiceWasRunning -in @(0, 1)) {
    $BookshelfServiceWasRunning -ne 0
} elseif ($null -ne $migrationRecord -and $null -ne $migrationRecord.BookshelfServiceWasRunning) {
    [int] $migrationRecord.BookshelfServiceWasRunning -ne 0
} else {
    $true
}
$startTypeName = switch ($savedStartType) {
    2 { 'auto' }
    3 { 'demand' }
    4 { 'disabled' }
    default { throw "The saved BookshelfNG service start type '$savedStartType' is unsupported." }
}

if (-not (Test-Path -LiteralPath $backupBin -PathType Container) -or
    -not (Test-Path -LiteralPath $backupReadarrConsole -PathType Leaf) -or
    (-not (Test-Path -LiteralPath (Join-Path $BackupPath 'readarr.db') -PathType Leaf) -and
     -not (Test-Path -LiteralPath $postgresDump -PathType Leaf))) {
    throw "The BookshelfNG rollback backup is incomplete: $BackupPath"
}
if ($RestorePostgresDatabase -and -not (Test-Path -LiteralPath $postgresDump -PathType Leaf)) {
    throw "The independent PostgreSQL main-database backup is missing: $postgresDump"
}

function Stop-And-DeleteService([string] $Name) {
    $service = Get-Service -Name $Name -ErrorAction SilentlyContinue
    if (-not $service) { return }

    $serviceProcessId = [int] (Get-CimInstance -ClassName Win32_Service -Filter "Name = '$Name'" -ErrorAction Stop).ProcessId

    try {
        Write-Host "Stopping $Name service..."
        if ($service.Status -ne 'Stopped') {
            $service.Stop()
            $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
        }

        if ($serviceProcessId -gt 0) {
            $serviceProcess = Get-Process -Id $serviceProcessId -ErrorAction SilentlyContinue
            if ($serviceProcess) {
                try {
                    if (-not $serviceProcess.WaitForExit(15000)) {
                        throw "$Name service process $serviceProcessId did not exit after the service reported STOPPED."
                    }
                } finally {
                    $serviceProcess.Dispose()
                }
            }
        }
    } finally {
        # Release this exact ServiceController before asking SCM to delete it.
        $service.Dispose()
        $service = $null
    }

    Write-Host "Removing $Name service registration..."
    & sc.exe delete $Name | Out-Null
    if ($LASTEXITCODE -ne 0 -and $LASTEXITCODE -ne 1072) {
        throw "sc.exe delete $Name failed with exit code $LASTEXITCODE."
    }
    if ($LASTEXITCODE -eq 1072) {
        Write-Host "$Name is already marked for deletion; waiting for Windows to release it..."
    }

    $serviceKeyPath = "HKLM:\SYSTEM\CurrentControlSet\Services\$Name"
    $deadline = [DateTime]::UtcNow.AddSeconds(60)
    # Query the registry while deletion is pending. Get-Service opens an SCM
    # handle, which can keep the marked-for-deletion registration alive.
    while ((Test-Path -LiteralPath $serviceKeyPath) -and [DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 500
    }
    if (Test-Path -LiteralPath $serviceKeyPath) {
        throw "$Name is still registered after a delete request."
    }
}

function Stop-ServiceOnly([string] $Name) {
    $service = Get-Service -Name $Name -ErrorAction SilentlyContinue
    if (-not $service) { return }

    $serviceProcessId = 0
    $serviceInfo = Get-CimInstance -ClassName Win32_Service -Filter "Name = '$Name'" -ErrorAction SilentlyContinue
    if ($serviceInfo) {
        $serviceProcessId = [int] $serviceInfo.ProcessId
        $serviceInfo = $null
    }

    try {
        if ($service.Status -ne 'Stopped') {
            Write-Host "Stopping $Name service while preserving its registration..."
            if ($service.Status -ne 'StopPending') { $service.Stop() }
            $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
        }
    } finally {
        $service.Dispose()
        $service = $null
    }

    if ($serviceProcessId -gt 0) {
        $serviceProcess = Get-Process -Id $serviceProcessId -ErrorAction SilentlyContinue
        if ($serviceProcess) {
            try {
                if (-not $serviceProcess.WaitForExit(15000)) {
                    throw "$Name service process $serviceProcessId did not exit after reporting STOPPED."
                }
            } finally {
                $serviceProcess.Dispose()
                $serviceProcess = $null
            }
        }
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
Stop-ServiceOnly 'Readarr'

if ($RestorePostgresDatabase) {
    if ([string]::IsNullOrWhiteSpace($ChaptarrConsolePath)) {
        $ChaptarrConsolePath = Join-Path $DataDirectory 'bin\Chaptarr.Console.exe'
    }
    if (-not (Test-Path -LiteralPath $ChaptarrConsolePath -PathType Leaf)) {
        throw "The ChaptarrNG utility needed to restore the PostgreSQL backup was not found: $ChaptarrConsolePath"
    }

    Write-Host 'Restoring the independent PostgreSQL database backups before restoring BookshelfNG binaries...'
    & $ChaptarrConsolePath '-restore-bookshelf-postgres' "-bookshelf-postgres-backup=$BackupPath" "-data=$BackupPath" '-confirm-bookshelf-service-stopped'
    if ($LASTEXITCODE -ne 0) {
        throw "ChaptarrNG PostgreSQL backup restore exited with code $LASTEXITCODE. The dump is preserved at $postgresDump."
    }
}

$currentBin = Join-Path $DataDirectory 'bin'
if (Test-Path -LiteralPath $currentBin) {
    Remove-Item -LiteralPath $currentBin -Recurse -Force
}
Copy-Item -LiteralPath $backupBin -Destination $currentBin -Recurse -Force
$readarrConsole = Join-Path $currentBin 'Readarr.Console.exe'
if (-not (Test-Path -LiteralPath $readarrConsole -PathType Leaf)) {
    throw "The restored BookshelfNG executable is missing from $currentBin."
}

$hasSqliteBackup = Test-Path -LiteralPath (Join-Path $BackupPath 'readarr.db') -PathType Leaf
$restoreFileNames = if ($hasSqliteBackup) {
    @('config.xml', 'readarr.db', 'readarr.db-journal', 'readarr.db-wal')
} else {
    @('config.xml')
}
foreach ($name in $restoreFileNames) {
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

if (-not (Test-Path -LiteralPath 'HKLM:\SYSTEM\CurrentControlSet\Services\Readarr')) {
    Write-Host 'Re-registering the restored Readarr service...'
    # The deferred successful-handoff cleanup may already have removed the old
    # registration. Recreate it with the same service identity and saved policy.
    $imagePathArgument = '"\"' + $readarrConsole + '\" -data=\"' + $DataDirectory + '\" /nobrowser"'
    $createArguments = "create Readarr DisplayName= `"Readarr`" binpath= $imagePathArgument start= $startTypeName depend= EventLog/Tcpip/http obj= `"NT AUTHORITY\LocalService`""
    $createProcess = Start-Process -FilePath 'sc.exe' -ArgumentList $createArguments -Wait -PassThru -NoNewWindow
    if ($createProcess.ExitCode -ne 0) {
        throw "sc.exe could not create the restored Readarr service (exit code $($createProcess.ExitCode))."
    }

    $descriptionProcess = Start-Process -FilePath 'sc.exe' -ArgumentList 'description Readarr "Readarr Application Server"' -Wait -PassThru -NoNewWindow
    if ($descriptionProcess.ExitCode -ne 0) {
        throw "sc.exe could not set the restored Readarr service description (exit code $($descriptionProcess.ExitCode))."
    }

    $daclLines = & sc.exe sdshow Readarr
    if ($LASTEXITCODE -ne 0) {
        throw "sc.exe could not read the restored Readarr service permissions (exit code $LASTEXITCODE)."
    }
    $serviceDacl = ($daclLines | Where-Object { $_ -match '^D:' } | Select-Object -First 1).Trim()
    if ([string]::IsNullOrWhiteSpace($serviceDacl)) {
        throw 'The restored Readarr service returned an invalid security descriptor.'
    }

    $authenticatedUsersDacl = '(A;;CCLCSWRPWPLOCRRC;;;AU)'
    if (-not $serviceDacl.Contains($authenticatedUsersDacl)) {
        $indexOfS = $serviceDacl.IndexOf('S:', [StringComparison]::OrdinalIgnoreCase)
        $serviceDacl = if ($indexOfS -eq -1) {
            "$serviceDacl$authenticatedUsersDacl"
        } else {
            $serviceDacl.Insert($indexOfS, $authenticatedUsersDacl)
        }

        $permissionsProcess = Start-Process -FilePath 'sc.exe' -ArgumentList "sdset Readarr $serviceDacl" -Wait -PassThru -NoNewWindow
        if ($permissionsProcess.ExitCode -ne 0) {
            throw "sc.exe could not set the restored Readarr service permissions (exit code $($permissionsProcess.ExitCode))."
        }
    }
}

$readarrServiceKey = 'HKLM:\SYSTEM\CurrentControlSet\Services\Readarr'
$serviceKey = Get-ItemProperty -Path $readarrServiceKey -ErrorAction Stop
if ([Environment]::ExpandEnvironmentVariables([string] $serviceKey.ImagePath).IndexOf($DataDirectory, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
    throw 'The restored Readarr service command does not target the restored data directory.'
}

$serviceAccount = [string] $serviceKey.ObjectName
if (-not [string]::Equals($serviceAccount, 'NT AUTHORITY\LocalService', [StringComparison]::OrdinalIgnoreCase)) {
    throw "The restored Readarr service uses unsupported account '$serviceAccount'."
}

$restoreStart = Start-Process -FilePath 'sc.exe' -ArgumentList "config Readarr start= $startTypeName" -Wait -PassThru -NoNewWindow
if ($restoreStart.ExitCode -ne 0) {
    throw "sc.exe could not restore the Readarr startup policy (exit code $($restoreStart.ExitCode))."
}

if ($bookshelfWasRunning) {
    if ($savedStartType -eq 4) {
        $temporaryStart = Start-Process -FilePath 'sc.exe' -ArgumentList 'config Readarr start= demand' -Wait -PassThru -NoNewWindow
        if ($temporaryStart.ExitCode -ne 0) {
            throw "Could not temporarily enable the previously running Readarr service (exit code $($temporaryStart.ExitCode))."
        }
    }

    Write-Host 'Starting the restored BookshelfNG service...'
    Start-Service -Name 'Readarr' -ErrorAction Stop
    $service = Get-Service -Name 'Readarr' -ErrorAction Stop
    try {
        $service.WaitForStatus('Running', [TimeSpan]::FromSeconds(60))
        if ($service.Status -ne 'Running') {
            throw 'The restored Readarr service did not reach RUNNING.'
        }
    } finally {
        $service.Dispose()
        $service = $null
    }

    if ($savedStartType -eq 4) {
        $disableProcess = Start-Process -FilePath 'sc.exe' -ArgumentList 'config Readarr start= disabled' -Wait -PassThru -NoNewWindow
        if ($disableProcess.ExitCode -ne 0) {
            throw "The restored running Readarr service could not recover its disabled startup policy (exit code $($disableProcess.ExitCode))."
        }
    }
} else {
    Write-Host "BookshelfNG was stopped before migration; leaving Readarr stopped with its original '$startTypeName' startup policy."
}

try {
    Set-ItemProperty -Path 'HKLM:\SOFTWARE\ChaptarrNG\BookshelfMigration' -Name LastRollbackTime -Value (Get-Date -Format 'o') -ErrorAction Stop
} catch {
    Write-Warning "Could not record the rollback timestamp: $($_.Exception.Message)"
}

Write-Host 'BookshelfNG rollback completed; the Readarr registration and original startup state were restored.'
