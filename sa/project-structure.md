# pet_salon_wpf 專案結構對照表（SA 階段掃描）

掃描日期：2026-05-16
適用範圍：R1–R8 客戶反饋變更的程式碼放置位置與測試對應

---

## 1. Solution 結構

`PetSalon.sln`（位於 `C:\WorkSpace\pet_salon_wpf\`）含 6 個 csproj：

```
PetSalon.sln
├── src/
│   ├── PetSalon.Core/                 (net10.0)         領域與服務介面
│   ├── PetSalon.Infrastructure/       (net10.0)         EF Core + PDF + OS
│   └── PetSalon.Wpf/                  (net10.0-windows) WPF UI + MVVM
└── tests/
    ├── PetSalon.Core.Tests/           (net10.0)         單元/整合測試
    ├── PetSalon.Wpf.Tests/            (net10.0-windows) ViewModel 測試
    └── PetSalon.Wpf.UiTests/          (net10.0-windows) E2E UI 測試（FlaUI）
```

---

## 2. `src/PetSalon.Core/`

```
src/PetSalon.Core/
├── Abstractions/
│   ├── IClock.cs
│   ├── IFileOpener.cs
│   ├── IIdGenerator.cs
│   ├── IPdfGenerator.cs              ← ContractRenderData + IPdfGenerator + ContractGenerateOutput
│   └── IPetSalonDbContext.cs
├── Common/                            ← RocDate 等 helper
├── Constants/
│   ├── BodyExaminationOptions.cs     ← R6 / Q8：Fur 需擴充「異常」
│   ├── DefaultHospital.cs
│   ├── GroomingServiceOptions.cs
│   ├── MedicalHistoryOptions.cs      ← R7：擴充第 19 項「以上皆無」
│   └── PersonalityOptions.cs
├── Dtos/                              ← 服務間傳輸資料
├── Entities/
│   ├── Appointment.cs
│   ├── GroomingRecord.cs             ← 內含 PhysicalExamination (owned)
│   ├── Owner.cs                       ← R1
│   └── Pet.cs                         ← R3 / R6（含 PhysicalExamination class 定義）
├── Enums/
└── Services/
    ├── AppointmentService.cs
    ├── BackupService.cs
    ├── ContractService.cs             ← R2 / R4 / R5 / R8 觸發點（產契約 PDF）
    ├── GroomingRecordService.cs       ← R6 / R7 寫入 PDF 來源
    ├── OwnerService.cs                ← R1 寫入點（移除三欄必填驗證）
    ├── PetService.cs                  ← R3 / R6 / R7 寫入點
    └── StoredValueService.cs
```

---

## 3. `src/PetSalon.Infrastructure/`

```
src/PetSalon.Infrastructure/
├── DependencyInjection.cs            ← DI 註冊：DbContext、IPdfGenerator 等
├── Identity/                          ← IIdGenerator 實作
├── Os/                                ← IFileOpener、IClock 實作
├── Pdf/
│   ├── Assets/
│   │   └── kaiu.ttf                  ← 楷體字型（以 data URI 嵌入 HTML）
│   ├── Templates/
│   │   └── contract.hbs              ← ★ R3 / R4 / R5 / R6 / R7 / R8 模板主修改檔
│   ├── ContractTemplateRenderer.cs   ← Handlebars 渲染 + BuildViewModel
│   ├── PuppeteerSharpContractGenerator.cs  ← 正式 PDF 產生器（線上使用）
│   └── QuestPdfContractGenerator.cs  ← 備援（本次 R 不改）
└── Persistence/
    ├── Configurations/
    │   ├── AppointmentConfiguration.cs
    │   ├── GroomingRecordConfiguration.cs    ← R6：擴充 OwnsOne 加 6 個 *Note
    │   ├── OwnerConfiguration.cs
    │   └── PetConfiguration.cs                ← R6：擴充 OwnsOne 加 6 個 *Note
    ├── DesignTimeDbContextFactory.cs
    └── PetSalonDbContext.cs
