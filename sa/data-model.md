# pet_salon_wpf 資料模型對照表（SA 階段掃描）

掃描日期：2026-05-16
來源：`src/PetSalon.Core/Entities/`、`src/PetSalon.Infrastructure/Persistence/Configurations/`
適用範圍：R1–R8 客戶反饋變更

---

## 1. Entity 一覽

| Entity | 檔案 | EF 對應 Table | 主鍵 |
| --- | --- | --- | --- |
| `Owner` | `src/PetSalon.Core/Entities/Owner.cs` | `owners` | `OwnerId : string` |
| `Pet` | `src/PetSalon.Core/Entities/Pet.cs` | `pets` | `PetId : string` |
| `Appointment` | `src/PetSalon.Core/Entities/Appointment.cs` | `appointments` | `AppointmentId : string` |
| `GroomingRecord` | `src/PetSalon.Core/Entities/GroomingRecord.cs` | `grooming_records` | `GroomingRecordId : string` |
| `PhysicalExamination` | `src/PetSalon.Core/Entities/Pet.cs`（與 Pet 同檔，定義在第 31–39 行） | 內嵌至 `pets` 與 `grooming_records`（EF Core Owned Entity） | — |

---

## 2. `Owner`（R1 主要影響）

| 屬性 | 型別 | 必填 | EF 設定 | 備註 |
| --- | --- | --- | --- | --- |
| `OwnerId` | `string` | Yes（PK） | `HasMaxLength(40)` | id 由 `IIdGenerator` 產生 |
| `Name` | `string` | Yes | — | 飼主姓名 |
| `NationalId` | `string` | Yes | — | 身分證字號 |
| `Phone` | `string` | Yes | — | |
| `Address` | `string` | Yes | — | |
| **`EmergencyContactName`** | `string`（預設 `""`） | **R1 變更為選填** | — | **本次變更**：UI 移除必填提示與紅星 |
| **`EmergencyContactPhone`** | `string`（預設 `""`） | **R1 變更為選填** | — | **本次變更**：同上 |
| **`EmergencyContactRelationship`** | `string`（預設 `""`） | **R1 變更為選填** | — | **本次變更**：同上 |
| `PreferredAnimalHospitalName` | `string` | No | — | |
| `PreferredAnimalHospitalPhone` | `string` | No | — | |
| `PreferredAnimalHospitalAddress` | `string` | No | — | |
| `IsStoredValueCustomer` | `bool` | — | — | |
| `StoredValueBalance` | `decimal` | — | — | |
| `Note` | `string` | No | — | |
| `CreatedAt` | `DateTimeOffset` | — | — | |
| `UpdatedAt` | `DateTimeOffset` | — | — | |
| `Pets` | `ICollection<Pet>` | — | navigation | 1:N |

**R1 schema 是否變更**：**否**。三欄已是 `string` 預設 `""`，本來就接受空值；變更只在 UI 層（移除 `LabelHelper.RequiredText`）與 PDF 模板層（`contract.hbs` page 1、page 4 不顯示佔位字串）。

---

## 3. `Pet`（R3、R6、R7 主要影響）

| 屬性 | 型別 | EF 設定 | 備註 |
| --- | --- | --- | --- |
| `PetId` | `string` | PK, `HasMaxLength(40)` | |
| `OwnerId` | `string` | FK → `Owner.OwnerId`, `HasMaxLength(40)` | |
| `Name` | `string` | `HasMaxLength(100)`, Required | |
| `Species` | `string` | `HasMaxLength(10)`, Required | "犬" / "貓" |
| `Breed` | `string` | `HasMaxLength(100)`, Required | |
| `Gender` | `string` | `HasMaxLength(10)`, Required | "公" / "母" |
| `Age` | `string` | `HasMaxLength(40)` | 自由格式 |
| `IsNeutered` | `bool` | — | |
| **`ChipNumber`** | `string?` | `HasMaxLength(50)`, **nullable** | **R3**：空白時不再自動推論「未登記」 |
| **`UnregisteredIdMethod`** | `string?` | `HasMaxLength(100)`, **nullable** | **R3 既有欄位、本次成為 PDF page 1「若未登記」與 page 4「無」的勾選判定來源**（不新增布林） |
| `Personality` | `List<string>` | JSON 字串欄位（`HasConversion` + `ValueComparer`） | 取值範圍：`PersonalityOptions.All` |
| **`PhysicalExamination`** | `PhysicalExamination`（owned） | `OwnsOne(...)` 對應 `phys_eyes` 等 6 個 column | **R6**：新增 6 個 `*Note` 欄位 |
| **`MedicalHistory`** | `List<string>` | JSON 字串欄位 | **R7**：取值範圍 `MedicalHistoryOptions.All` 由 18 項擴充為 19 項（新增「以上皆無」） |
| `MedicalHistoryOther` | `string` | `HasMaxLength(500)` | 與 R7 之「其它」病史搭配的自由文字 |
| `Note` | `string` | `HasMaxLength(1000)` | |
| `CreatedAt` | `DateTimeOffset` | — | |
| `UpdatedAt` | `DateTimeOffset` | — | |
| `Owner` | `Owner?` | navigation | |

