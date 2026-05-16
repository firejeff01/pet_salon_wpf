# language: zh-TW
功能: R3 晶片號碼空白時不得自動勾選「未登記 / 無」
  作為 美容師 / 店家操作人員
  我希望 寵物未填晶片號碼時，PDF 不會自動將「未登記 / 無」勾選格塗黑
  以便 系統不對未明確填寫之狀態自行推論，避免錯誤資訊外流

  背景:
    假設 系統已啟動並可讀寫本機 JSON 檔案
    並且 "data/owners.json" 中已存在飼主 "王小明"
    並且 "data/pets.json" 中已存在寵物 "小白" 屬於飼主 "王小明"
    並且 我位於寵物編輯頁或服務紀錄 / 契約產生頁

  場景: ChipNumber 與 UnregisteredIdMethod 皆為空時 PDF 兩處勾選格皆不勾選
    假設 寵物 "小白" 之 "ChipNumber" 為 null
    並且 寵物 "小白" 之 "UnregisteredIdMethod" 為 null
    當 我為該寵物產生契約 PDF
    那麼 PDF 第 2 頁（原 page 1）之「晶片號碼」儲存格應為空白
    並且 PDF 第 2 頁之「若未登記」勾選格不應勾選
    並且 PDF 第 2 頁之「身份識別方式」儲存格應為空白
    並且 PDF 第 1 頁（原 page 4）服務資料表之「無」勾選格不應勾選

  場景: ChipNumber 為空但 UnregisteredIdMethod 有值時兩處勾選格皆勾選
    假設 寵物 "小白" 之 "ChipNumber" 為 null
    並且 寵物 "小白" 之 "UnregisteredIdMethod" 為 "項圈牌號 A12"
    當 我為該寵物產生契約 PDF
    那麼 PDF 第 2 頁之「若未登記」勾選格應勾選
    並且 PDF 第 2 頁之「身份識別方式」應顯示 "項圈牌號 A12"
    並且 PDF 第 1 頁服務資料表之「無」勾選格應勾選

  場景: ChipNumber 有值時兩處勾選格皆不勾選
    假設 寵物 "小白" 之 "ChipNumber" 為 "900123456789012"
    並且 寵物 "小白" 之 "UnregisteredIdMethod" 為 null
    當 我為該寵物產生契約 PDF
    那麼 PDF 第 2 頁之「晶片號碼」應顯示 "900123456789012"
    並且 PDF 第 2 頁之「若未登記」勾選格不應勾選
    並且 PDF 第 1 頁之「無」勾選格不應勾選

  場景: ChipNumber 有值且 UnregisteredIdMethod 也有值（罕見情況）以 UnregisteredIdMethod 為勾選依據
    假設 寵物 "小白" 之 "ChipNumber" 為 "900123456789012"
    並且 寵物 "小白" 之 "UnregisteredIdMethod" 為 "舊晶片損壞，已換項圈牌號 B05"
    當 我為該寵物產生契約 PDF
    那麼 PDF 第 2 頁之「晶片號碼」應顯示 "900123456789012"
    並且 PDF 第 2 頁之「若未登記」勾選格應勾選
    並且 PDF 第 2 頁之「身份識別方式」應顯示 "舊晶片損壞，已換項圈牌號 B05"
    並且 PDF 第 1 頁之「無」勾選格應勾選

  場景: 寵物編輯頁顯示輔助文字提示無晶片填寫方式
    當 我開啟寵物編輯頁
    那麼 在「晶片號碼」欄位旁應顯示輔助文字，提示使用者若無晶片請於「身份識別方式」填寫替代識別方式
    並且 輔助文字應提供範例如「無晶片，以項圈牌號 A12 識別」或「無」

  場景: 不新增任何專用布林欄位
    假設 我檢視 "data/pets.json" 之 schema
    那麼 schema 中不應存在 "IsUnregistered" 之布林欄位
    並且 schema 應沿用既有 "ChipNumber" 與 "UnregisteredIdMethod" 兩個字串欄位

  場景大綱: 不同晶片資料組合下 PDF 兩處勾選與顯示行為
    假設 寵物 "小白" 之 "ChipNumber" 為 "<晶片>"
    並且 寵物 "小白" 之 "UnregisteredIdMethod" 為 "<識別方式>"
    當 我為該寵物產生契約 PDF
    那麼 PDF 第 2 頁「若未登記」勾選狀態應為 "<page1勾選>"
    並且 PDF 第 1 頁「無」勾選狀態應為 "<page4勾選>"

    例子:
      | 晶片            | 識別方式             | page1勾選 | page4勾選 |
      |                 |                      | 否        | 否        |
      |                 | 無                   | 是        | 是        |
      |                 | 項圈牌號 A12         | 是        | 是        |
      | 900123456789012 |                      | 否        | 否        |
      | 900123456789012 | 舊晶片損壞已換項圈   | 是        | 是        |
