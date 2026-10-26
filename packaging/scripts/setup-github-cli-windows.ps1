$ErrorActionPreference = 'Stop'

if (Get-Command gh -ErrorAction SilentlyContinue) {
    gh --version
    exit 0
}

if ($env:RUNNER_OS -ne 'Windows' -or $env:RUNNER_ARCH -ne 'X64') {
    throw 'GitHub CLI setup requires the private Windows x64 runner.'
}

$version = '2.103.2'
$archiveName = "gh_${version}_windows_amd64.zip"
$archiveUrl = "https://github.com/cli/cli/releases/download/v${version}/${archiveName}"
$archiveSha256 = 'fef9fcdd8fdabd0501ab8b3d99eac976f504ab55d01bef9e8ef428a5d900ddda'
$archivePath = Join-Path $env:RUNNER_TEMP $archiveName
$installRoot = Join-Path $env:RUNNER_TEMP "gh-cli-${version}"

Invoke-WebRequest -Uri $archiveUrl -OutFile $archivePath
$actualSha256 = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualSha256 -ne $archiveSha256) {
    throw 'The downloaded GitHub CLI archive checksum does not match the pinned release.'
}

New-Item -ItemType Directory -Force -Path $installRoot | Out-Null
Expand-Archive -LiteralPath $archivePath -DestinationPath $installRoot -Force
$ghExecutable = Get-ChildItem -LiteralPath $installRoot -Filter 'gh.exe' -File -Recurse | Select-Object -First 1
if ($null -eq $ghExecutable) {
    throw 'The pinned GitHub CLI archive did not contain gh.exe.'
}

$ghDirectory = $ghExecutable.Directory.FullName
[System.IO.File]::AppendAllText(
    $env:GITHUB_PATH,
    "$ghDirectory$([Environment]::NewLine)",
    [System.Text.UTF8Encoding]::new($false)
)
$env:Path = "$ghDirectory;$env:Path"
& $ghExecutable.FullName --version
if ($LASTEXITCODE -ne 0) {
    throw 'The installed GitHub CLI could not start.'
}

Remove-Item -LiteralPath $archivePath -Force
