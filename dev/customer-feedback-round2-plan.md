# 開發計畫書：客戶回饋第二輪（自填資料、刪除與店家簽名）

## 文件狀態

- 狀態：需求分析、技術設計、實作與回歸驗證均已完成。
- 專案根目錄：`C:/WorkSpace/pet_salon_wpf`
- 本文件建立依據：2026-08-18 客戶提出的四項回饋。
- 業主已於 2026-08-18 明確回覆「確認開始」，並依已確認決策完成實作。

## 實作驗證結果

- `dotnet build PetSalon.sln --no-restore`：成功，0 errors。
- Core 測試：298/298 通過（含 Chromium 實際 PDF 整合測試）。
- WPF ViewModel 測試：87/87 通過。
- Windows UI 自動化：46 通過、7 略過、0 失敗。
- 既有 NU1903 高嚴重性套件弱點警告仍存在，需另立依賴升級工作處理。

## 來源文件

### 既有 PM / SA 文件

- PM Spec：`pm/spec_contract_pdf_customer_feedback_changes.md`
- 本輪 PM Spec：`pm/spec_customer_feedback_round2.md`
- 本輪事件風暴：`pm/event-storming-customer-feedback-round2.md`
- 本輪 PM Feature：
  - `pm/customer-self-service-round2.feature`
  - `pm/owner-deletion.feature`
  - `pm/shop-signature-pdf.feature`
- 本輪 SA Feature：
  - `sa/sa-customer-self-service-round2.feature`
  - `sa/sa-owner-deletion.feature`
  - `sa/sa-shop-signature-pdf.feature`
- 本輪 Step Definitions 骨架：
  - `sa/StepDefinitions/CustomerSelfServiceRound2Steps.cs`
  - `sa/StepDefinitions/OwnerDeletionSteps.cs`
  - `sa/StepDefinitions/ShopSignaturePdfSteps.cs`
- SA Feature：
  - `sa/sa-feedback-r1-emergency-contact-optional.feature`
  - `sa/sa-feedback-r2-remove-system-signature-pad.feature`
  - `sa/sa-feedback-r3-chip-unregistered-not-inferred.feature`
  - `sa/sa-feedback-r4-contract-pdf-page-order.feature`
  - `sa/sa-feedback-r5-signature-lines-blank.feature`
  - `sa/sa-feedback-r6-physical-exam-abnormal-with-note.feature`
  - `sa/sa-feedback-r7-medical-history-none-of-above.feature`
  - `sa/sa-feedback-r8-contract-clauses-text.feature`
- `sa/StepDefinitions/**/*.cs`：目前不存在；既有 Step Definitions 位於測試專案的 `tests/**/StepDefinitions/`。

### 本輪新增需求原文

1. 顧客自己打字的地方，備註即使有填寫也無法儲存。
2. 顧客資料無法刪除，誤存多次會出現多筆。
3. 顧客自己填寫的地方需新增「病史」與「是否有晶片」。
4. 店家希望手寫簽名可保存，往後每次產生 PDF 時可自動套入店家簽名。

## 專案架構分析

- 架構模式：WPF + MVVM（CommunityToolkit.Mvvm）／Core Service／EF Core SQLite／PuppeteerSharp + Handlebars PDF。
- Presentation：`src/PetSalon.Wpf`
- Domain / Application：`src/PetSalon.Core`
- Infrastructure：`src/PetSalon.Infrastructure`
- 自動測試：`tests/PetSalon.Core.Tests`、`tests/PetSalon.Wpf.Tests`、`tests/PetSalon.Wpf.UiTests`
- 命名與實作慣例參考：
  - ViewModel 命令：`OwnerPageViewModel.cs`、`DailyAppointmentsViewModel.cs`
  - 服務層 CRUD：`OwnerService.cs`、`AppointmentService.cs`
  - 確認對話框：`IDialogService.Confirm`
  - PDF 資料流：`ContractService` → `ContractRenderData` → `ContractTemplateRenderer` → `contract.hbs`
  - 本機資料根目錄：`%LOCALAPPDATA%/PetSalon`，測試時可由 `PETSALON_APP_DATA` 覆寫。

