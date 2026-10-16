#!/usr/bin/env bash
# Run INSIDE the Linux VM: copies the Mac sources (Parallels shared folder) into the VM working
# copy, one way (Mac → VM). Build and debug from the VM copy only: the shared folder holds the
# Mac's obj/ (NuGet paths under /Users/...), which fail in the VM with NETSDK1064.
# See docs/LINUX-UNO-ROADMAP.md §2.2.
#
#   ~/RadioE45/tools/vm-sync.sh            sync
#   ~/RadioE45/tools/vm-sync.sh --dry-run  show what would change
#
# Override the paths with RADIOE45_SRC / RADIOE45_DEST if needed.
set -euo pipefail

SRC="${RADIOE45_SRC:-/media/psf/Lavori/RadioE45}"
DEST="${RADIOE45_DEST:-$HOME/RadioE45}"

if [[ ! -f "$SRC/RadioE45.slnx" ]]; then
    echo "Source not found: $SRC (is the Parallels shared folder mounted?)" >&2
    exit 1
fi

mkdir -p "$DEST"

# --delete makes the VM copy identical to the Mac one: changes made in the VM are lost.
# Excluded paths (bin/obj/IDE settings) are neither copied nor deleted, so the VM keeps its own
# build output and Rider settings.
rsync -a --delete --itemize-changes \
    --exclude 'bin/' --exclude 'obj/' --exclude '.vs/' --exclude '.idea/' --exclude '*.user' \
    "$@" "$SRC/" "$DEST/"

echo "Synced $SRC → $DEST"
