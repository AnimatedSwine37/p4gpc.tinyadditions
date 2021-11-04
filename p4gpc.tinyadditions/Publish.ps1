# Project Output Paths
$modOutputRootPath = "TempBuild"
$modOutputPath = "$modOutputRootPath/p4gpc.tinyadditions"
$solutionName = "p4gpc.tinyadditions.csproj"
$publishName = "p4gpc.tinyadditions.zip"
$publishDirectory = "Publish"

[Environment]::CurrentDirectory = $PWD

# Clean anything in existing Release directory.
if (Test-Path -Path $modOutputPath) {
    Remove-Item $modOutputPath -Recurse
}
if (Test-Path -Path $publishDirectory) {
    Remove-Item $publishDirectory -Recurse
}
New-Item $modOutputPath -ItemType Directory   
New-Item $publishDirectory -ItemType Directory

# Build
dotnet restore $solutionName
dotnet clean $solutionName
dotnet publish $solutionName -c Release -r win-x86 --self-contained false -o "$modOutputPath/x86" /p:PublishReadyToRun=true
dotnet publish $solutionName -c Release -r win-x64 --self-contained false -o "$modOutputPath/x64" /p:PublishReadyToRun=true

# Remove Redundant Files
if (Test-Path -Path "$modOutputPath/x86/ModConfig.json") {
    Move-Item -Path "$modOutputPath/x86/ModConfig.json" -Destination "$modOutputPath/ModConfig.json"
}
if (Test-Path -Path "$modOutputPath/x86/Preview.png") {
    Move-Item -Path "$modOutputPath/x86/Preview.png" -Destination "$modOutputPath/Preview.png"
}
if (Test-Path -Path "$modOutputPath/x64/Preview.png") {
    Remove-Item "$modOutputPath/x64/Preview.png"
}
if (Test-Path -Path "$modOutputPath/x64/ModConfig.json") {
    Remove-Item "$modOutputPath/x64/ModConfig.json"
}

# Cleanup Unnecessary Files
Get-ChildItem $modOutputPath -Include *.exe -Recurse | Remove-Item -Force -Recurse
Get-ChildItem $modOutputPath -Include *.pdb -Recurse | Remove-Item -Force -Recurse
Get-ChildItem $modOutputPath -Include *.xml -Recurse | Remove-Item -Force -Recurse

# Compress
Add-Type -A System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($modOutputRootPath, "$publishDirectory/$publishName")

# Cleanup After Build
Remove-Item $modOutputPath -Recurse