```

---

## 4. `src/PetSalon.Wpf/`

```
src/PetSalon.Wpf/
├── App.xaml / App.xaml.cs            ← 啟動 + Generic Host + DI
├── MainWindow.xaml / .cs
├── Assets/                            ← 圖示、樣式資源
├── Behaviors/                         ← XAML attached property（如 LabelHelper.RequiredText）
│   └── LabelHelper.cs（推測）          ← R1 影響：OwnerPageView 三欄 RequiredText 需移除
├── Controls/                          ← 自訂 control
├── Converters/                        ← IValueConverter 群組
├── Dialogs/                           ← 對話框（含 ContractGenerateDialog?）
├── Services/                          ← WPF-only service（如 IDialogService 實作）
├── Themes/                            ← 共用 Style / Brush
├── ViewModels/
│   ├── AppointmentEditViewModel.cs
│   ├── BackupPageViewModel.cs
│   ├── CalendarViewModel.cs
│   ├── ContractGenerateDialogViewModel.cs ← R2 / R4 / R5 / R8 觸發點
│   ├── CustomerFormViewModel.cs
│   ├── DailyAppointmentsViewModel.cs
│   ├── GroomingPageViewModel.cs       ← R6 / R7 PDF 來源資料的填寫頁
│   ├── HomeViewModel.cs
│   ├── MainViewModel.cs
│   ├── OwnerPageViewModel.cs          ← R1：緊急聯絡人三欄
│   ├── PetEditViewModel.cs            ← R3 / R6 / R7：晶片、身體狀態、病史
│   └── ViewModelBase.cs
└── Views/
    ├── AppointmentEditView.xaml(.cs)
    ├── BackupPageView.xaml(.cs)
    ├── CalendarView.xaml(.cs)
    ├── CustomerFormView.xaml(.cs)     ← AutomationId: btn-customer-submit/reset/add-pet
    ├── DailyAppointmentsView.xaml(.cs)
    ├── GroomingPageView.xaml(.cs)
    ├── HomeView.xaml(.cs)
    ├── OwnerPageView.xaml(.cs)        ← AutomationId: txt-owner-emergency-*（R1）
    └── PetEditView.xaml(.cs)          ← ★ 目前無 AutomationId，R3 / R6 / R7 dev 階段需補
