#!/bin/bash

set -e

cw $(dirname $0)

VERSION=$(cat InstallMe.csproj | grep Version | egrep -o "[0-9]+\.[0-9]+\.[0-9]+")

dotnet build -c release

zip -j InstallMe_${VERSION}.zip bin/release/net6.0/*

scp InstallMe_${VERSION}.zip digitalsorcery.net:digitalsorcery/InstallMe/download/

ssh digitalsorcery.net "echo $VERSION > digitalsorcery/InstallMe/version.txt"
