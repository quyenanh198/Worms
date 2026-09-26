#!/usr/bin/env python3
"""Creates missing Unity .meta files with stable GUIDs (md5 of the path).

Unity would create them on first import anyway, but with random GUIDs that
then show up as untracked files. Run after adding files under client/Assets
or shared/com.worms.*. Types Unity configures specially (e.g. .jslib plugins)
are left for Unity to create.
"""
import hashlib
import os
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
TREES = ["client/Assets", "shared/com.worms.sim", "shared/com.worms.protocol"]

DEFAULT = """DefaultImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
IMPORTERS = {
    ".cs": """MonoImporter:
  externalObjects: {}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {instanceID: 0}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
""",
    ".asmdef": """AssemblyDefinitionImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
""",
    ".json": """TextScriptImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
""",
    ".md": """TextScriptImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
""",
}
PACKAGE_JSON = """PackageManifestImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def guid(rel):
    return hashlib.md5(rel.encode("utf-8")).hexdigest()


def write(path, body):
    rel = os.path.relpath(path, ROOT).replace(os.sep, "/")
    with open(path + ".meta", "w", newline="\n") as f:
        f.write("fileFormatVersion: 2\nguid: %s\n%s" % (guid(rel), body))
    return rel


def main():
    created = []
    for tree in TREES:
        base = os.path.join(ROOT, tree)
        if not os.path.isdir(base):
            continue
        for dirpath, dirnames, filenames in os.walk(base):
            dirnames[:] = [d for d in dirnames if not d.startswith(".") and not d.endswith("~")]
            for d in dirnames:
                p = os.path.join(dirpath, d)
                if not os.path.exists(p + ".meta"):
                    created.append(write(p, "folderAsset: yes\n" + DEFAULT))
            for name in filenames:
                if name.endswith(".meta") or name.startswith("."):
                    continue
                p = os.path.join(dirpath, name)
                if os.path.exists(p + ".meta"):
                    continue
                ext = os.path.splitext(name)[1]
                if name == "package.json" and dirpath == base:
                    created.append(write(p, PACKAGE_JSON))
                elif ext in IMPORTERS:
                    created.append(write(p, IMPORTERS[ext]))
    for c in created:
        print("created", c + ".meta")
    return 0


if __name__ == "__main__":
    sys.exit(main())