```

### 4.1 既有 AutomationId 命名摘要（給 sa-feedback-*.feature 引用）

| 區域 | 既存 AutomationId | 對應控制項 |
| --- | --- | --- |
| 導覽 | `nav-home`, `nav-customer`, `nav-owner`, `nav-calendar`, `nav-appointments`, `nav-backup` | MainWindow 左側導覽按鈕 |
| 飼主清單 | `txt-search`, `btn-new-owner`, `list-owners`, `btn-save-owner` | OwnerPageView 上半 |
| 飼主表單（R1） | `txt-owner-name`, `txt-owner-national-id`, `txt-owner-phone`, `txt-owner-address`, `txt-owner-emergency-name`, `txt-owner-emergency-phone`, `txt-owner-emergency-relationship` | OwnerPageView 編輯區 |
| 飼主關聯 | `btn-add-appointment`, `btn-add-pet` | OwnerPageView 飼主子清單按鈕 |
| 客戶表單 | `btn-customer-submit`, `btn-customer-reset`, `btn-customer-add-pet` | CustomerFormView |
| 日曆 | `btn-view-month/week/day/today`, `btn-cal-prev/next`, `txt-cal-month-label` | CalendarView |
| 每日預約 | `txt-cancel-reason`, `btn-mark-complete`, `btn-cancel-appointment`, `btn-delete-appointment`, `grid-appointments` | DailyAppointmentsView |
| 備份 | `btn-create-backup`, `btn-refresh-backup`, `btn-restore-backup`, `btn-delete-backup`, `grid-backups` | BackupPageView |
| 首頁 | `btn-home-go-today`, `btn-home-go-owners` | HomeView |

### 4.2 SA 階段提議的新 AutomationId（dev 補上時請對齊）

| 需求 | View | 建議 AutomationId | 對應控制項 |
| --- | --- | --- | --- |
| R3 | `PetEditView.xaml` | `txt-pet-chip-number` | 晶片號碼 TextBox（第 60 行）|
| R3 | `PetEditView.xaml` | `txt-pet-unregistered-id` | 非晶片識別方式 TextBox（第 64 行）|
| R3 | `PetEditView.xaml` | `txt-pet-chip-hint` | 「晶片號碼」旁的輔助提示文字 |
| R6 | `PetEditView.xaml` | `cmb-phys-eyes`, `cmb-phys-ears`, `cmb-phys-teeth`, `cmb-phys-limbs`, `cmb-phys-skin`, `cmb-phys-fur` | 6 個身體部位 ComboBox（第 101–122 行） |
| R6 | `PetEditView.xaml` | `txt-phys-eyes-note`, `txt-phys-ears-note`, `txt-phys-teeth-note`, `txt-phys-limbs-note`, `txt-phys-skin-note`, `txt-phys-fur-note` | 6 個動態顯示之「異常說明」TextBox（dev 新增） |
| R7 | `PetEditView.xaml` | `cb-medical-none-of-above` | 病史「以上皆無」CheckBox |
| R7 | `PetEditView.xaml` | `cb-medical-{disease}` 命名通則（例：`cb-medical-heart-disease`、`cb-medical-asthma`） | 既有 18 項病史 CheckBox |
| R2 / R4 / R5 / R8 | `Dialogs/ContractGenerateDialog.xaml` 或 `GroomingPageView.xaml` | `btn-generate-contract` | 「產生契約」主要按鈕（dev 確認對應 View） |
| R2 | （若存在）`SignaturePadDialog` | — | **移除整個對話框**（不再有 AutomationId） |

---

## 5. `tests/PetSalon.Core.Tests/`（單元 / 整合測試）

```
tests/PetSalon.Core.Tests/
├── Common/
├── Helpers/
├── Pdf/
│   └── PuppeteerSharpContractGeneratorTests.cs    ← R4 / R5 / R6 / R8 PDF 整合測試樣板
├── Sanity/
│   └── InfrastructureSanityTests.cs
├── Services/                                       ← R1 / R7 Service 行為測試
└── PetSalon.Core.Tests.csproj                      ← xUnit v3 3.1.0 + FluentAssertions 7.2.0
```

**StepDefinitions 放置位置（SA 階段新增）**：

```
tests/PetSalon.Core.Tests/StepDefinitions/
├── R1EmergencyContactSteps.cs        ← 對應 sa-feedback-r1-emergency-contact-optional.feature 之非 @ui 場景
├── R3ChipUnregisteredSteps.cs        ← 對應 sa-feedback-r3-*.feature 之 PDF / 模板 / Service 場景
├── R4PageOrderSteps.cs               ← 對應 sa-feedback-r4-*.feature 之 PDF 模板 / 整合測試
├── R5SignatureBlankSteps.cs          ← 對應 sa-feedback-r5-*.feature 之 PDF 整合測試
├── R6PhysicalExamSteps.cs            ← 對應 sa-feedback-r6-*.feature 之 Service / 模板 / Note 邏輯
├── R7MedicalHistorySteps.cs          ← 對應 sa-feedback-r7-*.feature 之 Service / 模板 / 互斥規則
└── R8ContractClausesSteps.cs         ← 對應 sa-feedback-r8-*.feature 之模板文字逐字斷言
```

理由：
- R1 的 Service 行為（`OwnerService.SaveAsync` 對三欄空值不擲例外）→ Core.Tests
- R2 的 PDF 流程「不再阻擋」→ Core.Tests（呼叫 `ContractService.GenerateAsync` 不擲例外）+ UiTests（畫面上的畫布 / 按鈕不存在）
- R3 的勾選邏輯：在 `ContractTemplateRenderer.Render(data)` HTML 字串裡找 `cb on` → Core.Tests
- R4 / R5 / R6 / R8 的 PDF 渲染：透過 `ContractTemplateRenderer.Render()` 取 HTML 後字串斷言 → Core.Tests
- R6 的 EF Core schema 新增欄位：透過 `DbContextOptionsBuilder.UseSqlite(...)` + in-memory SQLite 驗證 → Core.Tests
- R7 的 `MedicalHistoryOptions.All` 長度 / 末尾項 → Core.Tests

---

## 6. `tests/PetSalon.Wpf.UiTests/`（E2E UI 測試）

```
tests/PetSalon.Wpf.UiTests/
├── Backup/
├── Calendar/
├── CustomerForm/
│   └── CustomerFormUiTests.cs        ← 既有 R1 客戶表單 UI 測試樣板
├── DailyAppointments/
├── Helpers/
│   ├── AppFixture.cs                 ← 啟動 PetSalon.Wpf.exe 並提供 FlaUI 操作
│   └── UiTestExtensions.cs           ← ClickById / TypeInto / FindDialog / CloseAllDialogs
├── HomePage/
├── MessageDialog/
├── Navigation/
├── OwnerPage/
├── Sanity/
└── PetSalon.Wpf.UiTests.csproj       ← xUnit v3 + FlaUI.UIA3 5.0.0 + FluentAssertions
```

**StepDefinitions 放置位置（SA 階段新增）**：

```
tests/PetSalon.Wpf.UiTests/StepDefinitions/
├── R1EmergencyContactUiSteps.cs      ← OwnerPageView 三欄不顯示紅星、可儲存
├── R2SignaturePadRemovalUiSteps.cs   ← ContractGenerate / GroomingPage 不應出現簽名畫布
├── R3ChipHintUiSteps.cs              ← PetEditView 晶片旁輔助文字、雙欄行為
├── R6PhysicalExamUiSteps.cs          ← PetEditView 6 個 ComboBox + 動態 Note TextBox
└── R7MedicalHistoryUiSteps.cs        ← PetEditView 病史互斥規則 + 「以上皆無」可勾選
```

R4 / R5 / R8 屬純 PDF 模板變更，UiTests 不需新增（PDF 內容已透過 Core.Tests 之整合測試覆蓋）。

---

## 7. BDD 框架選擇與整合方式

### 7.1 框架決策

**選擇：pure xUnit v3 + `[Fact]` / `[Theory]`，以 .feature 為「規格參考文件」**

理由：
1. 專案已使用 **xUnit v3 3.1.0**。
2. Reqnroll（SpecFlow 後繼）截至 SA 掃描日（2026-05-16）對 xUnit v3 支援尚未完全穩定（既有套件主要對應 xUnit 2.x）。
3. 額外加入 BDD runner 套件會引入新的 build dependency 與 source generator，與既有 `Microsoft.NET.Test.Sdk 17.14.0` 鏈結需額外驗證。
4. 採用 `[Fact]` / `[Theory]` 一對一對應 .feature scenario 可達相同可讀性：
   - 每個 `Scenario:` → 一個 `[Fact]` 方法
   - 每個 `場景大綱:` + `例子:` → 一個 `[Theory]` + `[InlineData]`
   - 方法名稱直接抄 .feature 中文標題，並在 `[Trait]` 中標註 `R{n}`、`feature=<scenario name>` 對應追溯
5. Step Definition 仍依 R 分檔，使用 C# `partial class` 切分可在未來改用 Reqnroll 時無痛遷移（partial class 是 Reqnroll 與 SpecFlow 的標準切分方式）。

### 7.2 命名慣例

```csharp
[Trait("category", "integration")]
[Trait("feature", "R6-PhysicalExamAbnormalWithNote")]
public partial class R6PhysicalExamSteps
{
    // === Given 區 ===
    [Fact(DisplayName = "Given GroomingRecord.PhysicalExamination.Eyes 為 異常")]
    public void Given_grooming_record_eyes_is_abnormal() { /* TODO */ }

    // === Scenario：5 個部位於 PDF 同時呈現「正常」與「異常」兩個勾選格 ===
    [Fact(DisplayName = "R6 / Scenario: 5 個部位於 PDF 同時呈現「正常」與「異常」兩個勾選格")]
    public void Five_body_parts_show_normal_and_abnormal_checkboxes()
    {
        // Given ...
        // When ...
        // Then ...
    }
}
```

UI 測試類別同步沿用既有 `IClassFixture<AppFixture>` + `[Collection("uiseq")]` pattern（參考 `CustomerFormUiTests.cs`）。
