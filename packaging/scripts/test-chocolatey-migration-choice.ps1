$ErrorActionPreference = 'Stop'

$templatePath = Join-Path $PSScriptRoot '../legacy/chocolatey/tools/chocolateyinstall.ps1.in'
$template = Get-Content -LiteralPath $templatePath -Raw
$functionMatch = [regex]::Match(
  $template,
  '(?ms)^function Confirm-BookshelfMigrationChoice \{\r?\n.*?^\}')
if (-not $functionMatch.Success) {
  throw 'Could not find Confirm-BookshelfMigrationChoice in the Chocolatey install template.'
}
. ([scriptblock]::Create($functionMatch.Value))

$parseTokens = $null
$parseErrors = $null
$installAst = [System.Management.Automation.Language.Parser]::ParseInput($template, [ref] $parseTokens, [ref] $parseErrors)
if ($parseErrors.Count -gt 0) {
  throw 'The Chocolatey install template contains PowerShell parse errors.'
}
$utilityFunction = $installAst.Find({
    param($node)
    $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
      $node.Name -eq 'Invoke-BookshelfUtility'
  }, $true)
if (-not $utilityFunction) {
  throw 'Could not find Invoke-BookshelfUtility in the Chocolatey install template.'
}
. ([scriptblock]::Create($utilityFunction.Extent.Text))

function Assert-Condition {
  param([bool] $Condition, [string] $Message)
  if (-not $Condition) { throw $Message }
}

$script:PromptCalls = 0
$script:PromptMessage = ''
$script:PromptResponse = ''
$testPrompt = {
  param([string] $Message)
  $script:PromptCalls++
  $script:PromptMessage = $Message
  return $script:PromptResponse
}

function Invoke-ChoiceCase {
  param(
    [string] $Response,
    [bool] $ExpectedToProceed
  )

  $script:PromptCalls = 0
  $script:PromptMessage = ''
  $script:PromptResponse = $Response
  $stagingReached = $false
  $failure = $null
  try {
    Confirm-BookshelfMigrationChoice `
      -HasMigrationData $true `
      -Mode 'ask' `
      -InputIsRedirected $false `
      -Prompt $testPrompt
    $stagingReached = $true
  } catch {
    $failure = $_.Exception.Message
  }

  Assert-Condition ($script:PromptCalls -eq 1) 'Ask mode did not show exactly one prompt.'
  Assert-Condition ($script:PromptMessage -match '\[y/N\]$') 'The prompt does not advertise its default-No choice.'
  if ($ExpectedToProceed) {
    Assert-Condition $stagingReached "Explicit yes '$Response' did not continue to package staging."
    Assert-Condition ($null -eq $failure) "Explicit yes '$Response' was rejected: $failure"
  } else {
    Assert-Condition (-not $stagingReached) "Response '$Response' continued into package staging."
    Assert-Condition ($failure -like 'BookshelfNG replacement cancelled before staging files*') `
      "Response '$Response' did not cancel before package staging."
  }
}

Invoke-ChoiceCase -Response 'n' -ExpectedToProceed $false
Invoke-ChoiceCase -Response 'N' -ExpectedToProceed $false
Invoke-ChoiceCase -Response '' -ExpectedToProceed $false
Invoke-ChoiceCase -Response 'y' -ExpectedToProceed $true
Invoke-ChoiceCase -Response 'YES' -ExpectedToProceed $true

$script:PromptCalls = 0
try {
  Confirm-BookshelfMigrationChoice `
    -HasMigrationData $true `
    -Mode 'ask' `
    -InputIsRedirected $true `
    -Prompt $testPrompt
  throw 'Ask mode unexpectedly accepted redirected input.'
} catch {
  if ($_.Exception.Message -notlike 'MigrationMode=ask requires an interactive terminal*') { throw }
}
Assert-Condition ($script:PromptCalls -eq 0) 'Ask mode prompted despite redirected input.'

$script:PromptCalls = 0
Confirm-BookshelfMigrationChoice `
  -HasMigrationData $false `
  -Mode 'ask' `
  -InputIsRedirected $true `
  -Prompt $testPrompt
Assert-Condition ($script:PromptCalls -eq 0) 'A fresh install prompted without BookshelfNG migration data.'

$script:PromptCalls = 0
try {
  Confirm-BookshelfMigrationChoice `
    -HasMigrationData $true `
    -Mode 'manual' `
    -InputIsRedirected $false `
    -Prompt $testPrompt
  throw 'Manual mode unexpectedly accepted the package transition.'
} catch {
  if ($_.Exception.Message -notlike 'MigrationMode=manual cancelled this package update*') { throw }
}
Assert-Condition ($script:PromptCalls -eq 0) 'Manual mode prompted for an automatic package transition.'

$temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) ("bookshelfng-chocolatey-utility-{0}" -f [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporaryDirectory | Out-Null
try {
  if ([Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT) {
    $fakeUtility = Join-Path $temporaryDirectory 'fake-utility.cmd'
    @(
      '@echo off',
      'if /I not "%2"=="-exitimmediately" exit /b 99',
      'echo Utility output must not replace the exit code',
      'exit /b 7'
    ) | Set-Content -LiteralPath $fakeUtility -Encoding ascii
  } else {
    $fakeUtility = Join-Path $temporaryDirectory 'fake-utility.sh'
    @(
      '#!/bin/sh',
      '[ "$2" = "-exitimmediately" ] || exit 99',
      'echo Utility output must not replace the exit code',
      'exit 7'
    ) | Set-Content -LiteralPath $fakeUtility -Encoding ascii
    & chmod +x $fakeUtility
    if ($LASTEXITCODE -ne 0) { throw 'Could not make the fake utility executable.' }
  }

  $utilityResult = @(Invoke-BookshelfUtility -Executable $fakeUtility -Arguments @('fixture-argument') -EnvironmentEntries @())
  Assert-Condition ($utilityResult.Count -eq 1) 'Utility stdout leaked into the returned exit-code value.'
  Assert-Condition ([int] $utilityResult[0] -eq 7) 'Utility did not preserve its process exit code.'
  $global:LASTEXITCODE = 0
} finally {
  Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host 'Chocolatey migration tests passed: No/empty cancel, explicit yes proceeds, utility output preserves exit codes, and failures exit without waiting for Enter.'
