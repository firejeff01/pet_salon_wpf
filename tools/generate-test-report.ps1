#requires -version 7
<#
.SYNOPSIS
    Run all PetSalon tests, generate TRX, render as styled HTML report.
.EXAMPLE
    pwsh tools/generate-test-report.ps1
#>
param(
    [string]$OutputPath = (Join-Path $PSScriptRoot '..' 'test-report.html'),
    [switch]$SkipUiTests
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $root

$resultsDir = Join-Path $root 'test-results'
if (Test-Path $resultsDir) { Remove-Item $resultsDir -Recurse -Force }
New-Item -ItemType Directory -Path $resultsDir | Out-Null

$projects = @(
    @{ Name = 'Core 單元+整合測試';     Path = 'tests\PetSalon.Core.Tests\PetSalon.Core.Tests.csproj' }
    @{ Name = 'WPF ViewModel 測試';     Path = 'tests\PetSalon.Wpf.Tests\PetSalon.Wpf.Tests.csproj'  }
)
if (-not $SkipUiTests) {
    $projects += @{ Name = 'WPF UI 自動化測試'; Path = 'tests\PetSalon.Wpf.UiTests\PetSalon.Wpf.UiTests.csproj' }
}

Write-Host "Running tests (TRX logger)..." -ForegroundColor Cyan
foreach ($p in $projects) {
    $logFile = Join-Path $resultsDir ((Split-Path $p.Path -Leaf) -replace '\.csproj$', '.trx')
    Write-Host "  $($p.Name) ..." -NoNewline
    & dotnet test $p.Path --logger "trx;LogFileName=$logFile" --results-directory $resultsDir | Out-Null
    if (Test-Path $logFile) { Write-Host " OK" -ForegroundColor Green }
    else { Write-Host " (no trx)" -ForegroundColor Yellow }
}

Write-Host "Parsing TRX..." -ForegroundColor Cyan
$ns = @{ ns = 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010' }
$allRuns = @()
foreach ($p in $projects) {
    $trxFile = Get-ChildItem $resultsDir -Filter "$(([IO.Path]::GetFileNameWithoutExtension($p.Path))).trx" -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $trxFile) { continue }
    [xml]$doc = Get-Content $trxFile.FullName -Raw
    $cases = @()
    $resultsByExec = @{}
    foreach ($r in $doc.TestRun.Results.UnitTestResult) {
        $resultsByExec[$r.executionId] = $r
    }
    foreach ($td in $doc.TestRun.TestDefinitions.UnitTest) {
        $execId = $td.Execution.id
        if (-not $resultsByExec.ContainsKey($execId)) { continue }
        $r = $resultsByExec[$execId]
        $errorMsg = $null
        if ($r.Output -and $r.Output.ErrorInfo -and $r.Output.ErrorInfo.Message) {
            $errorMsg = $r.Output.ErrorInfo.Message
        }
        $cases += [pscustomobject]@{
            ClassName  = $td.TestMethod.className
            TestName   = $td.name
            Outcome    = $r.outcome
            Duration   = $r.duration
            ErrorMsg   = $errorMsg
        }
    }
    $allRuns += [pscustomobject]@{
        ProjectName = $p.Name
        TrxFile     = $trxFile.Name
        Cases       = $cases
        Total       = $cases.Count
        Passed      = ($cases | Where-Object { $_.Outcome -eq 'Passed' }).Count
        Failed      = ($cases | Where-Object { $_.Outcome -eq 'Failed' }).Count
        Skipped     = ($cases | Where-Object { $_.Outcome -ne 'Passed' -and $_.Outcome -ne 'Failed' }).Count
    }
}

$grandTotal   = ($allRuns | Measure-Object -Property Total   -Sum).Sum
$grandPassed  = ($allRuns | Measure-Object -Property Passed  -Sum).Sum
$grandFailed  = ($allRuns | Measure-Object -Property Failed  -Sum).Sum
$grandSkipped = ($allRuns | Measure-Object -Property Skipped -Sum).Sum
$timestamp    = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')

function HtmlEncode([string]$s) {
    if ($null -eq $s) { return '' }
    return [System.Web.HttpUtility]::HtmlEncode($s)
}
Add-Type -AssemblyName System.Web

$css = @'
:root {
  --pink-50:#FFF5F8;--pink-100:#FFE8F0;--pink-200:#FFD1DF;--pink-500:#F87199;--pink-600:#E85787;--pink-700:#C83F6D;
  --brown-700:#5D4037;--brown-500:#8D6E63;--text:#4A2E35;--muted:#8A6570;
  --success:#6FBF8E;--success-bg:#E6F6EC;--danger:#E05761;--danger-bg:#FEE8EA;--warning:#E8A64E;--warning-bg:#FFF3E0;
  --border:#F8D7E0;--surface:#FFFFFF;--bg:#FFF5F8;
}
* { box-sizing:border-box; }
body { font-family:"Noto Sans TC","Microsoft JhengHei",sans-serif; background:var(--bg); color:var(--text); margin:0; padding:24px; }
.container { max-width:1280px; margin:0 auto; }
.hero { background:linear-gradient(135deg,#FFE8F0,#FFD1DF 50%,#FFB6CC); border-radius:24px; padding:32px; margin-bottom:24px; box-shadow:0 12px 32px rgba(232,87,135,.15); }
.hero h1 { margin:0 0 6px; font-size:28px; color:var(--brown-700); }
.hero .subtitle { color:var(--brown-500); font-size:14px; }
.summary { display:grid; grid-template-columns:repeat(4,1fr); gap:16px; margin-bottom:24px; }
.card { background:var(--surface); border:1px solid var(--border); border-radius:18px; padding:20px; box-shadow:0 4px 12px rgba(232,87,135,.10); }
.metric { font-size:36px; font-weight:bold; }
.metric.pass { color:var(--success); }
.metric.fail { color:var(--danger); }
.metric.skip { color:var(--warning); }
.metric.total { color:var(--pink-700); }
.label { color:var(--muted); font-size:13px; }
section.project { background:var(--surface); border:1px solid var(--border); border-radius:18px; padding:0; margin-bottom:20px; overflow:hidden; box-shadow:0 4px 12px rgba(232,87,135,.10); }
.project-header { padding:16px 24px; background:var(--pink-50); border-bottom:1px solid var(--border); display:flex; align-items:center; gap:16px; }
.project-header h2 { margin:0; color:var(--pink-700); font-size:18px; }
.project-stats { color:var(--muted); font-size:13px; }
.project-stats .stat-pass { color:var(--success); font-weight:bold; }
.project-stats .stat-fail { color:var(--danger); font-weight:bold; }
table { width:100%; border-collapse:collapse; }
th,td { padding:10px 16px; text-align:left; border-bottom:1px solid var(--border); font-size:13px; }
th { background:var(--pink-50); color:var(--pink-700); font-weight:bold; }
tr:hover { background:var(--pink-50); }
.outcome { display:inline-block; padding:3px 10px; border-radius:999px; font-size:11px; font-weight:bold; }
.outcome.pass { background:var(--success-bg); color:var(--success); }
.outcome.fail { background:var(--danger-bg); color:var(--danger); }
.outcome.skip { background:var(--warning-bg); color:var(--warning); }
.error { color:var(--danger); font-family:Consolas,Monaco,monospace; font-size:11px; white-space:pre-wrap; background:var(--danger-bg); padding:8px; border-radius:6px; margin-top:6px; }
.footer { text-align:center; color:var(--muted); font-size:12px; padding:20px 0; }
.paw { color:var(--pink-500); font-size:18px; }
'@

$projectSections = New-Object System.Text.StringBuilder
foreach ($run in $allRuns) {
    $headerStats = "通過 <span class='stat-pass'>$($run.Passed)</span> · 失敗 <span class='stat-fail'>$($run.Failed)</span> · 跳過 $($run.Skipped) · 共 $($run.Total) 項"
    [void]$projectSections.AppendLine("<section class='project'><div class='project-header'><h2>$([System.Web.HttpUtility]::HtmlEncode($run.ProjectName))</h2><div class='project-stats'>$headerStats</div></div>")
    [void]$projectSections.AppendLine("<table><thead><tr><th style='width:55%'>測試</th><th style='width:15%'>類別</th><th style='width:10%'>狀態</th><th style='width:10%'>耗時</th></tr></thead><tbody>")
    foreach ($c in ($run.Cases | Sort-Object ClassName, TestName)) {
        $cls = ($c.ClassName -split '\.')[-1]
        $name = HtmlEncode $c.TestName
        $outcome = switch ($c.Outcome) {
            'Passed' { 'pass'; break }
            'Failed' { 'fail'; break }
            default  { 'skip' }
        }
        $outcomeText = switch ($c.Outcome) {
            'Passed' { '通過'; break }
            'Failed' { '失敗'; break }
            default  { $c.Outcome }
        }
        $errBlock = ''
        if ($c.ErrorMsg) { $errBlock = "<div class='error'>$([System.Web.HttpUtility]::HtmlEncode($c.ErrorMsg))</div>" }
        [void]$projectSections.AppendLine("<tr><td>$name$errBlock</td><td>$cls</td><td><span class='outcome $outcome'>$outcomeText</span></td><td>$($c.Duration)</td></tr>")
    }
    [void]$projectSections.AppendLine("</tbody></table></section>")
}

$status = if ($grandFailed -gt 0) { '<span style="color:var(--danger)">❌ 失敗</span>' } else { '<span style="color:var(--success)">✅ 全綠</span>' }

$html = @"
<!doctype html>
<html lang="zh-Hant"><head><meta charset="utf-8"><title>貳寶寵物美容工坊 — 測試報告</title><style>$css</style></head>
<body><div class="container">
  <div class="hero">
    <h1>🐾 貳寶寵物美容工坊 — 測試報告</h1>
    <div class="subtitle">產生時間：$timestamp　·　$status</div>
  </div>
  <div class="summary">
    <div class="card"><div class="metric total">$grandTotal</div><div class="label">總測試數</div></div>
    <div class="card"><div class="metric pass">$grandPassed</div><div class="label">通過</div></div>
    <div class="card"><div class="metric fail">$grandFailed</div><div class="label">失敗</div></div>
    <div class="card"><div class="metric skip">$grandSkipped</div><div class="label">跳過 / 其他</div></div>
  </div>
  $($projectSections.ToString())
  <div class="footer"><span class="paw">🐶 🐱 🐾</span><br>© 貳寶寵物美容工坊 — TDD 真心打造</div>
</div></body></html>
"@

Set-Content -Path $OutputPath -Value $html -Encoding UTF8
Write-Host "Report saved: $OutputPath" -ForegroundColor Green
Write-Host "Total: $grandTotal | Passed: $grandPassed | Failed: $grandFailed | Skipped: $grandSkipped"
