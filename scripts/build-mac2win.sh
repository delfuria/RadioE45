#!/usr/bin/env bash
# Trigger Windows publish builds on the Parallels VM via SSH (prlctl exec is unreliable: TCHOST "Can't send to empty handle").
SSH="ssh -i $HOME/.ssh/radioe45_winvm -o StrictHostKeyChecking=accept-new delfo@10.211.55.3"
$SSH "cd /d \\mac\home\Lavori\RadioE45\scripts && build-windows.bat"

#$SSH "cd /d \\mac\home\Lavori\RadioE45 && dotnet publish -f net10.0-windows10.0.19041.0 -c Release -r win-x64 --self-contained RadioE45/RadioE45.csproj"
#$SSH "cd /d \\mac\home\Lavori\RadioE45 && dotnet publish -f net10.0-windows10.0.19041.0 -c Release -r win-arm64 --self-contained RadioE45/RadioE45.csproj"
