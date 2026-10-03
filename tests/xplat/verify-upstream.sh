#!/usr/bin/env bash
# Verifies the cross-platform shadow projects against the current upstream/master, without touching the working tree.
#
# 1. Fetches upstream/master.
# 2. Checks it out into a throwaway worktree (local branches, including master, are not touched).
# 3. Copies src/xplat and tests/xplat into it, then builds and tests the shadow projects there.
#
# Failing here means an upstream change broke a seam or a stand-in: fix the seam before merging upstream.
# Usage: tests/xplat/verify-upstream.sh
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
WORKTREE="$(mktemp -d)/upstream-verify"

export XPLAT_ARTIFACTS="${XPLAT_ARTIFACTS:-$HOME/xplat-artifacts}-upstream"

cd "$REPO"
git fetch upstream master

git worktree add --detach "$WORKTREE" upstream/master
trap 'git -C "$REPO" worktree remove --force "$WORKTREE"' EXIT

mkdir -p "$WORKTREE/src" "$WORKTREE/tests"
cp -r src/xplat "$WORKTREE/src/"
cp -r tests/xplat "$WORKTREE/tests/"

cd "$WORKTREE"
echo "Verifying shadow projects against upstream $(git rev-parse --short HEAD)"
bash tests/xplat/run-tests.sh
