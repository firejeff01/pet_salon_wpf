# pet_salon_wpf 架構概覽（SA 階段掃描）

掃描日期：2026-05-16
掃描者：SA agent
適用範圍：客戶反饋 R1–R8 變更（spec：`pm/spec_contract_pdf_customer_feedback_changes.md`）

---

## 1. Target Runtime / SDK

- **.NET SDK**：10.0.300（`global.json` 鎖定）
- **WPF TargetFramework**：`net10.0-windows`（src/PetSalon.Wpf、tests/PetSalon.Wpf.Tests、tests/PetSalon.Wpf.UiTests）
- **Core / Infrastructure / Core.Tests TargetFramework**：`net10.0`（無 Windows 相依）
- **Solution**：`PetSalon.sln`（位於 repo root）

## 2. 高階分層

| 層 | 專案 | 角色 |
| --- | --- | --- |
| Presentation | `src/PetSalon.Wpf` | WPF + MVVM（CommunityToolkit.Mvvm 之 `[ObservableProperty]` / `[RelayCommand]`）；Views、ViewModels、Dialogs、Behaviors、Converters、Controls |
| Application / Domain | `src/PetSalon.Core` | 純 C# 資料模型與服務介面：`Entities`、`Constants`、`Enums`、`Dtos`、`Abstractions`、`Common`、`Services` |
| Infrastructure | `src/PetSalon.Infrastructure` | EF Core SQLite 持久層（`PetSalonDbContext`、`Configurations/`）、PDF 產生器（`Pdf/`）、檔案系統 / 身分 / OS 介面實作 |
| Tests | `tests/PetSalon.Core.Tests` | 單元 / 整合測試（xUnit v3 + FluentAssertions），無 Windows 相依 |
| Tests | `tests/PetSalon.Wpf.Tests` | ViewModel 與 WPF 邏輯測試（xUnit v3，net10.0-windows） |
| Tests | `tests/PetSalon.Wpf.UiTests` | 端對端 UI 自動化（xUnit v3 + FlaUI.UIA3，net10.0-windows），透過 `AppFixture` 啟動實際 EXE |

## 3. 啟動入口 / Composition Root

- `src/PetSalon.Wpf/App.xaml.cs` — WPF 啟動，預期透過 .NET **Generic Host** 註冊 DI 容器（`Microsoft.Extensions.Hosting`）。
- `src/PetSalon.Infrastructure/DependencyInjection.cs` — Infrastructure 的 DI 擴充方法（將 `PetSalonDbContext`、`IPdfGenerator`、`IFileOpener`、`IClock`、`IIdGenerator` 等註冊到容器）。
- `src/PetSalon.Wpf/MainWindow.xaml` + `MainViewModel.cs` — 主視窗，內含導覽分頁（home / customer / owner / calendar / appointments / backup）。

## 4. 資料持久層

- **DbContext**：`PetSalon.Infrastructure.Persistence.PetSalonDbContext`（繼承 `IPetSalonDbContext` 抽象介面）
- **DbSet**：`Owners`、`Pets`、`Appointments`、`GroomingRecords`
- **儲存引擎**：EF Core 10.0 + SQLite（package `Microsoft.EntityFrameworkCore.Sqlite 10.0.0`）
- **EF Configurations**：`Configurations/{Owner,Pet,Appointment,GroomingRecord}Configuration.cs`
- **特殊型別映射**：
  - `Pet.Personality` / `Pet.MedicalHistory` / `GroomingRecord.Personality` / `GroomingRecord.MedicalHistory`：`List<string>` → JSON 字串（`HasConversion` + `ValueComparer`）
  - `Pet.PhysicalExamination` / `GroomingRecord.PhysicalExamination`：EF Core **Owned Entity**，6 個欄位映射到主表的 `phys_eyes` / `phys_ears` / `phys_teeth` / `phys_limbs` / `phys_skin` / `phys_fur` 欄位
- **R6 / Q5 影響**：`PhysicalExamination` 需新增 6 個 `*Note : string?` 欄位 → `PetConfiguration` 與 `GroomingRecordConfiguration` 的 `OwnsOne` 區塊需擴充 `phys_eyes_note` 等 6 個 column，且**需要產生一支 EF Core migration** 將欄位加進 SQLite schema（既有資料 backfill 為 NULL，沒有破壞性 migration）。
- **DesignTimeDbContextFactory**：`src/PetSalon.Infrastructure/Persistence/DesignTimeDbContextFactory.cs`（提供 `dotnet ef migrations add` 設計時建構 DbContext）。

