# PowerShell equivalent of publish.sh for Windows
param(
    [string]$Version = "0.1.0",
    [string]$AccountKey = "abc",
    [string]$RID = "win"
)

$ProjectPath = "./app/Tim.csproj"
$OutDir = "./out/exe"

if (Test-Path $OutDir) {
    Remove-Item $OutDir -Recurse -Force
}
New-Item -ItemType Directory -Path $OutDir -Force | Out-Null

$TargetDir = Join-Path $OutDir $RID
New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null

Write-Host "Publishing $ProjectPath for $RID ..."
dotnet publish $ProjectPath `
    -c Release `
    -r $RID `
    -o $TargetDir `
    -f "net10.0" `
    -p:Version=$Version

Write-Host "Executables available in $TargetDir"

switch ($RID) {
    "win" { $Exe = "tim.exe" }
    default { $Exe = "tim" }
}

$TarFile = Join-Path $OutDir "tim-$RID.tar.gz"

& tar -czf $TarFile -C $TargetDir $Exe

Write-Host "Uploading tim-$RID.tar.gz to Azure..."

az storage blob upload `
  --account-name homebrewfiles `
  --container-name tim `
  --name "$Version/tim-$RID.tar.gz" `
  --file $TarFile `
  --account-key $AccountKey `
  --overwrite

Write-Host "Uploading tim-$RID.tar.gz to github release"
& gh release upload --clobber $Version "$TarFile#tim-$RID"
