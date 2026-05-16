# language: zh-TW
功能: R6 身體狀態檢視「異常」狀態必須寫入 PDF（含 Q5 異常說明）
  作為 美容師
  我希望 將寵物身體狀態填為「異常」時 PDF 能如實顯示，並可加註異常說明文字
  以便 完整呈現該次服務之身體狀態檢視結果

  背景:
    假設 系統已啟動並可讀寫本機 JSON 檔案
    並且 "data/owners.json" 中已存在飼主 "王小明"
    並且 "data/pets.json" 中已存在寵物 "小白" 屬於飼主 "王小明"
    並且 "data/service-records.json" 中已存在對應之服務紀錄
    並且 PDF 顯示之身體狀態以 "GroomingRecord.PhysicalExamination" 為準
    並且 我位於寵物編輯頁或服務紀錄 / 契約產生頁

  場景: 5 個部位於 PDF 同時呈現「正常」與「異常」兩個勾選格
    當 我按下「產生契約」按鈕並完成 PDF 產生
    那麼 PDF 第 1 頁「1.眼睛」列應同時顯示「□正常」與「□異常」兩個勾選格
    並且 PDF 第 1 頁「2.耳朵」列應同時顯示「□正常」與「□異常」兩個勾選格
    並且 PDF 第 1 頁「3.牙齒」列應同時顯示「□正常」與「□異常」兩個勾選格
    並且 PDF 第 1 頁「4.四肢」列應同時顯示「□正常」與「□異常」兩個勾選格
    並且 PDF 第 1 頁「5.皮膚」列應同時顯示「□正常」與「□異常」兩個勾選格

  場景: 6.皮毛列於 PDF 呈現「正常 / 異常 / 打結 / 跳蚤壁蝨」四個勾選格
    當 我按下「產生契約」按鈕並完成 PDF 產生
    那麼 PDF 第 1 頁「6.皮毛」列應同時顯示「□正常」、「□異常」、「□打結」、「□跳蚤壁蝨」四個勾選格

  場景: 部位為「正常」時僅勾選正常
    假設 "GroomingRecord.PhysicalExamination.Eyes" 為 "正常"
    當 我為該服務紀錄產生契約 PDF
    那麼 PDF 第 1 頁「1.眼睛」列之「正常」勾選格應勾選
    並且 PDF 第 1 頁「1.眼睛」列之「異常」勾選格不應勾選

  場景: 部位為「異常」時僅勾選異常
    假設 "GroomingRecord.PhysicalExamination.Eyes" 為 "異常"
    當 我為該服務紀錄產生契約 PDF
    那麼 PDF 第 1 頁「1.眼睛」列之「正常」勾選格不應勾選
    並且 PDF 第 1 頁「1.眼睛」列之「異常」勾選格應勾選

  場景: 部位欄位為空字串 / null 時兩格皆不勾選（理論不應發生之邊界）
    假設 "GroomingRecord.PhysicalExamination.Eyes" 為 null
    當 我為該服務紀錄產生契約 PDF
    那麼 PDF 第 1 頁「1.眼睛」列之「正常」與「異常」勾選格皆不應勾選

  場景: 皮毛同時為「異常 + 打結」之多選情境
    假設 "GroomingRecord.PhysicalExamination.Fur" 包含 "異常"
    並且 "GroomingRecord.PhysicalExamination.Fur" 包含 "打結"
    當 我為該服務紀錄產生契約 PDF
    那麼 PDF 第 1 頁「6.皮毛」列之「異常」勾選格應勾選
    並且 PDF 第 1 頁「6.皮毛」列之「打結」勾選格應勾選
    並且 PDF 第 1 頁「6.皮毛」列之「正常」勾選格不應勾選

  場景: UI 提供「正常 / 異常」單選控制項並與 PDF 顯示一致
    當 我於寵物編輯頁將「眼睛」選為「異常」並儲存
    那麼 對應 "PhysicalExamination.Eyes" 應寫入 "異常"
    並且 後續產出之 PDF 第 1 頁「1.眼睛」列應勾選「異常」

  # ===== Q5：異常說明文字欄位 =====

  場景: 部位選為「異常」時 UI 動態顯示異常說明文字框
    當 我於寵物編輯頁將「眼睛」選為「異常」
    那麼 系統應於「眼睛」項目下方動態顯示一個「異常說明」文字框
    並且 該文字框允許輸入自由文字

  場景: 將部位從「異常」改回「正常」時 UI 隱藏並自動清空說明
    假設 我已將「眼睛」選為「異常」並輸入說明文字 "結膜發紅"
    當 我將「眼睛」改選為「正常」
    那麼 系統應隱藏「眼睛」之「異常說明」文字框
    並且 "PhysicalExamination.EyesNote" 應被清空為 null 或空字串

  場景: 異常 + Note 有值時 PDF 顯示說明文字
    假設 "GroomingRecord.PhysicalExamination.Eyes" 為 "異常"
    並且 "GroomingRecord.PhysicalExamination.EyesNote" 為 "結膜發紅，建議就醫"
    當 我為該服務紀錄產生契約 PDF
    那麼 PDF 第 1 頁「1.眼睛」列之「異常」勾選格應勾選
    並且 PDF 第 1 頁「1.眼睛」列之勾選格旁或下方應顯示說明文字 "結膜發紅，建議就醫"

  場景: 異常但 Note 為空時 PDF 不顯示說明區
    假設 "GroomingRecord.PhysicalExamination.Eyes" 為 "異常"
    並且 "GroomingRecord.PhysicalExamination.EyesNote" 為 null
    當 我為該服務紀錄產生契約 PDF
    那麼 PDF 第 1 頁「1.眼睛」列之「異常」勾選格應勾選
    並且 PDF 第 1 頁「1.眼睛」列不應顯示任何說明文字區

  場景: 正常但 Note 殘留資料時 PDF 不顯示說明區
    假設 "GroomingRecord.PhysicalExamination.Eyes" 為 "正常"
    並且 "GroomingRecord.PhysicalExamination.EyesNote" 殘留為 "舊資料"
    當 我為該服務紀錄產生契約 PDF
    那麼 PDF 第 1 頁「1.眼睛」列之「正常」勾選格應勾選
    並且 PDF 第 1 頁「1.眼睛」列不應顯示任何說明文字區

  場景: PhysicalExamination 新增 6 個 *Note 欄位
    當 我檢視 "PhysicalExamination" 之 entity schema
    那麼 schema 應包含 "EyesNote : string?"
    並且 schema 應包含 "EarsNote : string?"
    並且 schema 應包含 "TeethNote : string?"
    並且 schema 應包含 "LimbsNote : string?"
    並且 schema 應包含 "SkinNote : string?"
    並且 schema 應包含 "FurNote : string?"

  場景: 既有 data/*.json 不含 *Note 欄位時系統向後相容
    假設 既有 "data/pets.json" 中某筆寵物之 "PhysicalExamination" 沒有 "EyesNote" 欄位
    當 系統載入該筆寵物
    那麼 "EyesNote" 應視為 null
    並且 後續 PDF 渲染不應顯示「眼睛」之說明文字區

  場景大綱: 6 個部位之異常 + Note 行為一致
    假設 "GroomingRecord.PhysicalExamination.<部位>" 為 "<狀態>"
    並且 "GroomingRecord.PhysicalExamination.<部位>Note" 為 "<說明>"
    當 我為該服務紀錄產生契約 PDF
    那麼 PDF 第 1 頁「<列名>」列之「<勾選格>」應勾選
    並且 PDF 第 1 頁「<列名>」列之說明文字應顯示為 "<PDF說明>"

    例子:
      | 部位  | 列名    | 狀態 | 說明           | 勾選格 | PDF說明        |
      | Eyes  | 1.眼睛  | 正常 |                | 正常   |                |
      | Eyes  | 1.眼睛  | 異常 | 結膜發紅       | 異常   | 結膜發紅       |
      | Ears  | 2.耳朵  | 異常 | 右耳有異味     | 異常   | 右耳有異味     |
      | Teeth | 3.牙齒  | 異常 | 牙結石嚴重     | 異常   | 牙結石嚴重     |
      | Limbs | 4.四肢  | 異常 | 左後腳跛行     | 異常   | 左後腳跛行     |
      | Skin  | 5.皮膚  | 異常 | 腹部紅疹       | 異常   | 腹部紅疹       |
      | Fur   | 6.皮毛  | 異常 | 大面積脫毛     | 異常   | 大面積脫毛     |

  # ===== Q7：異常說明文字最大長度 30 字 =====

  場景: UI 文字框限制最多輸入 30 字元
    當 我於寵物編輯頁將「眼睛」選為「異常」並見到「異常說明」文字框
    那麼 該文字框之 MaxLength 屬性應為 30
    並且 使用者無法輸入超過 30 字元之內容

  場景: 服務端寫入超過 30 字之說明時截斷至前 30 字
    假設 系統收到一筆寫入請求，"PhysicalExamination.EyesNote" 為長度 45 字之字串 "結膜嚴重發紅伴隨黃色分泌物建議盡速就醫進行裂隙燈檢查與細菌培養"
    當 系統儲存該筆資料
    那麼 寫入後 "PhysicalExamination.EyesNote" 應為前 30 字 "結膜嚴重發紅伴隨黃色分泌物建議盡速就醫進行裂隙燈檢查與細"
    並且 不應顯示阻擋錯誤訊息

  場景: PDF 渲染超過 30 字之 Note 時顯示前 30 字
    假設 "GroomingRecord.PhysicalExamination.EyesNote" 為 45 字之字串
    當 我為該服務紀錄產生契約 PDF
    那麼 PDF 第 1 頁「1.眼睛」列之說明文字最多顯示 30 字
    並且 不應自動折行至第 2 行

  # ===== Q8：皮毛 Note 只在含「異常」時顯示 =====

  場景: 皮毛只勾「打結」未勾「異常」時 UI 不顯示說明框
    當 我於寵物編輯頁勾選「皮毛」之「打結」但未勾「異常」
    那麼 系統不應顯示「皮毛」之「異常說明」文字框
    並且 "PhysicalExamination.FurNote" 應為 null 或空字串

  場景: 皮毛只勾「跳蚤壁蝨」未勾「異常」時 UI 不顯示說明框
    當 我於寵物編輯頁勾選「皮毛」之「跳蚤壁蝨」但未勾「異常」
    那麼 系統不應顯示「皮毛」之「異常說明」文字框
    並且 "PhysicalExamination.FurNote" 應為 null 或空字串

  場景: 皮毛同時勾「異常 + 打結」時 UI 顯示說明框
    當 我於寵物編輯頁勾選「皮毛」之「異常」與「打結」
    那麼 系統應顯示「皮毛」之「異常說明」文字框
    並且 我可輸入文字至 "PhysicalExamination.FurNote"

  場景: 皮毛取消「異常」勾選但保留「打結」時 FurNote 自動清空
    假設 我已勾選「皮毛」之「異常」與「打結」並輸入 "大面積脫毛"
    當 我取消「皮毛」之「異常」勾選但保留「打結」
    那麼 系統應隱藏「皮毛」之「異常說明」文字框
    並且 "PhysicalExamination.FurNote" 應被清空為 null 或空字串

  場景: 皮毛不含「異常」但 FurNote 殘留值時 PDF 不輸出說明文字
    假設 "GroomingRecord.PhysicalExamination.Fur" 包含 "打結" 但不含 "異常"
    並且 "GroomingRecord.PhysicalExamination.FurNote" 殘留為 "舊資料"
    當 我為該服務紀錄產生契約 PDF
    那麼 PDF 第 1 頁「6.皮毛」列之「打結」勾選格應勾選
    並且 PDF 第 1 頁「6.皮毛」列不應顯示任何說明文字

  場景: 皮毛含「異常」且 FurNote 有值時 PDF 輸出說明文字
    假設 "GroomingRecord.PhysicalExamination.Fur" 包含 "異常"
    並且 "GroomingRecord.PhysicalExamination.FurNote" 為 "大面積脫毛"
    當 我為該服務紀錄產生契約 PDF
    那麼 PDF 第 1 頁「6.皮毛」列之「異常」勾選格應勾選
    並且 PDF 第 1 頁「6.皮毛」列應顯示說明文字 "大面積脫毛"

  # ===== Q9：服務紀錄之異常不同步回寫 Pet =====

  場景: 服務紀錄填寫異常 + Note 不同步回寫 Pet
    假設 "Pet.PhysicalExamination.Eyes" 為 "正常"
    並且 "Pet.PhysicalExamination.EyesNote" 為 null
    當 美容師於當次 GroomingRecord 將「眼睛」填為 "異常" 並輸入 "結膜發紅"
    並且 我儲存該 GroomingRecord
    那麼 "GroomingRecord.PhysicalExamination.Eyes" 應為 "異常"
    並且 "GroomingRecord.PhysicalExamination.EyesNote" 應為 "結膜發紅"
    並且 "Pet.PhysicalExamination.Eyes" 應仍為 "正常"
    並且 "Pet.PhysicalExamination.EyesNote" 應仍為 null

  場景: 新建 GroomingRecord 之 PhysicalExamination 預設值來自 Pet
    假設 "Pet.PhysicalExamination.Eyes" 為 "正常"
    當 我為該寵物建立新一筆 GroomingRecord
    那麼 新 GroomingRecord 之 "PhysicalExamination.Eyes" 預設應為 "正常"
    並且 美容師可於當次服務獨立調整為 "異常" 而不影響 Pet 主檔
