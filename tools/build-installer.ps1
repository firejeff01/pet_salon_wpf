<#
.SYNOPSIS
   貳寶寵物美容工坊 — Build self-contained MSI installer

.DESCRIPTION
   1) dotnet publish self-contained 到 out/publish
   2) 掃描 publish 目錄、產出 Files.wxs（自動產 Component / ComponentGroup）
   3) WiX v5 build → out/PetSalon-Setup.msi

.NOTES
   需要 dotnet 10 SDK + WiX v5（已 dotnet tool install --global wix）
#>

$ErrorActionPreference = 'Stop'
$repo = Resolve-Path "$PSScriptRoot/.."
$publishDir = Join-Path $repo 'out\publish'
$installerDir = Join-Path $repo 'installer'
$outDir = Join-Path $repo 'out'
$filesWxs = Join-Path $installerDir 'Files.wxs'

# 從 csproj <Version> 讀取版本作為 single source of truth；轉成 4-part 給 MSI
$csproj = Join-Path $repo 'src\PetSalon.Wpf\PetSalon.Wpf.csproj'
[xml]$xml = Get-Content $csproj
$semVer = $xml.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $semVer) { throw "<Version> not found in $csproj" }
# 三段轉四段（MSI 需 M.m.p.b）
$msiVer = if ($semVer -match '^\d+\.\d+\.\d+$') { "$semVer.0" } else { $semVer }
Write-Host "Version: $semVer (MSI: $msiVer)" -ForegroundColor Green

Write-Host '== Step 1: dotnet publish (self-contained win-x64) ==' -ForegroundColor Cyan
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
dotnet publish (Join-Path $repo 'src\PetSalon.Wpf\PetSalon.Wpf.csproj') `
    -c Release -r win-x64 --self-contained true `
    -o $publishDir --nologo
if ($LASTEXITCODE -ne 0) { throw 'publish failed' }

# 清掉 runtime 不需要的檔案（EF design-time host、docs、pdb、未用字型）
foreach ($drop in @('BuildHost-net472', 'BuildHost-netcore', 'LatoFont')) {
    $p = Join-Path $publishDir $drop
    if (Test-Path $p) { Remove-Item $p -Recurse -Force }
}
Get-ChildItem $publishDir -Recurse -File -Include '*.xml','*.pdb' | Remove-Item -Force

Write-Host '== Step 2: generate Files.wxs from publish output ==' -ForegroundColor Cyan
# 掃 publish 目錄，每個檔產一個 Component；ComponentGroupRef=PublishedFiles
$files = Get-ChildItem $publishDir -Recurse -File
$components = New-Object System.Collections.ArrayList
$subdirs = @{}  # subdirRelativePath -> directoryId

