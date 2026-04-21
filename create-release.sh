#!/bin/sh
set -eux

VERSION="0.4.0"

dotnet build --configuration Release

rm -rf "release/TransferWindowPlanner2"
mkdir -p "release/TransferWindowPlanner2"
cd "release/"

FILENAME="TransferWindowPlanner2_v${VERSION}.zip"
if [ -f "$FILENAME" ]; then
  echo "Zip file already exists!"
  exit 1
fi

cp -r "../GameData" \
  "../LICENSE" \
  "../LICENSE.ClickThroughBlocker" \
  "../LICENSE.TransferWindowPlanner" \
  "../README.md" \
  "TransferWindowPlanner2/"

zip -r "TransferWindowPlanner2_v${VERSION}.zip" "TransferWindowPlanner2"

