#!/bin/sh
set -eux

VERSION="0.4.0"

dotnet build --configuration Release

mkdir -p "release"
cd "release/"

FILENAME="TransferWindowPlanner2_v${VERSION}.zip"
if [ -f "$FILENAME" ]; then
  echo "Zip file already exists!"
  exit 1
fi

zip -r "TransferWindowPlanner2_v${VERSION}.zip" \
  "../GameData" \
  "../LICENSE" \
  "../LICENSE.ClickThroughBlocker" \
  "../LICENSE.TransferWindowPlanner" \
  "../README.md"

