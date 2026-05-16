# 事件風暴分析：客戶反饋之契約 PDF 與資料表調整

> 來源需求：`pm/spec_contract_pdf_customer_feedback_changes.md`（R1–R8）
> 分析原則：僅針對本次客戶反饋帶來的變更範圍，不重複列出原 `pm/spec_pet_salon.md` 已存在之既有事件、命令與聚合。
> 用語對齊：`src/PetSalon.Core/Entities/`（Owner、Pet、PhysicalExamination、GroomingRecord）、`src/PetSalon.Core/Constants/`（MedicalHistoryOptions、BodyConditionOptions）、`src/PetSalon.Infrastructure/Pdf/Templates/contract.hbs`。
> 業主已於 2026-05-16 回覆所有待確認事項（Q2 / Q4 / Q5 / Q6 / Q7 / Q8 / Q9），相關變更已併入本文件，詳見 spec 第 7 節。

---

## Bounded Context 總覽

本次變更涉及既有的三個 Bounded Context：

1. **飼主管理（Owner Management）** — R1
2. **寵物管理（Pet Management）** — R3、R6（Pet 層級之 PhysicalExamination 編輯）、R7（病史選項擴充）
3. **契約產生（Contract Generation）** — R2、R3 PDF 渲染、R4 頁序、R5 簽名線、R6 PDF 渲染、R7 PDF 渲染、R8 條款替換

本次變更**不新增 Bounded Context**，亦不影響預約管理（Appointment Management）、服務紀錄管理（Service Record Management，GroomingRecord 編輯邏輯不變，僅其內 `PhysicalExamination` 與 `MedicalHistory` 子欄位依 R6 / R7 同步調整）。

---

## Bounded Context：飼主管理（Owner Management）

### Domain Events（領域事件）
- 🟠 緊急聯絡人欄位必填規則已放寬：系統設定中「緊急聯絡人」、「與飼主關係」、「緊急聯絡電話」三欄改為選填（R1）
- 🟠 飼主資料已建立（含空白緊急聯絡人）：店家人員未填寫緊急聯絡人三欄即完成 Owner 建立並儲存（R1）
- 🟠 飼主資料已更新（含空白緊急聯絡人）：店家人員將既有 Owner 之緊急聯絡人三欄清空後仍可儲存（R1）

### Commands（命令）
- 🔵 建立飼主資料（緊急聯絡人選填）：由美容師／店家操作人員觸發，輸入 Owner 必填欄位即可送出，緊急聯絡人三欄可留空（R1）
- 🔵 編輯飼主資料（緊急聯絡人選填）：由美容師／店家操作人員觸發，可清空或維持既有緊急聯絡人三欄之內容（R1）

### Aggregates（聚合）
- 🟡 飼主（Owner）：原有聚合，僅變更必填驗證規則
  - 接收命令：建立飼主資料、編輯飼主資料（緊急聯絡人三欄改為選填）
  - 產生事件：飼主資料已建立、飼主資料已更新

### Policies（政策/規則）
- 🔴 當儲存 Owner 時 → 不再對 `EmergencyContactName / EmergencyContactPhone / EmergencyContactRelationship` 三欄執行必填檢查（R1）

---

## Bounded Context：寵物管理（Pet Management）

### Domain Events（領域事件）
- 🟠 寵物晶片登記狀態已標記：店家人員於 Pet 編輯介面填寫 `UnregisteredIdMethod`（替代識別方式），系統視為使用者明確標示「無晶片／未登記」（R3）
- 🟠 寵物身體狀態異常已記錄：店家人員將 `Pet.PhysicalExamination` 之眼睛／耳朵／牙齒／四肢／皮膚／皮毛中任一項目選為「異常」並儲存（R6）
- 🟠 寵物身體異常說明已記錄：店家人員於某部位選為「異常」後，於該部位對應之 `*Note` 文字欄位輸入說明文字並儲存（R6 + Q5）
- 🟠 寵物身體異常說明已清空：店家人員將某部位從「異常」改回「正常」時，系統自動清空該部位之 `*Note` 欄位（R6 + Q5）
- 🟠 寵物病史以上皆無已標記：店家人員於 Pet 編輯介面勾選「以上皆無」並儲存 `Pet.MedicalHistory`（R7）
- 🟠 寵物病史選項清單已擴充：系統 `MedicalHistoryOptions.All` 由 18 項擴充為 19 項，新增「以上皆無」（R7）