## 現況與根因分析

### 1. 顧客自填備註無法儲存

目前原始碼存在兩個明確缺口：

- `Owner`、`OwnerInput`、`OwnerFormFields` 與 `OwnerService.Apply` 都已支援 `Note`。
- 但 `CustomerFormView.xaml` 沒有備註 TextBox，因此顧客自填頁無法將備註輸入到 `Owner.Note`。
- `CustomerPetEntry` 也沒有寵物備註欄位，送出時建立的 `PetInput` 沒有設定 `Note`。
- `OwnerPageView.xaml` 同樣沒有呈現 `Form.Note`，所以後台雖有資料欄位，畫面上也不能檢視或修改。

因此本輪需先確認客戶所稱「備註」是飼主共用備註、每隻寵物備註，或兩者都要。依目前欄位語意，推薦同時提供：

- 飼主備註 → `Owner.Note`
- 毛孩備註（每隻各自）→ `Pet.Note`

兩者皆為選填，最大長度沿用既有 EF 設定 1000 字。

### 2. 顧客資料無法刪除與重複資料

目前確實沒有飼主刪除功能：

- `OwnerService` 沒有 `DeleteAsync`。
- `OwnerPageViewModel` 沒有刪除命令。
- `OwnerPageView.xaml` 沒有刪除按鈕。
- `Owner → Pet`、`Owner/Pet → Appointment` 關聯使用 `DeleteBehavior.Restrict`，不能直接刪除含關聯資料的飼主。

另外，自填送出流程有造成重複資料的結構性風險：

1. `CustomerFormViewModel.Submit` 先建立 Owner。
2. 接著逐隻建立 Pet。
3. 每次 `WithScopeAsync` 都建立新的 DI scope / DbContext。
4. 若任一 Pet 驗證失敗，Owner 已經先永久寫入；顧客修正後重送會再新增一筆 Owner。

建議把「顧客自填送出」改為一個應用服務與單一交易：

- 先在記憶體中驗證 Owner 與所有有填姓名的 Pet。
- 所有資料合法後，於同一個 DbContext transaction 一次建立 Owner 與 Pets。
- 任一步驟失敗時全部 rollback，不留下半套 Owner。
- 送出期間停用送出按鈕，避免連點重複送出。
- 是否對相同身分證字號做重複警告，須由業主確認；不建議未確認就直接建立 unique index，以免既有資料升級失敗。

刪除策略推薦採「保留歷史的條件式永久刪除」：

- 無任何 Appointment / GroomingRecord 的飼主：確認後可刪除其 Pets，再刪除 Owner，整批置於同一 transaction。
- 已有 Appointment 或 GroomingRecord 的飼主：禁止永久刪除，避免歷史契約、服務紀錄與儲值資料失去來源；後續如有需要可另做「封存／停用」功能。
- 確認視窗需顯示飼主姓名、寵物數量，並明示不可復原。
- 不將 EF 關聯全面改成 Cascade，避免日後單一誤操作連鎖刪除歷史紀錄。

### 3. 自填頁新增病史與晶片狀態

既有資料模型已足夠，不需新增資料欄位：

- `Pet.ChipNumber : string?`
- `Pet.UnregisteredIdMethod : string?`
- `Pet.MedicalHistory : List<string>`
- `Pet.MedicalHistoryOther : string`

目前 `CustomerPetEntry` 雖有 `ChipNumber` 屬性，但 `CustomerFormView.xaml` 沒有對應控制項，實際上顧客看不到也填不到；病史則連 ViewModel 狀態與送出 mapping 都沒有。

推薦 UI 與映射：

