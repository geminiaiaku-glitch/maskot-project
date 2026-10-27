"""Package an already-published Windows build; standard library only."""
import hashlib
import os
from pathlib import Path
import shutil
import zipfile

root = Path(__file__).resolve().parent.parent
output = root / "artifacts/win-x64"
exe = output / "Maskot.exe"
if not exe.exists() or exe.read_bytes()[:2] != b"MZ":
    raise SystemExit("Publish the Windows executable before packaging")

cache = Path(os.environ.get("NUGET_PACKAGES", "/workspace/.tools/nuget"))
notices = output / "notices"
notices.mkdir(exist_ok=True)
for package in ["microsoft.netcore.app.runtime.win-x64", "microsoft.windowsdesktop.app.runtime.win-x64"]:
    versions = sorted((cache / package).glob("*"), key=lambda p: tuple(map(int, p.name.split("."))))
    if not versions:
        raise SystemExit(f"Runtime license package missing: {package}")
    copied = 0
    for item in versions[-1].iterdir():
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
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    path.with_suffix(path.suffix + ".sha256").write_text(digest + "  " + path.name + "\n", encoding="ascii")
    print(path.name, path.stat().st_size, "bytes", "SHA256", digest)