### Commands（命令）
- 🔵 編輯寵物晶片資料：由美容師／店家操作人員觸發，輸入 `ChipNumber` 與／或 `UnregisteredIdMethod`（R3）
- 🔵 編輯寵物身體狀態：由美容師／店家操作人員觸發，於 6 個部位各擇一選擇「正常／異常」（皮毛另可加選「打結」、「跳蚤壁蝨」）（R6）
- 🔵 輸入身體異常說明：當部位為「異常」時，由美容師／店家操作人員於該部位之說明文字欄位輸入自由文字（R6 + Q5）
- 🔵 勾選病史「以上皆無」：由美容師／店家操作人員於 Pet 編輯介面勾選，UI 同步取消其他病史勾選（R7）
- 🔵 勾選具體病史：由美容師／店家操作人員勾選任一具體病史項目，UI 同步取消「以上皆無」勾選（R7）

### Aggregates（聚合）
- 🟡 寵物（Pet）：原有聚合
  - 接收命令：編輯寵物晶片資料、編輯寵物身體狀態、輸入身體異常說明、勾選病史「以上皆無」、勾選具體病史
  - 產生事件：寵物晶片登記狀態已標記、寵物身體狀態異常已記錄、寵物身體異常說明已記錄、寵物身體異常說明已清空、寵物病史以上皆無已標記
- 🟡 身體狀態檢視（PhysicalExamination，Pet 的值物件）：
  - 部位欄位：眼睛／耳朵／牙齒／四肢／皮膚（"正常" / "異常"）、皮毛（"正常" / "異常" / "打結" / "跳蚤壁蝨"）
  - **新增**異常說明欄位（Q5）：`EyesNote / EarsNote / TeethNote / LimbsNote / SkinNote / FurNote`，型別 `string?`，預設 `null`
  - 既有 `data/*.json` 向後相容：欄位缺漏視為 `null`，不需 migration

### Policies（政策/規則）
- 🔴 當勾選「以上皆無」時 → UI 自動取消所有具體病史勾選（含「其它」）（R7 互斥規則）
- 🔴 當勾選任一具體病史（含「其它」）時 → UI 自動取消「以上皆無」勾選（R7 互斥規則）
- 🔴 當 Pet 之 `ChipNumber` 為空且 `UnregisteredIdMethod` 為空時 → 系統不自行推論「無晶片」狀態（R3 不推論原則）
- 🔴 當 Pet 之 `ChipNumber` 為空且 `UnregisteredIdMethod` 有值時 → 系統視為使用者已明確標示「無晶片／未登記」（R3）
- 🔴 當某部位於 UI 被選為「異常」時 → UI 顯示該部位對應之「異常說明」短文字框，允許使用者輸入自由文字（R6 + Q5）
- 🔴 當某部位於 UI 由「異常」改為「正常」時 → UI 隱藏該部位之「異常說明」文字框，並自動清空 `*Note` 欄位（R6 + Q5）

### Read Models / 常數
- 📘 `MedicalHistoryOptions.All`（更新後）：心臟病、氣喘、氣管塌陷、癲癇、白內障、心絲蟲、艾利希體、腸炎、腹膜炎、腹積水、血便、血尿、骨折、髖關節問題、懷孕、手術外傷未癒合、傳染性疾病、其它、**以上皆無**（共 19 項，新增項目於末尾）
- 📘 `BodyConditionOptions.Standard`（不變）：正常、異常
- 📘 `BodyConditionOptions.Fur`（不變）：正常、打結、跳蚤壁蝨（皮毛在「異常」上之展現由模板渲染呈現，資料模型不變）

---

## Bounded Context：契約產生（Contract Generation）