## 5. PDF 產生流程

```
GroomingRecord(+Owner/Pet/Appointment)
        ↓ packed by Owner.PreferredAnimalHospital* + signature bytes
ContractRenderData (record)  ← src/PetSalon.Core/Abstractions/IPdfGenerator.cs
        ↓
ContractTemplateRenderer.Render(data) → HTML
        ↓ uses Handlebars.Net + contract.hbs + kaiu.ttf（data URI）
PuppeteerSharpContractGenerator.GenerateContractAsync(...)
        ↓ launches headless Chromium via PuppeteerSharp.BrowserFetcher
        ↓ page.SetContentAsync(html) → page.PdfAsync(...) A4
Output PDF file (4 pages)
```

關鍵元件：
- `src/PetSalon.Core/Abstractions/IPdfGenerator.cs` — `ContractRenderData` record / `IPdfGenerator` 介面 / `ContractGenerateOutput` record
- `src/PetSalon.Infrastructure/Pdf/ContractTemplateRenderer.cs` — Handlebars.Net 編譯 + `BuildViewModel(data)` 把 entity 轉成 Handlebars dictionary
- `src/PetSalon.Infrastructure/Pdf/PuppeteerSharpContractGenerator.cs` — 啟動 Chromium（cache 在 `chromiumCacheDir`）並輸出 PDF
- `src/PetSalon.Infrastructure/Pdf/Templates/contract.hbs` — 唯一的契約模板（4 個 `<section class="page">`，需依 R4 重排序）
- `src/PetSalon.Infrastructure/Pdf/Assets/kaiu.ttf` — 楷體字型，以 base64 data URI 嵌入 HTML
- 備用 generator：`src/PetSalon.Infrastructure/Pdf/QuestPdfContractGenerator.cs`（目前正式線上未使用；R4 / R5 / R6 / R8 模板變更**僅針對 contract.hbs**，QuestPdf 路徑不受影響）

## 6. MVVM 框架

- **NuGet**：`CommunityToolkit.Mvvm`（推測為 8.x，原始碼以 `[ObservableProperty]` partial / `[RelayCommand]` 命令模式撰寫）
- **ViewModel 基類**：`src/PetSalon.Wpf/ViewModels/ViewModelBase.cs`
- **特別 ViewModel**：
  - `OwnerPageViewModel` — R1 的 UI 行為主體（緊急聯絡人三欄）
  - `PetEditViewModel` — R3、R6、R7 的 UI 行為主體（晶片、身體狀態檢查、病史）
  - `ContractGenerateDialogViewModel` — R2、R4、R5、R8 的觸發點（產生契約 PDF）
  - `GroomingPageViewModel` — R6、R7 的服務當下填寫頁面（PDF 以此為準）

## 7. UI 測試框架

- **Runner**：xUnit v3（package `xunit.v3 3.1.0` + `xunit.runner.visualstudio 3.1.5`）
- **Automation library**：`FlaUI.UIA3 5.0.0`（驅動 Windows UI Automation v3）
- **Fixture**：`tests/PetSalon.Wpf.UiTests/Helpers/AppFixture.cs`
  - 每個測試類別啟動一份新的 `PetSalon.Wpf.exe`（用 `PETSALON_APP_DATA` 環境變數導向獨立暫存資料夾）
  - 提供 `MainWindow`、`FindById(automationId)`、`WaitForId(automationId, timeoutMs)`
- **Helpers**：`UiTestExtensions.cs` — `ClickById`、`TypeInto`、`ReadText`、`IsEnabled`、`FindDialog`、`CloseAllDialogs`
- **AutomationId 命名規範**（從現有 XAML 觀察）：
  - 按鈕：`btn-{action}-{target}`（如 `btn-customer-submit`、`btn-save-owner`、`btn-create-backup`）
  - 文字框：`txt-{entity}-{field}`（如 `txt-owner-name`、`txt-owner-emergency-phone`）
  - 清單 / 表格：`list-{collection}` / `grid-{collection}`（如 `list-owners`、`grid-backups`）
  - 導覽：`nav-{page}`（如 `nav-customer`、`nav-home`）
