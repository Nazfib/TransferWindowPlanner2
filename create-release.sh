#!/bin/sh
set -eux

VERSION="0.3.0-preview"

dotnet build --configuration Release

rm -rf "release/TransferWindowPlanner2"
mkdir -p "release/TransferWindowPlanner2"
cd "release/"

cp -r "../GameData" "TransferWindowPlanner2/"
cp "../TransferWindowPlanner2/bin/Release/net48/TransferWindowPlanner2.dll" "TransferWindowPlanner2/GameData/TransferWindowPlanner2"
cp "../LICENSE" "../LICENSE.TransferWindowPlanner" "../LICENSE.ClickThroughBlocker" "TransferWindowPlanner2/"
cp "../README.md" "TransferWindowPlanner2/README.md"

zip -FS -r "TransferWindowPlanner2_v${VERSION}.zip" "TransferWindowPlanner2"
