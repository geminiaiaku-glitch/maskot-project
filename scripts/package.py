"""Package an already-published Windows build; standard library only."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import zipfile

root = Path(__file__).resolve().parent.parent
output = root / "artifacts/win-x64"
exe = output / "Maskot.exe"
if not exe.exists():
    raise SystemExit("Publish the Windows executable before packaging")
with exe.open("rb") as stream:
    if stream.read(2) != b"MZ":
        raise SystemExit("Publish the Windows executable before packaging")

runtime_config = root / "src/Maskot.Windows/bin/Release/net10.0-windows10.0.19041.0/win-x64/Maskot.runtimeconfig.json"
runtimes = json.loads(runtime_config.read_text())["runtimeOptions"]["includedFrameworks"]
runtime_versions = {item["name"].lower() + ".runtime.win-x64": item["version"] for item in runtimes}
(output / "build-info.json").write_text(json.dumps({
    "commit": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=root, text=True).strip(),
    "sdk": json.loads((root / "global.json").read_text())["sdk"]["version"],
    "runtimes": runtimes,
}, indent=2) + "\n", encoding="utf-8")

cache = Path(os.environ.get("NUGET_PACKAGES", "/workspace/.tools/nuget"))
notices = output / "notices"
notices.mkdir(exist_ok=True)
for package in ["microsoft.netcore.app.runtime.win-x64", "microsoft.windowsdesktop.app.runtime.win-x64"]:
    version_dir = cache / package / runtime_versions[package]
    if not version_dir.is_dir():
        raise SystemExit(f"Runtime license package missing: {package}")
    copied = 0
    for item in version_dir.iterdir():
        if item.is_file() and ("license" in item.name.lower() or "notice" in item.name.lower()):
            shutil.copy2(item, notices / (package + "-" + item.name))
            copied += 1
    if not copied:
        raise SystemExit(f"Runtime license missing: {package}")
for package in ["microsoft.windows.sdk.net.ref"]:
    for manifest in (cache / package).glob("*/*.nuspec"):
        shutil.copy2(manifest, notices / manifest.name)
for name in ["README.md", "NOTICE.md"]:
    shutil.copy2(root / name, output / name)
for name in ["TESTING.md", "WINDOWS-ACCEPTANCE.md"]:
    shutil.copy2(root / "docs" / name, output / name)
(output / "docs").mkdir(exist_ok=True)
for document in (root / "docs").glob("*.md"):
    shutil.copy2(document, output / "docs" / document.name)
shutil.copy2(root / "docs/MULAI-DI-SINI.txt", output / "MULAI-DI-SINI.txt")
archive = root / "artifacts/Maskot-win-x64.zip"
with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as zipout:
    for path in sorted(output.rglob("*")):
        if path.is_file() and path.suffix != ".pdb":
            zipout.write(path, "Maskot-win-x64/" + str(path.relative_to(output)))
with zipfile.ZipFile(archive) as zipin:
    if zipin.testzip() is not None:
        raise SystemExit("ZIP integrity check failed")
for path in [exe, archive]:
    with path.open("rb") as stream:
        digest = hashlib.file_digest(stream, "sha256").hexdigest()
    path.with_suffix(path.suffix + ".sha256").write_text(digest + "  " + path.name + "\n", encoding="ascii")
    print(path.name, path.stat().st_size, "bytes", "SHA256", digest)
