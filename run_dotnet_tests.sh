#!/usr/bin/env bash

# Runs dotnet tests with enhanced output filtering and logging.
# Features:
#   - Colored console output with source file locations
#   - Full test output saved to test_failures.log
#   - Clickable file references for VS Code
#   - Filtering options to focus on failures/warnings
#
# Usage:
#   ./run_dotnet_tests.sh [--failures-only] [--no-pass]
#
#   --failures-only   Only display failed tests (hides warnings and passed tests)
#   --no-pass         Hide passed tests (shows failures and warnings only)
#
# Default: --no-pass is enabled unless overridden.

set -euo pipefail

LOGFILE="test_failures.log"
: > "$LOGFILE"

# Default options
FAILURES_ONLY=0
NO_PASS=1

# Parse arguments
for arg in "$@"; do
    case "$arg" in
        --failures-only)
            FAILURES_ONLY=1
            NO_PASS=1
            ;;
        --no-pass)
            NO_PASS=1
            ;;
        --all)
            FAILURES_ONLY=0
            NO_PASS=0
            ;;
        *)
            echo "Unknown argument: $arg"
            exit 1
            ;;
    esac
done

# Color codes
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
GRAY='\033[1;30m'
NC='\033[0m'

# Display filter settings
if [[ $FAILURES_ONLY -eq 1 ]]; then
    echo -e "${CYAN}Filter: Showing FAILURES only${NC}"
elif [[ $NO_PASS -eq 1 ]]; then
    echo -e "${CYAN}Filter: Showing FAILURES and WARNINGS${NC}"
else
    echo -e "${CYAN}Filter: Showing ALL tests (use --no-pass to hide passing, --failures-only for failures only)${NC}"
fi
echo

# State
declare -a CAPTURE_BUFFER=()
CAPTURE_TEST_NAME=""
CAPTURE_STATUS=""
IN_CAPTURE=0

PASSED_COUNT=0
WARN_COUNT=0
FAILED_COUNT=0

log_line=0

write_log() {
    # $1: text
    log_line=$((log_line+1))
    echo "$1" >> "$LOGFILE"
    echo $log_line
}

extract_source_location() {
    # $@: lines
    for l in "$@"; do
        if [[ "$l" =~ [[:space:]]in[[:space:]]([^:]+\.cs):line[[:space:]]([0-9]+) ]]; then
            file="${BASH_REMATCH[1]}"
            line="${BASH_REMATCH[2]}"
            filebase=$(basename "$file")
            echo "${filebase}:${line}"
            return
        fi
    done
    echo ""
}

