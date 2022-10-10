#!/bin/bash

cd $(dirname $0)
VERSION=$(cat JUSTInstaller.csproj | grep \<Version\> | egrep -o "[0-9]+\.[0-9]+\.[0-9]+")
dotnet pack -c release -o nupkg
git tag v$VERSION
