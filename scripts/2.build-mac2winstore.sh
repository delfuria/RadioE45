#!/usr/bin/env bash
# Trigger Windows publish builds on the Parallels VM via SSH (prlctl exec is unreliable: TCHOST "Can't send to empty handle").
SSH="ssh -i $HOME/.ssh/radioe45_winvm -o StrictHostKeyChecking=accept-new delfo@10.211.55.3"
$SSH "cd /d \\mac\home\Lavori\RadioE45\scripts && build-store-windows.bat"
