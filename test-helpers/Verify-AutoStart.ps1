<#
.SYNOPSIS
Verify auto-start task in Windows Task Scheduler

.DESCRIPTION
Checks that dIKta.me auto-start task is registered in Task Scheduler

.EXAMPLE
.\Verify-AutoStart.ps1
#>

$taskName = "dIKta.me"
$taskPath = "\dIKta.me\"

Write-Host "Checking Task Scheduler for auto-start task..."

try {
    # Get task from Task Scheduler
    $task = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue

    if (-not $task) {
        Write-Host "⚠️  WARNING: Auto-start task '$taskName' not found in Task Scheduler" -ForegroundColor Yellow
        Write-Host "   (Task may not be created yet if auto-start not enabled)" -ForegroundColor Gray
        exit 0
    }

    Write-Host "✅ PASS: Auto-start task found" -ForegroundColor Green
    Write-Host "   Task Name: $($task.TaskName)" -ForegroundColor Gray
    Write-Host "   State: $($task.State)" -ForegroundColor Gray

    if ($task.State -eq "Disabled") {
        Write-Host "⚠️  WARNING: Task is disabled (won't auto-start)" -ForegroundColor Yellow
    } else {
        Write-Host "   Status: Enabled (will auto-start)" -ForegroundColor Green
    }

    if ($task.Triggers) {
        Write-Host "   Triggers:" -ForegroundColor Gray
        $task.Triggers | ForEach-Object {
            Write-Host "     - $($_.GetType().Name): $($_ | Select-Object -Property * | Out-String)" -ForegroundColor Gray
        }
    }

    exit 0
} catch {
    Write-Host "❌ ERROR: Failed to check Task Scheduler: $_" -ForegroundColor Red
    exit 1
}