flush_captured_test() {
    if [[ ${#CAPTURE_BUFFER[@]} -gt 0 ]]; then
        should_display=1
        if [[ $FAILURES_ONLY -eq 1 && "$CAPTURE_STATUS" != FAILED* ]]; then
            should_display=0
        fi

        # Extract source location
        source_location=$(extract_source_location "${CAPTURE_BUFFER[@]}")

        # Write header to log
        if [[ -n "$source_location" ]]; then
            ln=$(write_log "$CAPTURE_TEST_NAME - $CAPTURE_STATUS ($source_location)")
        else
            ln=$(write_log "$CAPTURE_TEST_NAME - $CAPTURE_STATUS")
        fi

        # Write all detail lines to log
        for l in "${CAPTURE_BUFFER[@]}"; do
            write_log "    $l" >/dev/null
        done

        # Console output: header + first 25 lines
        if [[ $should_display -eq 1 ]]; then
            color="$RED"
            if [[ -n "$source_location" ]]; then
                echo -e "${color}${CAPTURE_TEST_NAME} - $CAPTURE_STATUS ($source_location) [${LOGFILE}:${ln}:1]${NC}"
            else
                echo -e "${color}${CAPTURE_TEST_NAME} - $CAPTURE_STATUS (${LOGFILE}:${ln}:1)${NC}"
            fi

            # Show first 25 lines
            local count=0
            for l in "${CAPTURE_BUFFER[@]}"; do
                if [[ $count -lt 25 ]]; then
                    echo -e "${GRAY}    $l${NC}"
                fi
                count=$((count+1))
            done
            if [[ $count -gt 25 ]]; then
                echo -e "${GRAY}    ... ($((count-25)) more lines in ${LOGFILE})${NC}"
            fi
        fi
    fi
    CAPTURE_BUFFER=()
    CAPTURE_TEST_NAME=""
    CAPTURE_STATUS=""
    IN_CAPTURE=0
}

# Read dotnet test output line by line
while IFS= read -r line || [[ -n "$line" ]]; do
    if [[ "$line" =~ ^[[:space:]]*Passed[[:space:]]+([^\ ]+) ]]; then
        # Flush any pending capture before handling new test
        if [[ $IN_CAPTURE -eq 1 ]]; then
            flush_captured_test
        fi
        PASSED_COUNT=$((PASSED_COUNT+1))
        testname="${BASH_REMATCH[1]}"
        if [[ $NO_PASS -eq 0 && $FAILURES_ONLY -eq 0 ]]; then
            echo -e "${GREEN}${testname} - passed${NC}"
        fi
        IN_CAPTURE=0
    elif [[ "$line" =~ ^[[:space:]]*Skipped[[:space:]]+([^\ ]+) ]]; then
        if [[ $IN_CAPTURE -eq 1 ]]; then
            flush_captured_test
        fi
        WARN_COUNT=$((WARN_COUNT+1))
        testname="${BASH_REMATCH[1]}"
        ln=$(write_log "$testname - SKIPPED")
        if [[ $FAILURES_ONLY -eq 0 ]]; then
            echo -e "${YELLOW}${testname} - SKIPPED (${LOGFILE}:${ln}:1)${NC}"
        fi
        IN_CAPTURE=0
    elif [[ "$line" =~ ^[[:space:]]*Failed[[:space:]]+([^\ ]+) ]]; then
        if [[ $IN_CAPTURE -eq 1 ]]; then
            flush_captured_test
        fi
        FAILED_COUNT=$((FAILED_COUNT+1))
        IN_CAPTURE=1
        CAPTURE_BUFFER=()
        CAPTURE_TEST_NAME="${BASH_REMATCH[1]}"
        CAPTURE_STATUS="FAILED"
    elif [[ $IN_CAPTURE -eq 1 ]]; then
        # End of a detail block when blank line or summary appears
        if [[ -z "${line// }" ]] || [[ "$line" =~ ^[[:space:]]*Total\ tests: ]]; then
            flush_captured_test
            IN_CAPTURE=0
        else
            CAPTURE_BUFFER+=("$line")
        fi
    fi
    # Ignore other lines
done < <(dotnet test KOTORModSync.Tests/KOTORModSync.Tests.csproj --logger "console;verbosity=normal")

# Flush any trailing capture block if the stream ends without a blank line
if [[ $IN_CAPTURE -eq 1 ]]; then
    flush_captured_test
    IN_CAPTURE=0
fi

# Display summary
echo
echo -e "${CYAN}================================${NC}"
total_tests=$((PASSED_COUNT + WARN_COUNT + FAILED_COUNT))
echo -e "${CYAN}Total Tests: $total_tests${NC}"
if [[ $PASSED_COUNT -gt 0 ]]; then
    echo -e "${GREEN}  Passed: $PASSED_COUNT${NC}"
fi
if [[ $WARN_COUNT -gt 0 ]]; then
    echo -e "${YELLOW}  Skipped: $WARN_COUNT${NC}"
fi
if [[ $FAILED_COUNT -gt 0 ]]; then
    echo -e "${RED}  Failed: $FAILED_COUNT${NC}"
fi
echo -e "${CYAN}================================${NC}"

if [[ $FAILED_COUNT -gt 0 ]]; then
    echo -e "${CYAN}See $LOGFILE for failure details${NC}"
fi