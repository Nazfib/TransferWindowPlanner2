#!/bin/sh

VERSION="1.0.0"

msbuild /property:Configuration=Release

rm -rf "release/TransferWindowPlanner2"
mkdir -p "release/TransferWindowPlanner2"
cd "release/"

cp -r "../GameData" "TransferWindowPlanner2/"
cp "../TransferWindowPlanner2/bin/Release/net48/TransferWindowPlanner2.dll" "TransferWindowPlanner2/GameData/TransferWindowPlanner2"

zip -r "TransferWindowPlanner2_v${VERSION}.zip" "TransferWindowPlanner2"

