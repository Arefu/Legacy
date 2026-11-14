# Reads `claude -p --output-format stream-json --verbose` from stdin and prints one short line per step.
# Exit code tells ClaudeQwen.bat what to do next:
#   0 run ended normally, 1 run was killed (Ctrl+C / crash), 2 run failed (start a fresh one),
#   3 the model's last message said ALL DONE, 4 it said STOPPED:
$failed = $false
$finished = $false
$verdict = 0
# Loop guard: small models can get stuck repeating one identical tool call. After LoopLimit identical calls in a row,
# kill this run's claude.exe (found by the prompt file path on its command line) so the launcher starts a fresh run.
$LoopLimit = 4
$lastCall = ''
$repeats = 0
function Stop-ThisRun {
    $tag = $env:QWEN_RUN_TAG
    if (-not $tag) { return }
    Get-CimInstance Win32_Process -Filter "Name='claude.exe'" |
        Where-Object { $_.CommandLine -and $_.CommandLine.Contains($tag) } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
}
$input | ForEach-Object {
    try { $j = $_ | ConvertFrom-Json } catch { return }
    $t = Get-Date -Format HH:mm:ss
    if ($j.type -eq 'assistant') {
        foreach ($c in $j.message.content) {
            if ($c.type -eq 'tool_use') {
                $name = $c.name -replace '^mcp__plugin_ida-pro-mcp_idalib__', 'ida.'
                $in = $c.input
                $color = 'DarkGray'
                switch -Regex ($name) {
                    '^ida\.rename$'                 { $color = 'Green' }
                    '^ida\.(set|append)_comments$'  { $color = 'Cyan' }
                    '^ida\.(set_type|declare_type|make_data)$' { $color = 'Yellow' }
                    '^ida\.decompile$'              { $color = 'White' }
                    '^(Edit|Write)$'                { $color = 'Green' }
                }
                $a = ($in | ConvertTo-Json -Compress -Depth 6)
                $key = "$name $a"
                if ($key -eq $lastCall) { $repeats++ } else { $lastCall = $key; $repeats = 1 }
                if ($a.Length -gt 160) { $a = $a.Substring(0, 160) + '...' }
                Write-Host "$t  $name $a" -ForegroundColor $color
                if ($repeats -ge $LoopLimit) {
                    Write-Host "$t  LOOP: same call $repeats times in a row - ending this run" -ForegroundColor Red
                    $failed = $true
                    Stop-ThisRun
                }
            } elseif ($c.type -eq 'text' -and $c.text.Trim()) {
                Write-Host "$t  > $($c.text.Trim())"
            }
        }
    } elseif ($j.type -eq 'user') {
        foreach ($c in $j.message.content) {
            if ($c.type -eq 'tool_result' -and $c.is_error) {
                $msg = ($c.content | Out-String).Trim()
                if ($msg.Length -gt 200) { $msg = $msg.Substring(0, 200) + '...' }
                Write-Host "$t  ! $msg" -ForegroundColor Red
            }
        }
    } elseif ($j.type -eq 'result') {
        $finished = $true
        if ($j.subtype -eq 'error_max_turns') {
            # Hit --max-turns: the run was working, it just used its step budget. Treat as a normal end.
            Write-Host "$t  run hit the turn limit - starting a fresh one" -ForegroundColor Magenta
        } elseif ($j.is_error) { $failed = $true; Write-Host "$t  RUN FAILED ($($j.subtype)): $($j.result)" -ForegroundColor Red }
        else {
            Write-Host ("$t  run done in {0:N0}s" -f ($j.duration_ms / 1000)) -ForegroundColor Magenta
            # The model's last message decides whether the loop goes on.
            $last = "$($j.result)"
            if ($last -cmatch 'STOPPED:') { $verdict = 4 }
            elseif ($last -cmatch 'ALL DONE') { $verdict = 3 }
        }
    }
}
# No result line = the run was killed (Ctrl+C) or crashed: stop the loop too.
if ($failed) { exit 2 }
if (-not $finished) { exit 1 }
exit $verdict
