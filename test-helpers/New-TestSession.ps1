<#
.SYNOPSIS
Initialize a new test session

.DESCRIPTION
Creates a fresh test-session-log.md file with timestamp header and checklist copy

.EXAMPLE
.\New-TestSession.ps1
#>

$sessionLogPath = "test-session-log.md"
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
$date = Get-Date -Format "yyyy-MM-dd"

$header = @"
# Test Session — $date

**Started:** $timestamp
**Tester:** $(whoami)
**Environment:** $(hostname) / Windows

---

## Quick Reference
- Edit this file to check off items as you test
- Use `- [x]` to mark complete, `- [ ]` to mark pending
- Add notes in the bugs/observations sections
- This file is gitignored (won't appear in commits)

---

## Test Sections Completed

- [ ] Section 1: First-Run Experience
- [ ] Section 2: Core Dictation Workflows
- [ ] Section 3: Advanced Modes
- [ ] Section 4: Audio System
- [ ] Section 5: Settings Management
- [ ] Section 6: Data & Privacy
- [ ] Section 7: System Integration
- [ ] Section 8: Error Handling & Edge Cases
- [ ] Section 9: Performance & Polish
- [ ] Section 10: Automated Voice Testing

---

## Bugs Found

(Use format: `- [ ] [ID] Description` to track)

---

## Observations

(General notes about behavior, performance, polish, etc.)

---

## Follow-up Items

(Specific tests to re-run or edge cases to investigate)

---

**Use MANUAL_TEST_PLAN.md as master checklist**

"@

# Write the session log
$header | Set-Content $sessionLogPath -Encoding UTF8

Write-Host "✅ Test session initialized: $sessionLogPath" -ForegroundColor Green
Write-Host "   Timestamp: $timestamp" -ForegroundColor Gray
Write-Host "   Next: Open MANUAL_TEST_PLAN.md and start testing" -ForegroundColor Yellow
