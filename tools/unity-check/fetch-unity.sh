#!/bin/bash
# Extracts the managed DLLs and built-in packages of the Unity editor used by
# the project from GameCI's Docker image (no Docker daemon or license needed).
# Usage: tools/unity-check/fetch-unity.sh <dest>   then export UNITY_DATA=<dest>/opt/unity/Editor/Data
set -euo pipefail
dest=${1:?destination directory}
version=$(sed -n 's/^m_EditorVersion: //p' "$(dirname "$0")/../../client/ProjectSettings/ProjectVersion.txt")
tag="ubuntu-${version}-base-3"
token=$(curl -sS "https://auth.docker.io/token?service=registry.docker.io&scope=repository:unityci/editor:pull" | python3 -c 'import json,sys;print(json.load(sys.stdin)["token"])')
accept='application/vnd.oci.image.index.v1+json,application/vnd.oci.image.manifest.v1+json,application/vnd.docker.distribution.manifest.v2+json,application/vnd.docker.distribution.manifest.list.v2+json'
reg=https://registry-1.docker.io/v2/unityci/editor
index=$(curl -sS -H "Authorization: Bearer $token" -H "Accept: $accept" "$reg/manifests/$tag")
digest=$(echo "$index" | python3 -c 'import json,sys;d=json.load(sys.stdin);print(next(m["digest"] for m in d["manifests"] if m["platform"]["architecture"]=="amd64"))')
# The editor is the largest layer.
layer=$(curl -sS -H "Authorization: Bearer $token" -H "Accept: $accept" "$reg/manifests/$digest" | python3 -c 'import json,sys;d=json.load(sys.stdin);print(max(d["layers"],key=lambda l:l["size"])["digest"])')
mkdir -p "$dest"
curl -sSL -H "Authorization: Bearer $token" "$reg/blobs/$layer" | tar -xz -C "$dest" --wildcards \
  '*Editor/Data/Managed/*' '*Editor/Data/NetStandard/ref/*' '*Editor/Data/Resources/PackageManager/BuiltInPackages/*'
echo "UNITY_DATA=$dest/opt/unity/Editor/Data"