- **既知缺口（待 dev 階段補）**：
  - `src/PetSalon.Wpf/Views/PetEditView.xaml` 目前**沒有** AutomationId — R3 / R6 / R7 的 UI 場景需要 dev 為晶片欄位、6 個身體部位 ComboBox、病史 CheckBox 群組補上 `AutomationId`。SA 在 `sa-feedback-r3-*.feature` / `sa-feedback-r6-*.feature` / `sa-feedback-r7-*.feature` 中以建議命名標註。
  - `ContractGenerateDialog`（R2 / R4 / R5 / R8 觸發點）需確認對話框的 AutomationId 命名。

## 8. PDF 整合測試慣例

- 既有樣板：`tests/PetSalon.Core.Tests/Pdf/PuppeteerSharpContractGeneratorTests.cs`
- 流程：
  1. 從 `typeof(PuppeteerSharpContractGenerator).Assembly.Location` 推導 Infrastructure output 目錄（透過 csproj 的 `CopyToOutputDirectory`）
  2. 把 `Pdf/Templates/contract.hbs` 與 `Pdf/Assets/kaiu.ttf` 複製到測試 bin
  3. 共用 Chromium cache 目錄 `%TEMP%/petsalon-test-chromium-cache`（避免每個測試類別重複下載 ~150MB）
  4. 用 `SampleData()` 建立 `Owner` / `Pet` / `Appointment` / `GroomingRecord` 範例
  5. 斷言 PDF 檔案存在、magic header `%PDF`、檔名格式
- **R1–R8 整合測試**會大量沿用此模式，但需要**斷言 HTML 內容**（透過 `ContractTemplateRenderer.Render()` 取 HTML 字串再用 FluentAssertions / regex 檢查），因為直接解析 PDF 內容文字需要額外套件（如 `iText7` 或 `PdfPig`），效率較低。

## 9. 既有測試品類分類（`[Trait("category", ...)]`）

- `category=integration`：實際啟 Chromium 產 PDF（慢）→ R4 / R5 / R8 PDF 視覺驗證
- `category=ui`：實際啟 WPF 主程式 + FlaUI → R1 / R2 / R3 / R6 / R7 的 UI 行為驗證
- 未標 trait：純單元測試（Renderer 渲染 HTML、`MedicalHistoryOptions`、`BodyConditionOptions` 等常數）→ R3 / R6 / R7 / R8 的快測試

## 10. 本次變更對架構的整體影響

| 需求 | 模板 | Entity / DB | ViewModel / View | DI / Startup |
| --- | --- | --- | --- | --- |
| R1 | contract.hbs（page 1、page 4 緊急聯絡人列） | 無 | OwnerPageView / OwnerPageViewModel（移除 RequiredText） | 無 |
| R2 | 無（既有模板即為紙本線） | 無 | ContractGenerateDialogVM、GroomingPageVM（移除簽名畫布相關控制項） | 無 |
| R3 | contract.hbs 第 100、307 行（勾選判定） | 無 | PetEditView / PetEditViewModel（補輔助文字） | 無 |
| R4 | contract.hbs 4 個 `<section>` 重新排序 | 無 | 無 | 無 |
| R5 | contract.hbs 第 239、240、432 行（移除 `{{owner.name}}` 與「貳寶寵物美容工坊」） | 無 | 無 | 無 |
| R6 | contract.hbs 第 334–363 行（每部位新增「異常」勾選格 + 異常說明） | **PhysicalExamination 新增 6 個 `*Note : string?`**（**需 EF Core migration**） | PetEditViewModel、GroomingPageViewModel（單選改為「正常/異常」+ 動態說明 TextBox） | 無 |
| R7 | contract.hbs 第 377–406 行（新增「以上皆無」勾選格、空陣列邏輯） | 無（`MedicalHistoryOptions.All` 是 readonly 常數，新增第 19 項為「以上皆無」） | PetEditViewModel、GroomingPageViewModel（互斥規則） | 無 |
| R8 | contract.hbs 第 413–425 行（＊段 + 1~8 點全段替換，含 Q4 修正） | 無 | 無 | 無 |

**Migration 影響只在 R6**：dev 階段需執行 `dotnet ef migrations add AddPhysicalExaminationNotes` 並 review SQL（純增欄、不改型別、向後相容 NULL）。
