#!/usr/bin/env bash
# Lists upstream commits that touched a file in docs/xplat/PORTING-MAP.md since the row's "Based on" commit.
# Each listed commit needs a row update (ported, skipped with a reason, or stale). Exit codes:
#   0  no row is stale
#   1  at least one row is marked stale, so the milestone exit is blocked
#   2  the upstream ref or a "Based on" commit is missing (fetch upstream first)
# Usage: tests/xplat/port-drift.sh   (UPSTREAM_REF defaults to upstream/master)
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
MAP="$REPO/docs/xplat/PORTING-MAP.md"
UPSTREAM_REF="${UPSTREAM_REF:-upstream/master}"

if ! git -C "$REPO" rev-parse --verify --quiet "$UPSTREAM_REF^{commit}" > /dev/null; then
    echo "port-drift: upstream ref '$UPSTREAM_REF' not found; fetch the upstream remote first" >&2
    exit 2
fi

# Strips surrounding spaces and backticks from one table cell.
clean() {
    local value="$1"
    value="${value#"${value%%[![:space:]]*}"}"
    value="${value%"${value##*[![:space:]]}"}"
    value="${value//\`/}"
    printf '%s' "$value"
}

rows=0
drifted=0
stale=0

while IFS='|' read -r _ upstream_cell _new_cell based_cell status_cell _; do
    upstream="$(clean "$upstream_cell")"
    based="$(clean "$based_cell")"
    status="$(clean "$status_cell")"
    rows=$((rows + 1))

    if ! git -C "$REPO" cat-file -e "$based^{commit}" 2> /dev/null; then
        echo "port-drift: 'Based on' commit '$based' for $upstream is not in this clone" >&2
        exit 2
    fi

    commits="$(git -C "$REPO" log --format='  %h %s' "$based..$UPSTREAM_REF" -- "$upstream")"
    if [[ -n "$commits" ]]; then
        drifted=$((drifted + 1))
        echo "$upstream (status: $status, based on $based):"
        echo "$commits"
    fi

    if [[ "$status" == stale* ]]; then
        stale=$((stale + 1))
        echo "STALE: $upstream"
    fi
done < <(grep -E '^\| `' "$MAP")

echo "port-drift: $rows rows, $drifted with upstream changes since their base, $stale stale (upstream $UPSTREAM_REF)"

if [[ "$stale" -gt 0 ]]; then
    exit 1
fi
