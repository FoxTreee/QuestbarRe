$ErrorActionPreference = "Stop"

if (-not (Test-Path "project.godot")) {
    throw "Run this script from the Questbar repository root (the folder containing project.godot)."
}

$deadFiles = @(
    "Scripts/Presentation/WorldScaleController.cs",
    "Scripts/Presentation/WorldScaleController.cs.uid",
    "Controllers/Debug/HeroFactorySpawnProbe.cs",
    "Controllers/Debug/HeroFactorySpawnProbe.cs.uid",
    "Controllers/WindowsTaskbarButtonGeometryReader.cs",
    "Controllers/WindowsTaskbarButtonGeometryReader.cs.uid"
)

foreach ($file in $deadFiles) {
    if (Test-Path $file) {
        Remove-Item $file -Force
        Write-Host "Removed $file"
    }
}

if (Get-Command git -ErrorAction SilentlyContinue) {
    git rm -r --cached --ignore-unmatch .vs
    Write-Host "Stopped tracking .vs in Git. Local Visual Studio files remain on disk."
} else {
    Write-Warning "Git was not found in PATH. .vs is ignored now, but run: git rm -r --cached .vs"
}

Write-Host ""
Write-Host "Automated non-scene cleanup complete."
Write-Host "Now perform the manual Godot scene cleanup from the ChatGPT instructions before testing."
