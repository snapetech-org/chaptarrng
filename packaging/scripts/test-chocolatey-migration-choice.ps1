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

Write-Host 'Chocolatey migration choice tests passed: No/empty cancel, explicit yes proceeds, and noninteractive/manual modes fail closed.'