# 為次資料夾產 Directory 元素
$subdirSet = @{}
foreach ($f in $files) {
    $rel = $f.FullName.Substring($publishDir.Length).TrimStart('\')
    $relDir = Split-Path $rel -Parent
    if ($relDir -and -not $subdirSet.ContainsKey($relDir)) {
        $subdirSet[$relDir] = $true
    }
}

$dirXml = New-Object System.Text.StringBuilder
[void]$dirXml.AppendLine('    <DirectoryRef Id="INSTALLFOLDER">')
$dirIdMap = @{ '' = 'INSTALLFOLDER' }
$sortedDirs = $subdirSet.Keys | Sort-Object { $_.Split('\').Count }
# 為每個 subdir 配發 Directory Id
foreach ($d in $sortedDirs) {
    $parts = $d.Split('\')
    $cumulative = ''
    foreach ($p in $parts) {
        $cumulative = if ($cumulative) { "$cumulative\$p" } else { $p }
        if (-not $dirIdMap.ContainsKey($cumulative)) {
            $id = 'd_' + ($cumulative -replace '[^a-zA-Z0-9]', '_')
            if ($id.Length -gt 70) { $id = $id.Substring(0, 70) }
            $dirIdMap[$cumulative] = $id
        }
    }
}
# 產生巢狀 Directory 結構
function Emit-Tree {
    param([string]$parentPath, [int]$indent)
    $children = $dirIdMap.Keys | Where-Object {
        $_ -ne '' -and (Split-Path $_ -Parent) -eq $parentPath
    } | Sort-Object
    foreach ($c in $children) {
        $name = Split-Path $c -Leaf
        $id = $dirIdMap[$c]
        $pad = ' ' * $indent
        [void]$script:dirXml.AppendLine("$pad<Directory Id=`"$id`" Name=`"$name`">")
        Emit-Tree -parentPath $c -indent ($indent + 2)
        [void]$script:dirXml.AppendLine("$pad</Directory>")
    }
}
Emit-Tree -parentPath '' -indent 6
[void]$dirXml.AppendLine('    </DirectoryRef>')

# 產 Component for each file
$compXml = New-Object System.Text.StringBuilder
[void]$compXml.AppendLine('    <ComponentGroup Id="PublishedFiles">')
$idx = 0
foreach ($f in $files) {
    $idx++
    $rel = $f.FullName.Substring($publishDir.Length).TrimStart('\')
    $relDir = Split-Path $rel -Parent
    $dirId = $dirIdMap[$relDir]
    $guid = (New-Guid).Guid.ToUpper()
    $compId = "c_$idx"
    $fileId = "f_$idx"
    $sourcePath = "..\out\publish\$rel"
    [void]$compXml.AppendLine("      <Component Id=`"$compId`" Directory=`"$dirId`" Guid=`"$guid`">")
    [void]$compXml.AppendLine("        <File Id=`"$fileId`" Source=`"$sourcePath`" KeyPath=`"yes`" />")
    [void]$compXml.AppendLine("      </Component>")
}
[void]$compXml.AppendLine('    </ComponentGroup>')

$wxs = @"
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Fragment>
$($dirXml.ToString())$($compXml.ToString())  </Fragment>
</Wix>
"@
Set-Content -Path $filesWxs -Value $wxs -Encoding UTF8
Write-Host "  generated $filesWxs  ($idx files)"

Write-Host '== Step 2.5: PreventDataLoss sanity check ==' -ForegroundColor Cyan
# 確保 Files.wxs 沒有引用到使用者資料目錄（%LOCALAPPDATA%\PetSalon\ 下任何路徑）
# 這是 Product.wxs 中 PreventDataLoss 社約的 build-time guard
$wxsContent = Get-Content $filesWxs -Raw
$forbidden = @(
    '\\PetSalon\\petsalon_app\.db',
    '\\PetSalon\\backups\\',
    '\\PetSalon\\contracts\\',
    '\\PetSalon\\chromium\\',
    'LocalAppData(?:Folder)?\\PetSalon(?![\\\.])'
)
foreach ($pat in $forbidden) {
    if ($wxsContent -match $pat) {
        throw "PreventDataLoss violation: Files.wxs contains forbidden path '$pat'. 使用者資料目錄不得納入 MSI。"
    }
}
Write-Host '  ok — Files.wxs 未引用使用者資料目錄'

Write-Host '== Step 3: wix build → MSI ==' -ForegroundColor Cyan
$msiOut = Join-Path $outDir "PetSalon-Setup-v$semVer.msi"
if (Test-Path $msiOut) { Remove-Item $msiOut -Force }
# 清掉舊版檔名（早期未帶版本號）
$legacy = Join-Path $outDir 'PetSalon-Setup.msi'
if (Test-Path $legacy) { Remove-Item $legacy -Force }
Push-Location $installerDir
try {
    & wix build 'Product.wxs' 'Files.wxs' -d "Version=$msiVer" -o $msiOut
    if ($LASTEXITCODE -ne 0) { throw 'wix build failed' }
} finally {
    Pop-Location
}

$size = [math]::Round((Get-Item $msiOut).Length / 1MB, 1)
Write-Host ""
Write-Host "DONE → $msiOut ($size MB)" -ForegroundColor Green