### Domain Events（領域事件）
- 🟠 契約 PDF 頁序已調整為服務資料表優先：產出之 PDF 第 1 頁為服務資料表，第 2~4 頁依序為原 page 1 / page 2 / page 3 之內容（R4）
- 🟠 契約 PDF 之飼主姓名自動帶入已停用：page 1 飼主簽名線、page 4 甲方簽章線不再印出 `Owner.Name`（R5）
- 🟠 契約 PDF 之乙方簽章自動帶名已停用：page 4 乙方簽章線不再印出「貳寶寵物美容工坊」，保持空白由店家於紙本親簽（R5 + Q2）
- 🟠 契約 PDF 之美容人員簽名線維持空白已確認：page 1 美容人員簽名線不會自動帶入任何員工姓名（R5）
- 🟠 契約 PDF 之晶片登記勾選格判定條件已替換：原以 `ChipNumber` 空白自動勾選之邏輯改為依 `UnregisteredIdMethod` 是否有值判定（R3）
- 🟠 契約 PDF 之身體狀態異常勾選格已新增：6 個部位皆新增「□異常」勾選格並依 `GroomingRecord.PhysicalExamination` 之值勾選（R6）
- 🟠 契約 PDF 之身體異常說明文字已輸出：當某部位為「異常」且對應 `*Note` 非空時，PDF 於該部位勾選格附近顯示說明文字；正常或 Note 為空時不顯示說明區（R6 + Q5）
- 🟠 契約 PDF 之病史「以上皆無」勾選格已新增：寵物病史區塊新增「□以上皆無」勾選格（R7）
- 🟠 契約 PDF 之病史空陣列自動勾選「以上皆無」已啟用：當 `MedicalHistory` 為空陣列或包含字串 "以上皆無" 時，PDF 自動於「以上皆無」勾選格打勾（R7 + Q6）
- 🟠 契約 PDF 之短版條款文字已替換：page 4（變更後為 page 1）末尾之 ＊段同意文字與 1~8 點條款替換為客戶提供原文（R8，第 2 點依 Q4 修正為「視為飼主惡意棄養」）
- 🟠 契約 PDF 之緊急聯絡人空白儲存格已修正：未填寫時顯示空白，不顯示佔位字串（R1 PDF 端）
- 🟠 契約 PDF 已產生（含本次模板變更）：所有上述模板變更套用後，依現有 PDF 產生流程輸出檔案

### Commands（命令）
- 🔵 產生契約 PDF（紙本簽署版本）：由美容師／店家操作人員於服務紀錄／契約產生頁按下「產生契約」按鈕觸發，系統直接渲染並輸出 PDF，不再要求事前於系統內手寫簽名（R2、R4、R5）

### Aggregates（聚合）
- 🟡 契約（Contract）：原有聚合，本次僅變更模板渲染邏輯與頁序，整合資料來源不變
  - 接收命令：產生契約 PDF（紙本簽署版本）
  - 產生事件：上述「契約 PDF …」相關 11 個 Domain Events

### Policies（政策/規則）
- 🔴 當按下「產生契約」時 → 系統不再要求「飼主簽名已完成」與「店家簽名已完成」作為前置條件（R2）
- 🔴 當渲染契約 PDF 時 → 頁序依序為：服務資料表（原 page 4）→ 原 page 1 → 原 page 2 → 原 page 3（R4）
- 🔴 當渲染契約 PDF 之甲方簽章線與飼主簽名線時 → 一律輸出空白線，不帶入 `Owner.Name`（R5）
- 🔴 當渲染契約 PDF 之乙方簽章線時 → 一律輸出空白線，不帶入「貳寶寵物美容工坊」字樣（R5 + Q2）
- 🔴 當渲染契約 PDF 之美容人員簽名線時 → 一律輸出空白線，不帶入任何員工姓名（R5）
- 🔴 當渲染契約 PDF 之晶片區塊時 → 「若未登記」與 page 4「無」勾選格依 `UnregisteredIdMethod` 是否有值判定，不再依 `ChipNumber` 空白自動推論（R3）
- 🔴 當渲染契約 PDF 之身體狀態檢視區塊時 → 6 部位均同時輸出「正常」與「異常」兩個勾選格，依 `GroomingRecord.PhysicalExamination` 之值擇一勾選；皮毛另保留「打結」、「跳蚤壁蝨」兩個附加勾選格（R6）
- 🔴 當某部位於 PDF 渲染為「異常」且對應 `*Note` 非空時 → 於該部位列勾選格之後輸出說明文字；當部位為「正常」或 `*Note` 為空 / null 時 → 不輸出任何說明文字區（R6 + Q5）
- 🔴 當渲染契約 PDF 之寵物病史區塊時 → 新增「以上皆無」勾選格，渲染條件為 `GroomingRecord.MedicalHistory` 包含字串 "以上皆無" **或** `MedicalHistory` 為空陣列 `[]`（R7 + Q6）
- 🔴 當渲染契約 PDF 之緊急聯絡人儲存格時 → 空值不輸出 `null` / `未填寫` 等佔位文字，亦不輸出空括號（R1 PDF 端）
- 🔴 當渲染契約 PDF 之 page 4（變更後為 page 1）末尾條款區時 → 以客戶提供之原文（＊段＋8 點）取代既有錯亂文字，文字逐字符合，包含第 2 點「視為飼主惡意棄養」修正（Q4）、第 7 點「以 地方法院」之空格、第 8 點「其他：____________」之底線（R8）

