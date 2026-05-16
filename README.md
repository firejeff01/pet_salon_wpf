# pet_salon2 — WPF (.NET 10) 重寫版

貳寶寵物美容工坊 — 犬貓美容定型化契約管理系統（WPF 桌面版）

## 技術棧

- **平台**: .NET 10 + WPF (Windows-only)
- **資料庫**: SQLite + EF Core 10（自動建立 schema 於 `%LOCALAPPDATA%\PetSalon\petsalon.db`）
- **MVVM**: CommunityToolkit.Mvvm（source-generator 屬性與命令）
- **DI / Host**: Microsoft.Extensions.Hosting
- **PDF**: QuestPDF（Community 授權）
- **簽名板**: 自製 SignaturePad UserControl（InkCanvas → PNG）

## 專案結構

```
src/
├── PetSalon.Core/            # 領域模型、Services、DTO、抽象介面
├── PetSalon.Infrastructure/  # EF Core DbContext、QuestPDF、FileOpener
└── PetSalon.Wpf/             # XAML View、ViewModel、Theme、Controls
```

## 跑起來

```powershell
dotnet build
dotnet run --project src/PetSalon.Wpf/PetSalon.Wpf.csproj
```

## 業主決議落實對照（14 項）

| # | PM/SA | 決議 | 落實位置 |
|---|-------|------|----------|
| 1 | PM | 預約完整狀態 | `AppointmentStatus.cs` |
| 2 | PM | 同日重複預約警告 | `AppointmentService.CreateAsync` |
| 3 | PM | PDF 覆蓋最新 | `ContractService.GenerateAsync` 同檔名覆蓋 |
| 4 | PM | 電子簽名嵌入 PDF | `SignaturePad` + `QuestPdfContractGenerator` |
| 5 | PM | 費用拆三項自動加總 | `FeeItems` + `ServiceRecordViewModel.TotalFee` |
| 6 | PM | 美容項目預設選單 | `GroomingItems.cs` + `GroomingItemSelection` |
| 7 | PM | 產 PDF 後自動開啟 | `ContractService` + `WindowsFileOpener` |
| 1 | SA | 服務紀錄與預約一次更新 | `ServiceRecordService.IntegratedUpdateAsync` |
| 2 | SA | 行事曆視圖 | `CalendarView` 月視圖（週/日延伸位） |
| 3 | SA | PDF 檔名 sanitize | `FileNameSanitizer` |
| 4 | SA | 「同日」= 本機日曆日 | `DateOnly` 比對 |
| 5 | SA | 健康布林歸 ServiceRecord | `ServiceRecord.NeedsSpecialCare/IsOverEightYears` |
| 6 | SA | 店家簽名可儲存 | `ShopSettings.ShopSignaturePng` + `ShopSettingsView` |
| 7 | SA | 預約刪除限制 | `AppointmentService.DeleteAsync` |

## 已知限制與後續

- 行事曆目前僅實作月視圖；週/日視圖可在 `CalendarView` 上加 `TabControl`
- 沒有自動化測試（原 Vue/Node 版有 199 個測試做規格對照，建議補 xUnit）
- 簽名板沒有實作 `LoadFromPng`，店家簽名儲存後不可在簽名板重播編輯
- 警告：`System.Security.Cryptography.Xml` 9.0.0 為 EF Core 10 傳遞依賴，待 EF Core patch 升版