**R3 schema 是否變更**：**否**（`ChipNumber` / `UnregisteredIdMethod` 都已是 `string?`，仍沿用既有欄位）。
**R7 schema 是否變更**：**否**（`MedicalHistory` 是 `List<string>` 自由字串清單，只動 `MedicalHistoryOptions.All` 常數即可）。

---

## 4. `PhysicalExamination`（R6 主要影響，**唯一 schema 變更項**）

### 現況（`src/PetSalon.Core/Entities/Pet.cs` 第 31–39 行）

```csharp
public class PhysicalExamination
{
    public string Eyes { get; set; } = "正常";
    public string Ears { get; set; } = "正常";
    public string Teeth { get; set; } = "正常";
    public string Limbs { get; set; } = "正常";
    public string Skin { get; set; } = "正常";
    public string Fur { get; set; } = "正常";
}
```

EF 映射（`PetConfiguration.cs` 第 46–54 行、`GroomingRecordConfiguration.cs` 類似）：

```csharp
b.OwnsOne(p => p.PhysicalExamination, ex =>
{
    ex.Property(e => e.Eyes).HasColumnName("phys_eyes").HasMaxLength(20);
    ex.Property(e => e.Ears).HasColumnName("phys_ears").HasMaxLength(20);
    ex.Property(e => e.Teeth).HasColumnName("phys_teeth").HasMaxLength(20);
    ex.Property(e => e.Limbs).HasColumnName("phys_limbs").HasMaxLength(20);
    ex.Property(e => e.Skin).HasColumnName("phys_skin").HasMaxLength(20);
    ex.Property(e => e.Fur).HasColumnName("phys_fur").HasMaxLength(20);
});
```

### R6 / Q5 / Q7 需新增的 6 個欄位

| 新欄位 | 型別 | 對應 SQLite column | MaxLength | 備註 |
| --- | --- | --- | --- | --- |
| `EyesNote` | `string?` | `phys_eyes_note` | 30 字（Q7） | 只在 `Eyes == "異常"` 時顯示 |
| `EarsNote` | `string?` | `phys_ears_note` | 30 字 | 只在 `Ears == "異常"` 時顯示 |
| `TeethNote` | `string?` | `phys_teeth_note` | 30 字 | 只在 `Teeth == "異常"` 時顯示 |
| `LimbsNote` | `string?` | `phys_limbs_note` | 30 字 | 只在 `Limbs == "異常"` 時顯示 |
| `SkinNote` | `string?` | `phys_skin_note` | 30 字 | 只在 `Skin == "異常"` 時顯示 |
| `FurNote` | `string?` | `phys_fur_note` | 30 字 | 只在 `Fur` 包含 `"異常"` 時顯示（Q8：「打結」、「跳蚤壁蝨」分類本身不需要說明） |

### Q9：服務當下異常不同步回寫 Pet

- `Pet.PhysicalExamination` 與 `GroomingRecord.PhysicalExamination` 為**兩份獨立記錄**。
- `GroomingRecord` 建立時預設值由 `Pet.PhysicalExamination` 帶（淺拷貝），但儲存 `GroomingRecord` 時**不會回寫到 Pet**。

### EF Core Migration 規劃

- Migration 名稱建議：`AddPhysicalExaminationNotes`
- 目標：在 `pets` 與 `grooming_records` 兩張表各 add 6 個 nullable TEXT column。
- 既有資料：所有欄位 backfill 為 NULL（與「未填說明」語意一致）。
- 回滾：drop 6 個欄位（不影響其他資料）。
- 預期 SQL（SQLite）：
  ```sql
  ALTER TABLE pets ADD COLUMN phys_eyes_note TEXT NULL;
  ALTER TABLE pets ADD COLUMN phys_ears_note TEXT NULL;
  ALTER TABLE pets ADD COLUMN phys_teeth_note TEXT NULL;
  ALTER TABLE pets ADD COLUMN phys_limbs_note TEXT NULL;
  ALTER TABLE pets ADD COLUMN phys_skin_note TEXT NULL;
  ALTER TABLE pets ADD COLUMN phys_fur_note TEXT NULL;
  -- 同樣的 6 個欄位也加到 grooming_records
  ```