### UI / External（受影響的 UI 與外部介面）
- 🖥️ Owner 編輯頁：緊急聯絡人三欄移除必填樣式（R1）
- 🖥️ Pet 編輯頁：
  - 晶片號碼旁加上輔助文字（R3）
  - 身體狀態 6 部位改為「正常／異常」單選控制項，皮毛保留多選格供「打結／跳蚤壁蝨」（R6）
  - **新增**異常說明文字框（Q5）：當某部位選為「異常」時動態顯示，選回「正常」時隱藏並清空對應 `*Note` 欄位
  - 寵物病史新增「以上皆無」選項與互斥邏輯（R7）
- 🖥️ GroomingRecord 填寫頁：
  - 身體狀態與寵物病史同上（R6、R7，含 Q5 異常說明文字框）
  - **移除**「飼主簽名／店家簽名」畫布、清除鈕、確認鈕（若目前 UI 存在）；產生 PDF 按鈕不再以簽名為前置條件（R2）
- 📄 外部產物（契約 PDF）：頁序、簽名線（含乙方）、勾選格、異常說明、條款文字皆依本次模板變更輸出
- 📄 模板檔案：`src/PetSalon.Infrastructure/Pdf/Templates/contract.hbs`（變更行數涵蓋第 60–435 行；含第 240 行乙方簽章去掉店名）

---

## 跨 Context 流程串接

本次變更影響的端到端流程：

### 流程一：Owner 建立 / 編輯（含緊急聯絡人選填）
緊急聯絡人欄位必填規則已放寬 → 店家人員建立或編輯 Owner（不填或清空緊急聯絡人三欄）→ 飼主資料已建立（含空白緊急聯絡人）／飼主資料已更新（含空白緊急聯絡人）

### 流程二：Pet 編輯（晶片登記 / 身體狀態 / 病史）
店家人員於 Pet 編輯介面：
- 若無晶片 → 編輯寵物晶片資料（`ChipNumber` 空、`UnregisteredIdMethod` 填值）→ 寵物晶片登記狀態已標記
- 6 部位身體狀態 → 編輯寵物身體狀態 → 寵物身體狀態異常已記錄（若擇一部位選為異常）
  - 該部位選為「異常」→ UI 顯示說明文字框 → 使用者輸入文字 → 寵物身體異常說明已記錄（Q5）
  - 該部位由「異常」改回「正常」→ UI 隱藏文字框並清空 `*Note` → 寵物身體異常說明已清空（Q5）
- 病史 → 勾選病史「以上皆無」（互斥規則自動取消其他選項）→ 寵物病史以上皆無已標記
- 或 → 勾選具體病史（互斥規則自動取消「以上皆無」）

