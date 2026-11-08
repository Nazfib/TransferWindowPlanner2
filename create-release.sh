#!/bin/sh
set -eux

VERSION="1.0.0-devjeb"

dotnet build --configuration Release

FILENAME="TransferWindowPlanner2_v${VERSION}.zip"
if [ -f "release/$FILENAME" ]; then
  echo "Zip file already exists!"
  exit 1
fi

zip -r "$FILENAME" \
  "GameData" \
  "TransferWindowPlanner2.ckan" \
  "LICENSE" \
  "README.md"
mv "$FILENAME" release

