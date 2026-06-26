# sync-codegraph-on-edit.ps1
# Companion script for post-codechange-codegraph-sync.json hook.
# Reads PostToolUse hook stdin to determine if a file-modifying tool was used,
# and only runs `codegraph sync` when files actually changed.

param()

$inputJson = @($input) -join "`n" | ConvertFrom-Json -ErrorAction SilentlyContinue

if (-not $inputJson) {
    # Can't parse input — fall back to safe sync
    & codegraph sync
    exit 0
}

# Tools that modify files (write operations)
$writeTools = @(
    'create_file',
    'create_new_jupyter_notebook',
    'create_new_workspace',
    'create_directory',
    'replace_string_in_file',
    'insert_edit_into_file',
    'edit_notebook_file',
    'delete_file',
    'rename_file',
    'copy_file',
    'write_file'
)

$toolName = $inputJson.toolUse.name
$isWriteTool = $toolName -and ($writeTools -contains $toolName)

if ($isWriteTool) {
    & codegraph sync
}