### 流程三：產生契約 PDF（紙本簽署版本）
店家人員於服務紀錄／契約產生頁按下「產生契約」→ 系統依新模板渲染 →
- 頁序：服務資料表（原 page 4）→ 原 page 1 → 原 page 2 → 原 page 3
- 晶片區塊依 `UnregisteredIdMethod` 判定勾選
- 身體狀態 6 部位皆輸出正常／異常雙勾選格
  - 異常 + `*Note` 有值 → PDF 顯示說明文字（Q5）
  - 正常或 `*Note` 為空 / null → PDF 不顯示說明區（Q5）
- 寵物病史含「以上皆無」勾選格
  - `MedicalHistory` 包含 "以上皆無" 或 `MedicalHistory = []` → 自動勾選（Q6）
- 緊急聯絡人空白儲存格顯示空白
- 甲方簽章線／乙方簽章線／飼主簽名線／美容人員簽名線皆為空白（Q2 已確認乙方亦空白）
- page 1（原 page 4）末尾以客戶提供之 ＊段＋8 點原文取代（第 2 點 "視為飼主惡意棄養" 為 Q4 修正版本）
→ 契約 PDF 已產生 → 既有 `ContractVersion` 保存與檢視器開啟流程不變

---

## 本次未涵蓋（已知排除）

依 spec 第 6 節「不在本次範圍」，以下項目**不**列入本次事件風暴：

- 線上電子簽章法律流程（不引入新 Aggregate / Policy）
- `Owner / Pet / GroomingRecord / Appointment` 主要 Entity schema 變更（資料模型僅 `PhysicalExamination` 新增 6 個 `*Note` 欄位以支援 Q5；其餘維持不變）
- PDF 檔名規則、版本記錄規則、備份規則之變更（`ContractVersion` 行為不變）
- page 2 ~ page 3 之既有法律條款內文（除 R8 page 4 末尾短版條款外）

## 業主已確認事項（2026-05-16 已併入本次範圍）

下列議題由業主於 2026-05-16 回覆後列入本次範圍，已併入上方事件 / 政策 / 聚合：

- **Q2 乙方簽章改空白**（業主確認 (B)）：`contract.hbs` 第 240 行去掉「貳寶寵物美容工坊」字樣，乙方簽章線保持空白；對應 Policy「當渲染契約 PDF 之乙方簽章線時 → 一律輸出空白線」。
- **Q4 條款第 2 點修正**：將「為飼主惡意養」修正為「視為飼主惡意棄養」（補上「視」「棄」二字）；其餘字詞、標點、空格皆維持原文。
- **Q5 異常說明文字欄位**（業主確認 (B)，最大變更）：`PhysicalExamination` 新增 6 個 `*Note : string?`，UI 於異常時動態顯示文字框、正常時隱藏並清空；PDF 異常時顯示 Note 文字、正常時不顯示說明區；既有 `data/*.json` 缺欄位視為 null 向後相容。
- **Q6 病史空陣列視為「以上皆無」**（業主確認 (B)）：`MedicalHistory = []` 與 `MedicalHistory = ["以上皆無"]` 兩種輸入於 PDF 上皆勾選「以上皆無」勾選格；既有資料不需 migration。R3 晶片仍維持「不自行推論」原則（兩者語意不同：病史空＝明確無；晶片空＝可能未登記或拒答）。
- **Q7 異常說明字數上限 30 字**（業主確認）：`*Note` 欄位 UI `MaxLength=30`；服務端寫入超過 30 字截斷至前 30 字；PDF 渲染同步截斷、不自動折行。
- **Q8 皮毛 Note 僅在含「異常」時顯示**（業主確認）：`FurNote` 文字框只在 `Fur` 包含 `"異常"` 時動態顯示；含「打結」/「跳蚤壁蝨」但不含「異常」時不顯示 Note 框、PDF 不輸出 Note 文字。對應 Policy：「當 `Fur` 從含『異常』改為不含『異常』時 → 自動清空 `FurNote`」。
- **Q9 服務紀錄異常不同步回 Pet**（業主確認）：`GroomingRecord.PhysicalExamination` 與 `Pet.PhysicalExamination` 為兩份獨立記錄；服務當下填的異常與 Note 不自動寫回 Pet 主檔；新建 GroomingRecord 時預設值從 Pet 主檔帶，由美容師重新評估當下狀態。