- 「是否有晶片」提供三種狀態：未填／有／無。
- 選「有」時顯示並要求晶片號碼；儲存到 `ChipNumber`，清空 `UnregisteredIdMethod`。
- 選「無」時可填替代識別方式；若未填則以既有約定寫入 `UnregisteredIdMethod = "無"`，讓 PDF 明確勾選「無／未登記」。
- 選「未填」時兩欄皆為 null，延續既有 R3「不得自行推論無晶片」規則。
- 病史選項直接沿用 `MedicalHistoryOptions.All`，包含「以上皆無」。
- 「以上皆無」與其他病史互斥；選「其它」時顯示 `MedicalHistoryOther` 文字框。
- 寫入 `PetInput.MedicalHistory` 與 `PetInput.MedicalHistoryOther`，不新增 schema。

### 4. 店家手寫簽名保存與 PDF 套用（新需求）

這是新需求，且會部分取代 2026-05-16 的 R2/R5 決策：

- 仍保留：產 PDF 不要求飼主在系統畫布簽名；飼主簽名／甲方簽章維持空白，供紙本親簽。
- 新增例外：店家的「美容人員簽名」與「乙方簽章」可套入預先保存的店家簽名圖。
- 不恢復每張契約都要現場簽名的流程。

#### 建議使用流程

1. 店家進入「店家簽名設定」。
2. 在既有 `SignaturePad` 手寫簽名，輸入簽名名稱（例如「店主」、「媽媽」）後儲存。
3. 可設定一個預設簽名，也可保留多個簽名。
4. 開啟契約預覽時，自動選取預設簽名；若有多個，可在預覽對話框切換。
5. 預覽 PDF 與正式 PDF 使用同一份簽名快照。
6. 若沒有設定簽名，PDF 店家簽名欄保持空白，不阻擋產生，延續 R2 的非阻擋原則。

#### 儲存設計

推薦採檔案式儲存，不新增 SQLite table：

```text
%LOCALAPPDATA%/PetSalon/
  signatures/
    profiles.json
    {signature-id}.png
```

- `profiles.json` 保存 id、顯示名稱、是否預設、建立／更新時間及 PNG 相對路徑。
- PNG 為裁切過、透明背景的簽名圖；不保存 InkCanvas 原始 strokes。
- 儲存採「暫存檔 + 原子替換」，避免中途當機留下半份 JSON 或 PNG。
- 刪除簽名只能在解析後確認目標位於 `signatures` 根目錄內，禁止任意路徑刪除。
- `BackupService` 的建立與還原流程需納入整個 `signatures/` 目錄。
- 此方案可直接跟隨既有備份搬移到其他電腦；因此不使用綁定單一 Windows 使用者的 DPAPI 加密。

#### 圖檔處理

- 主要輸入：既有 WPF `SignaturePad` 手寫後輸出 PNG。
- 可選延伸：允許匯入 PNG / JPEG 簽名圖片。
- 解碼後限制格式、尺寸與檔案大小；不只依副檔名判斷。
- 正規化為透明背景 PNG，裁切多餘邊界並限制最大尺寸，避免 PDF 跑版或影像耗盡記憶體。
- PDF 僅使用系統產生的 `data:image/png;base64,...`，不得把使用者可控的任意 HTML 或 URL 注入 Handlebars。

#### PDF 資料流變更

- `ContractRenderData` 新增獨立的 `ShopSignaturePng`；既有 `OwnerSignaturePng` 不得拿來承載店家簽名。
- `ContractTemplateRenderer` 產生 `shopSignatureDataUrl`。
- `contract.hbs` 只在以下兩處放入店家簽名 `<img>`：
  - 新 page 1：「美容人員簽名」
  - page 4：「乙方簽章」
- 「飼主簽名」與「甲方簽章」仍保持空白。
- `ContractService.PreviewAsync` 與 `CommitPreviewAsync` 必須取得同一份簽名 bytes，避免預覽與正式輸出不同。
- 若切換簽名，需重新產生預覽後才允許正式輸出。
- 不修改既有契約版本與檔名規則；已產生的舊 PDF 不回溯變更。

## 資料表異動清單

本設計不需要資料庫 schema 異動，因此不需要先執行 SQL。

理由：病史、晶片與備註均已有欄位；店家簽名採應用程式資料目錄的 PNG + JSON 儲存，並納入既有備份。

