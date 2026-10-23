#!/bin/bash -e
so=$(uname -s| tr 'A-Z' 'a-z')
mc=$(uname -m| tr 'A-Z' 'a-z'| perl -pe 's/86_//')
if [ "${so}" = "darwin" ];then
    so="osx"
fi
#dotnet publish ConsolePlayground/ConsolePlayground.csproj -r "${so}-${mc}" -p:PublishProfile=FolderProfile -c Release
#./Binaries/ConsolePlayground/ConsolePlayground $@
dotnet run -v q --no-launch-profile -c Release --project ConsolePlayground/ConsolePlayground.csproj -- $@
