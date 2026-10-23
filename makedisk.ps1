$ErrorActionPreference = 'Stop'
$tag=(git describe --tags --abbrev=0)
Write-Host "Tag=${tag} "
$proj = Join-Path ${PSScriptRoot} "ConsolePlayground" "ConsolePlayground.csproj"

$workDir = Join-Path ${PSScriptRoot} "_DistRoot"
if (!(Test-Path ${workDir})) {
    New-Item -Path ${workDir} -ItemType Directory
}
$baseName = "ConsolePlayground-${tag}-x64"

$buildDir = Join-Path ${workDir} ${baseName}
if (Test-Path ${buildDir}) {
    Remove-Item -Recurse -Force ${buildDir}
}
New-Item -Path ${buildDir} -ItemType Directory

$binDir = Join-Path ${buildDir} "Binaries" "ConsolePlayground"

dotnet publish $proj -r win-x64 -p:PublishProfile=FolderProfile -c Release -o ${binDir}
if ($LASTEXITCODE -ne 0){
    throw "dotnet publishが失敗 ${LASTEXITCODE}";
}

Remove-Item "${binDir}\*.pdb"
New-Item -ItemType Directory (Join-Path ${buildDir} "ffmpeg")
Copy-Item '.\ffmpeg\.gitignore' (Join-Path ${buildDir} "ffmpeg")
Copy-Item '.\run.ps1' ${buildDir}

Compress-Archive -Path ${buildDir} -DestinationPath (Join-Path ${workDir} "${baseName}.zip") -CompressionLevel Optimal