若業主改選「簽名存 SQLite」方案，則必須另行設計 `shop_signature_profiles` table、提供 migration / SQL，並在任何 schema-dependent 實作前由業主回覆「SQL 已執行」。

## 安全性預評估

- A01 權限／路徑控制：所有簽名與備份檔案操作必須解析完整路徑，並驗證仍位於允許的根目錄。
- A03 注入：PDF 模板中的文字沿用 Handlebars escaping；簽名僅接受解碼後重新編碼的 PNG bytes，不接受 HTML、SVG、外部 URL 或任意 data URI。
- A04 不安全設計：刪除 Owner 前顯示影響數量；有預約／服務歷史時阻擋；刪除與自填整批建立均使用 transaction。
- A05 設定錯誤：正式與測試資料路徑都由單一 options 物件注入，避免硬編 `%LOCALAPPDATA%` 導致測試污染真實資料。
- A08 軟體與資料完整性：`profiles.json` 原子寫入；還原備份時防 zip-slip，且簽名檔只還原到 `signatures/`。
- A09 日誌與錯誤：錯誤訊息不可輸出簽名 Base64 或完整敏感資料；只記錄 profile id／檔案名與操作結果。
- 敏感資料：手寫簽名視為高敏感個資，UI 需明示用途，刪除時需再次確認，備份檔亦包含此資料。
- 既有依賴風險：基準建置顯示 `SQLitePCLRaw.lib.e_sqlite3 2.1.11` 與 `System.Security.Cryptography.Xml 9.0.0` 有 NU1903 高嚴重性警告；不屬四項需求本身，但正式發版前應另排依賴升級與回歸測試。

## 影響範圍分析

### 預計新增檔案

- `src/PetSalon.Core/Dtos/CustomerRegistrationDtos.cs`
- `src/PetSalon.Core/Services/CustomerRegistrationService.cs`
- `src/PetSalon.Core/Dtos/ShopSignatureDtos.cs`
- `src/PetSalon.Core/Abstractions/IShopSignatureStore.cs`
- `src/PetSalon.Core/Services/ShopSignatureService.cs`
- `src/PetSalon.Infrastructure/Signatures/FileShopSignatureStore.cs`
- `src/PetSalon.Wpf/ViewModels/SignatureSettingsViewModel.cs`
- `src/PetSalon.Wpf/Views/SignatureSettingsView.xaml`
- 對應 Core / WPF / UI 測試檔案。

### 預計修改檔案

- 自填與備註：
  - `src/PetSalon.Wpf/ViewModels/CustomerFormViewModel.cs`
  - `src/PetSalon.Wpf/Views/CustomerFormView.xaml`
  - `src/PetSalon.Wpf/Views/OwnerPageView.xaml`
- 飼主刪除：
  - `src/PetSalon.Core/Services/OwnerService.cs`
  - `src/PetSalon.Wpf/ViewModels/OwnerPageViewModel.cs`
  - `src/PetSalon.Wpf/Views/OwnerPageView.xaml`
- 簽名與 PDF：
  - `src/PetSalon.Core/Abstractions/IPdfGenerator.cs`
  - `src/PetSalon.Core/Services/ContractService.cs`
  - `src/PetSalon.Infrastructure/Pdf/ContractTemplateRenderer.cs`
  - `src/PetSalon.Infrastructure/Pdf/Templates/contract.hbs`
  - `src/PetSalon.Wpf/ViewModels/ContractGenerateDialogViewModel.cs`
  - `src/PetSalon.Wpf/Dialogs/ContractGenerateDialog.xaml`
  - `src/PetSalon.Wpf/Controls/SignaturePad.xaml.cs`
  - `src/PetSalon.Infrastructure/DependencyInjection.cs`
  - `src/PetSalon.Wpf/App.xaml.cs`
  - `src/PetSalon.Wpf/App.xaml`
  - `src/PetSalon.Wpf/ViewModels/MainViewModel.cs`
- 備份：
  - `src/PetSalon.Core/Services/BackupService.cs`
