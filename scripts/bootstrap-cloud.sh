#!/usr/bin/env bash
set -euo pipefail
# Install the pinned official SDK outside the checkout. Verify SHA512 before extraction.
setup_root=/workspace/.tools
sdk_version=10.0.100
mkdir -p "$setup_root/dotnet" "$setup_root/downloads"
if [ ! -x "$setup_root/dotnet/dotnet" ] || ! "$setup_root/dotnet/dotnet" --list-sdks | rg -q '^10\.0\.100 '; then
  curl --fail --silent --show-error --location \
    "https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json" \
    -o "$setup_root/downloads/releases-10.0.json"
  python3 - "$setup_root" "$sdk_version" <<'PY'
import hashlib, json, pathlib, subprocess, sys, tarfile
root=pathlib.Path(sys.argv[1]); version=sys.argv[2]
metadata=json.loads((root/'downloads/releases-10.0.json').read_text())
files=[f for r in metadata['releases'] for s in r.get('sdks',[r.get('sdk',{})])
       if s.get('version')==version for f in s['files']
       if f['rid']=='linux-x64' and f['name'].endswith('.tar.gz')]
if len(files)!=1: raise SystemExit('Official SDK metadata is missing or ambiguous')
item=files[0]; archive=root/'downloads'/('dotnet-sdk-'+version+'.tar.gz')
subprocess.run(['curl','--fail','--silent','--show-error','--location',item['url'],'-o',str(archive)],check=True)
digest=hashlib.sha512()
with archive.open('rb') as stream:
    for chunk in iter(lambda:stream.read(1024*1024),b''): digest.update(chunk)
if digest.hexdigest().lower()!=item['hash'].lower():
    archive.unlink(); raise SystemExit('SDK SHA512 mismatch; archive discarded')
with tarfile.open(archive) as package: package.extractall(root/'dotnet',filter='tar')
print('Official .NET SDK verified and installed:',version)
PY
fi
project_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
bash "$project_dir/scripts/dev.sh" restore
