Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Decision {
    param(
        [Parameter(Mandatory = $true)] [ValidateSet('allow', 'ask', 'deny')] [string] $Decision,
        [Parameter(Mandatory = $true)] [string] $Reason,
        [string] $AdditionalContext
    )

    $result = @{
        hookSpecificOutput = @{
            hookEventName = 'PreToolUse'
            permissionDecision = $Decision
            permissionDecisionReason = $Reason
        }
    }

    if ($AdditionalContext) {
        $result.hookSpecificOutput.additionalContext = $AdditionalContext
    }

    $result | ConvertTo-Json -Depth 6 -Compress
}

$rawInput = [Console]::In.ReadToEnd()
if ([string]::IsNullOrWhiteSpace($rawInput)) {
    exit 0
}

try {
    $payload = $rawInput | ConvertFrom-Json -Depth 20
}
catch {
    exit 0
}

$toolName = [string] $payload.tool_name
if ($toolName -notin @('run_in_terminal', 'send_to_terminal', 'execute')) {
    exit 0
}

$toolInput = $payload.tool_input
$command = ''
if ($null -ne $toolInput) {
    if ($toolInput.PSObject.Properties.Name -contains 'command') {
        $command = [string] $toolInput.command
    }
    elseif ($toolInput.PSObject.Properties.Name -contains 'text') {
        $command = [string] $toolInput.text
    }
}

if ([string]::IsNullOrWhiteSpace($command)) {
    exit 0
}

$normalized = $command.ToLowerInvariant()
$denyPatterns = @(
    '(^|[;&|])\s*git\s+add\s+-a(\s|$)',
    '(^|[;&|])\s*git\s+add\s+\.(\s|$)',
    '(^|[;&|])\s*git\s+commit\s+(-a|--all)(\s|$)',
    '(^|[;&|])\s*git\s+reset\s+--hard(\s|$)',
    '(^|[;&|])\s*git\s+clean\s+-f',
    '(^|[;&|])\s*git\s+rebase(\s|$)',
    '(^|[;&|])\s*git\s+merge(\s|$)',
    '(^|[;&|])\s*git\s+cherry-pick(\s|$)',
    '(^|[;&|])\s*git\s+push(\s|$)',
    '(^|[;&|])\s*git\s+pull\s+--rebase(\s|$)',
    '(^|[;&|])\s*git\s+checkout\s+--(\s|$)',
    '(^|[;&|])\s*git\s+stash(\s|$)'
)

foreach ($pattern in $denyPatterns) {
    if ($normalized -match $pattern) {
        Write-Decision -Decision 'deny' -Reason 'Sprint-loop Git policy blocks blanket staging and destructive history changes.' -AdditionalContext 'Use explicit-path staging and let the root orchestrator own commits, branch movement, and recovery decisions.'
        exit 0
    }
}

$askPatterns = @(
    '(^|[;&|])\s*git\s+switch(\s|$)',
    '(^|[;&|])\s*git\s+checkout\s+(?!\-\-)(\S+)'
)

foreach ($pattern in $askPatterns) {
    if ($normalized -match $pattern) {
        Write-Decision -Decision 'ask' -Reason 'Sprint-loop Git policy requires confirmation before branch switches or non-path checkout commands.' -AdditionalContext 'If this is intentional, confirm it explicitly and keep it outside ordinary worker execution.'
        exit 0
    }
}

exit 0