- 既有 R2/R5 tests 與 SA 驗收條件需更新，不能再斷言四個簽名欄全部永遠空白。

## 業主確認事項（2026-08-18）

1. 備註同時提供「飼主共用備註」與「每隻寵物備註」。
2. 只允許永久刪除沒有 Appointment / GroomingRecord 歷史的飼主及其寵物；已有歷史時禁止永久刪除。
3. 顧客送出時若身分證字號或電話與既有資料相同，顯示可能重複警告，由店家決定是否仍要建立，不直接禁止。
4. 「是否有晶片」採三態：「未填／有／無」，未填不得推論為無晶片。
5. 店家可保存多組簽名、指定一組預設，並在產 PDF 時切換。
6. 選定的店家簽名同時套入 page 1「美容人員簽名」與 page 4「乙方簽章」；飼主簽名與甲方簽章保持空白。
7. 店家簽名同時支援系統內手寫，以及匯入 PNG／JPEG 圖片。
8. 預設簽名檔遺失、損毀或無法讀取時顯示警告，簽名欄保持空白，但仍允許產生 PDF。
9. 晶片狀態選擇「有」時，晶片號碼為必填；未填不得送出。

## 驗收設計摘要

### 顧客自填

- 飼主／寵物備註填寫後，重新從資料庫載入仍一致。
- 有／無／未填晶片三種狀態的 DB 與 PDF 語意符合 R3。
- 病史與「以上皆無」互斥，其他病史與自由文字正確儲存。
- 任一寵物驗證失敗時 Owner 與所有 Pets 都不應新增。
- 送出成功只新增一個 Owner 與預期數量的 Pets。

### 刪除

- 沒有歷史資料的 Owner 可在確認後連同 Pets 刪除。
- 使用者取消確認時不刪任何資料。
- 有 Appointment / GroomingRecord 時服務層與 UI 都阻擋。
- 刪除過程任一步驟失敗時 transaction rollback。

### 店家簽名

- 可儲存、預覽、重新命名、設預設與刪除簽名 profile。
- 無簽名時仍可產 PDF，四個簽名位置保持原本空白行為。
- 有店家簽名時，只在「美容人員簽名」與「乙方簽章」出現圖片。
- 「飼主簽名」與「甲方簽章」永遠不自動嵌入店家簽名。
- 預覽與正式 PDF 使用同一簽名。
- 切換簽名後預覽更新。
- 備份／還原包含簽名 profile 與 PNG。
- 惡意副檔名、超大圖片、毀損圖片與 signatures 根目錄外路徑均被拒絕。

## 基準驗證結果（2026-08-18）

- `PetSalon.Wpf.Tests`：87 / 87 通過。
- `PetSalon.Core.Tests`：276 通過、10 失敗；10 項皆為 PuppeteerSharp 整合測試找不到測試快取中的 Chromium executable，非本輪程式修改造成的 assertion failure。
- 第一次平行執行兩個 test project 時曾因共用 `obj/Debug` 發生 DLL 寫入鎖定；改為依序執行後 WPF 測試正常通過。
- UI Tests 尚未執行。

## 開發任務清單

- [x] 業主回答 8 項業務決策。
- [x] 用 PM 流程新增第二輪 spec、event storming 與 PM Gherkin。
- [x] 用 SA 流程掃描更新後規格，產生 engineer-facing feature 與 Step Definitions。
- [ ] 業主回覆「確認開始」。
- [ ] 先補 service / ViewModel 測試，再實作交易式顧客自填。
- [ ] 實作備註、病史與晶片狀態 UI / mapping。
- [ ] 實作安全的條件式 Owner 刪除與 UI 確認。
- [ ] 實作店家簽名 store、設定頁、備份／還原。
- [ ] 串接契約預覽與正式 PDF 的店家簽名。
- [ ] 更新與新決策衝突的 R2 / R5 tests。
- [ ] 執行 Core、WPF、UI 與 PDF 視覺回歸測試。
- [ ] 驗證 upgrade / backup / restore 與發版包。
