<#
.SYNOPSIS
    Runs dotnet tests with enhanced output filtering and logging.

.DESCRIPTION
    This script runs the KOTORModSync test suite and provides:
    - Colored console output with source file locations
    - Full test output saved to test_failures.log
    - Clickable file references for VS Code
    - Filtering options to focus on failures/warnings

.PARAMETER FailuresOnly
    Only display failed tests (hides warnings and passed tests)

.PARAMETER NoPass
    Hide passed tests (shows failures and warnings only)

.EXAMPLE
    .\run_dotnet_tests.ps1
    Run all tests and show everything

.EXAMPLE
    .\run_dotnet_tests.ps1 -NoPass
    Hide passing tests, show failures and warnings

.EXAMPLE
    .\run_dotnet_tests.ps1 -FailuresOnly
    Only show failed tests
#>

param(
    [switch]$FailuresOnly,         # Only show failures (hide warnings and passes)
    [switch]$NoPass=$true          # Hide passing tests (show failures and warnings)
)

$logFile = "test_failures.log"
# Create/overwrite the logfile
$sw = New-Object System.IO.StreamWriter($logFile, $false, [System.Text.Encoding]::UTF8)
$sw.AutoFlush = $true
$script:logLine = 0

# Display filter settings
if ($FailuresOnly) {
    Write-Host "Filter: Showing FAILURES only" -ForegroundColor Cyan
} elseif ($NoPass) {
    Write-Host "Filter: Showing FAILURES and WARNINGS" -ForegroundColor Cyan
} else {
    Write-Host "Filter: Showing ALL tests (use -NoPass to hide passing, -FailuresOnly for failures only)" -ForegroundColor Cyan
}
Write-Host ""

function Write-Log {
    param([string]$text)
    $script:logLine++
    # Write plain text to file (no line numbers)
    $sw.WriteLine($text)
    return $script:logLine
}

function Extract-SourceLocation {
    param([string[]]$lines)
    # Look for source file location in format: "in C:\path\to\file.cs:line 123"
    # or "   at Namespace.Class.Method() in C:\path\to\file.cs:line 123"
    foreach ($l in $lines) {
        if ($l -match '\s+in\s+(?<file>[^:]+\.cs):line\s+(?<line>\d+)') {
            $file = Split-Path -Leaf $matches['file']
            $lineNum = $matches['line']
            return "${file}:${lineNum}"
        }
    }
    return $null
}

# State machine for detail capture
$script:inCapture = $false
$script:captureBuffer = @()
$script:captureTestName = ""
$script:captureStatus = ""

# Test counters
$script:passedCount = 0
$script:warnCount = 0
$script:failedCount = 0

function Flush-CapturedTest {
    if ($script:captureBuffer.Count -gt 0) {
        # Check if we should display this based on filters
        $shouldDisplay = $true
        if ($FailuresOnly -and $script:captureStatus -notlike "FAILED*") {
            $shouldDisplay = $false
        }

        # Extract source location from the buffer
        $sourceLocation = Extract-SourceLocation -lines $script:captureBuffer

        # Write the test header with source location to log file
        if ($sourceLocation) {
            $ln = Write-Log "$script:captureTestName - $script:captureStatus ($sourceLocation)"
        } else {
            $ln = Write-Log "$script:captureTestName - $script:captureStatus"
        }

        # Write ALL detail lines to log file (no truncation)
        $script:captureBuffer | ForEach-Object { [void](Write-Log ("    " + $_)) }

        # Console output: header + first 25 lines only (if not filtered)
        if ($shouldDisplay) {
            # Only FAILED tests go through this function now
            $color = "Red"

            if ($sourceLocation) {
                Write-Host "$script:captureTestName - $script:captureStatus ($sourceLocation) [${logFile}:${ln}:1]" -ForegroundColor $color
            } else {
                Write-Host "$script:captureTestName - $script:captureStatus (${logFile}:${ln}:1)" -ForegroundColor $color
            }

            # Show first 25 lines in console for quick feedback
            $script:captureBuffer |
                Select-Object -First 25 |
                ForEach-Object { Write-Host "    $_" -ForegroundColor DarkGray }
            if ($script:captureBuffer.Count -gt 25) {
                Write-Host "    ... ($($script:captureBuffer.Count - 25) more lines in ${logFile})" -ForegroundColor DarkGray
            }
        }
    }
}

try {
    dotnet test KOTORModSync.Tests/KOTORModSync.Tests.csproj --logger "console;verbosity=normal" |
    ForEach-Object {
        $line = $_

        if ($line -match '^\s*Passed\s+(?<name>\S+)') {
            # Flush any pending capture before handling new test
            if ($script:inCapture) {
                Flush-CapturedTest
            }
            $script:passedCount++
            # Only show passed tests if not filtered out
            if (-not $NoPass -and -not $FailuresOnly) {
                Write-Host "$($matches['name']) - passed" -ForegroundColor Green
            }
            $script:inCapture = $false
        }
        elseif ($line -match '^\s*Skipped\s+(?<name>\S+)') {
            # Flush any pending capture before handling new test
            if ($script:inCapture) {
                Flush-CapturedTest
            }
            $script:warnCount++
            $testName = $matches['name']

            # Skipped tests don't execute, so they have no output to capture
            # Just log them directly without starting capture mode
            $ln = Write-Log "$testName - SKIPPED"

            if (-not $FailuresOnly) {
                Write-Host "$testName - SKIPPED (${logFile}:${ln}:1)" -ForegroundColor Yellow
            }

            $script:inCapture = $false
        }
        elseif ($line -match '^\s*Failed\s+(?<name>\S+)') {
            # Flush any pending capture before handling new test
            if ($script:inCapture) {
                Flush-CapturedTest
            }
            $script:failedCount++
            $script:inCapture = $true
            $script:captureBuffer = @()
            $script:captureTestName = $matches['name']
            $script:captureStatus = "FAILED"
        }
        elseif ($script:inCapture) {
            # End of a detail block when blank line or summary appears
            if ([string]::IsNullOrWhiteSpace($line) -or $line -match '^\s*Total tests:') {
                Flush-CapturedTest
                $script:inCapture = $false
            } else {
                $script:captureBuffer += $line
            }
        }
        else {
            # Ignore other noise
        }
    }

    # Flush any trailing capture block if the stream ends without a blank line
    if ($script:inCapture) {
        Flush-CapturedTest
        $script:inCapture = $false
    }
}
finally {
    $sw.Dispose()

    # Display summary
    Write-Host ""
    Write-Host "================================" -ForegroundColor Cyan
    $totalTests = $script:passedCount + $script:warnCount + $script:failedCount
    Write-Host "Total Tests: $totalTests" -ForegroundColor Cyan
    if ($script:passedCount -gt 0) {
        Write-Host "  Passed: $($script:passedCount)" -ForegroundColor Green
    }
    if ($script:warnCount -gt 0) {
        Write-Host "  Skipped: $($script:warnCount)" -ForegroundColor Yellow
    }
    if ($script:failedCount -gt 0) {
        Write-Host "  Failed: $($script:failedCount)" -ForegroundColor Red
    }
    Write-Host "================================" -ForegroundColor Cyan

    if ($script:failedCount -gt 0) {
        Write-Host "See $logFile for failure details" -ForegroundColor Cyan
    }
}