---

## 5. `GroomingRecord`（R6、R7 之 PDF 渲染來源）

| 屬性 | 型別 | EF 設定 | R 影響 |
| --- | --- | --- | --- |
| `GroomingRecordId` | `string` | PK | — |
| `AppointmentId` | `string` | FK → `Appointment` | — |
| `ServiceDate` | `DateOnly` | — | — |
| `ServiceTime` | `TimeOnly` | — | — |
| `Services` | `List<GroomingServiceItem>` | JSON 字串欄位 | — |
| `TotalCost`, `StoredValueDeduction`, `CashPayment` | `decimal` | — | — |
| **`PhysicalExamination`** | `PhysicalExamination`（owned） | 同 `Pet`，6 個 `phys_*` column | **R6**：PDF 以此為準（**非** `Pet.PhysicalExamination`） |
| `Personality` | `List<string>` | JSON 字串欄位 | — |
| **`MedicalHistory`** | `List<string>` | JSON 字串欄位 | **R7**：PDF 以此為準；空陣列視為「以上皆無」 |
| `MedicalHistoryOther` | `string` | — | — |
| `OwnerNotes`, `ShopNotes`, `OtherNotes` | `string` | — | — |
| `ContractPaths` | `List<ContractVersion>` | JSON 字串欄位 | R4 / R5 影響後仍為 4 頁 PDF |
| `CreatedAt`, `UpdatedAt` | `DateTimeOffset` | — | — |
| `Appointment` | `Appointment?` | navigation | — |

---

## 6. `Appointment`（本次無變更）

| 屬性 | 型別 | 備註 |
| --- | --- | --- |
| `AppointmentId` | `string` | PK |
| `OwnerId` | `string` | FK |
| `PetId` | `string` | FK |
| `Date` | `DateOnly` | |
| `Time` | `TimeOnly` | |
| `Status` | `string`（預設 `AppointmentStatus.Booked`） | |
| `CancelReason`, `Note` | `string` | |
| `CreatedAt`, `UpdatedAt` | `DateTimeOffset` | |
| `Owner`, `Pet`, `GroomingRecord` | navigation | |

---

## 7. `MedicalHistoryOptions` 常數（R7）

### 現況（`src/PetSalon.Core/Constants/MedicalHistoryOptions.cs`）

```csharp
public static readonly IReadOnlyList<string> All = new[]
{
    "心臟病", "氣喘", "氣管塌陷", "癲癇",
    "白內障", "心絲蟲", "艾利希體",
    "腸炎", "腹膜炎", "腹積水",
    "血便", "血尿", "骨折", "髖關節問題",
    "懷孕", "手術外傷未癒合",
    "傳染性疾病", "其它",
};
```
共 18 項。

### R7 變更

新增第 19 項 `"以上皆無"` 於陣列末尾，前 18 項順序與字串內容**不變**（既有 `data/*.json` 之 `MedicalHistory` 陣列 index 對齊邏輯不會被破壞）。

### Q6 補充

PDF 渲染邏輯：`MedicalHistory.Contains("以上皆無") || MedicalHistory.Count == 0` → 勾選「以上皆無」。

---

## 8. `BodyConditionOptions` 常數（R6 配套）

### 現況（`src/PetSalon.Core/Constants/BodyExaminationOptions.cs`）

```csharp
public static readonly IReadOnlyList<string> Standard = new[] { "正常", "異常" };
public static readonly IReadOnlyList<string> Fur = new[] { "正常", "打結", "跳蚤壁蝨" };
```

### R6 變更

- `Standard` **已含**「異常」，無需變更常數內容。模板需要新增「異常」勾選格的渲染（不變更資料模型）。
- `Fur` **不含**「異常」 → **需新增**：`Fur` 改為 `new[] { "正常", "異常", "打結", "跳蚤壁蝨" }`（4 項，PM Gherkin R6 與 spec 已對齊此擴充）。
- `For(part)` 方法不變。

---

## 9. 既有 JSON / 資料相容性檢查

- `data/*.json`（既有匯出資料）：
  - 缺 6 個 `*Note` 欄位 → 反序列化視為 `null`（C# `string?` 預設值）。✅ 向後相容
  - `MedicalHistory = []` 之既有資料 → 不需 migration，PDF 渲染時自動勾「以上皆無」。✅ 向後相容
  - `EmergencyContactName/Phone/Relationship = ""` 之既有資料 → 不需 migration，PDF 渲染時顯示空白。✅ 向後相容
- 既有 SQLite DB：
  - **唯一需要 migration 的**就是 R6 / Q5 的 6 個 `*Note` 欄位（在 `pets` 與 `grooming_records` 兩張表）。
