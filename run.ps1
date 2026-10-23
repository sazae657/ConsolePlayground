$ErrorActionPreference = 'stop'
$tdll="${PSScriptRoot}\ffmpeg\bin\x64\avcodec-62.dll"
$dsp=$null
if (Test-Path $tdll) {
    $dsp=(Get-Item $tdll)
}
if ($null -eq $dsp) {
    Push-Location ${PSScriptRoot}\ffmpeg
    Write-Host -ForegroundColor Yellow "ffmpegをダウンロード"
    # FFmpeg.AutoGenからスクリプトをお借りする
    $dlscript = '.\download-ffmpeg.ps1'
    if (!(Test-Path -Path $dlscript)) {
        $local:url = 'https://raw.githubusercontent.com/Ruslan-B/FFmpeg.AutoGen/refs/heads/main/FFmpeg/download-ffmpeg.ps1'
        try {
            Invoke-WebRequest -Uri ${url} -OutFile $dlscript
        }catch {
            Write-Error "${$dlscript} のダウンロードに失敗  $url`n$_"
            Remove-Item -Force ${$dlscript} -ErrorAction SilentlyContinue
            exit 1
        }
    }
    & ${dlscript} -Version 8.1.2 -Force
    Pop-Location
}
$projDir = "${PSScriptRoot}\ConsolePlayground"
$proj = "${projDir}\ConsolePlayground.csproj"
$exePath = "${PSScriptRoot}\Binaries\ConsolePlayground\ConsolePlayground.exe"

if (Test-Path ${proj}){
    #$dllPath =(dotnet msbuild -getProperty:TargetPath $proj -p:Platform=x64 -p:Configuration=Release)
    #$projExePath = [System.IO.Path]::ChangeExtension($dllPath, ".exe")

    $latestSource = Get-ChildItem -Path $projDir -Recurse -File |
        Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' -and $_.Extension -eq '.cs' } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    #$needsBuild = !(Test-Path $projExePath) -or !(Test-Path $exePath) -or ($latestSource -and ((Get-Item $exePath).LastWriteTime -lt $latestSource.LastWriteTime))
    $needsBuild = !(Test-Path $exePath) -or ($latestSource -and ((Get-Item $exePath).LastWriteTime -lt $latestSource.LastWriteTime))
    if ($needsBuild) {
        #dotnet build $proj -c Release -p:Platform=x64
        #if ($LASTEXITCODE -ne 0){
        #    throw "dotnet buildが失敗 ${LASTEXITCODE}";
        #}
        dotnet publish $proj -r win-x64 -p:PublishProfile=FolderProfile -c Release
        if ($LASTEXITCODE -ne 0){
            throw "dotnet publishが失敗 ${LASTEXITCODE}";
        }
    }
}
if (!(Test-Path ${exePath})) {
    throw "実行可能ファイルがない ${exePath}";
}

& $exePath $args
