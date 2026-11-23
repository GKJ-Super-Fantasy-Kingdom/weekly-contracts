"""Build and verify the local-import archive. Requires the .NET SDK and Pillow."""
import io
import json
import subprocess
from pathlib import Path
from tempfile import TemporaryDirectory
from xml.etree import ElementTree
from zipfile import ZIP_DEFLATED, ZipFile

from PIL import Image

root = Path(__file__).resolve().parent
listing = root / "thunderstore"
project = root / "WeeklyContracts.csproj"
manifest = json.loads((listing / "manifest.json").read_text("utf-8"))
version = manifest["version_number"]
xml = ElementTree.parse(project)
assert manifest["name"] == "WeeklyContracts" and version == xml.findtext(".//Version")
subprocess.run(["dotnet", "build", str(project), "-c", "Release"], cwd=root, check=True)
dll = root / "bin/Release" / xml.findtext(".//TargetFramework") / "WeeklyContracts.dll"
files = {
    "plugins/WeeklyContracts.dll": dll,
    "manifest.json": listing / "manifest.json",
    "README.md": root / "README.md",
    "icon.png": listing / "icon.png",
}
dist = root / "dist"
dist.mkdir(exist_ok=True)
output = dist / f"WeeklyContracts-{version}.zip"
with TemporaryDirectory(dir=dist) as temp:
    staged = Path(temp) / output.name
    with ZipFile(staged, "w", ZIP_DEFLATED) as archive:
        for entry, path in files.items():
            archive.write(path, entry)
    with ZipFile(staged) as archive:
        assert archive.testzip() is None and sorted(archive.namelist()) == sorted(files)
        for entry, path in files.items():
            assert archive.read(entry) == path.read_bytes(), entry
        assert json.loads(archive.read("manifest.json")) == manifest
        with Image.open(io.BytesIO(archive.read("icon.png"))) as icon:
            assert icon.format == "PNG" and icon.size == (256, 256)
            icon.load()
    staged.replace(output)
print(output)
