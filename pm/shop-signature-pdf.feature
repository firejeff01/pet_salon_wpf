# language: zh-TW
# Source: pm/spec_customer_feedback_round2.md（R6、R7）

Feature: 保存店家簽名並套用契約 PDF
  作為店家人員
  我希望保存多組店家簽名並在產生契約時選用
  以便不用每次重複手寫，同時維持飼主紙本親簽流程

  Background:
    Given 系統使用應用程式資料目錄保存店家簽名
    And 飼主簽名與甲方簽章依規則保持空白

  Scenario: 保存系統內手寫簽名
    Given 店家人員已輸入簽名名稱「店主」
    And 已在簽名畫布完成手寫簽名
    When 店家人員保存簽名
    Then 系統應建立名稱為「店主」的簽名 profile
    And 簽名應保存為正規化 PNG

  Scenario Outline: 匯入合法簽名圖片
    Given 店家人員選擇一個合法的「<格式>」圖片
    When 店家人員以名稱「媽媽」匯入簽名
    Then 系統應建立名稱為「媽媽」的簽名 profile
    And 簽名應重新編碼為正規化 PNG

    Examples:
      | 格式 |
      | PNG  |
      | JPEG |

  Scenario: 拒絕不合法的簽名檔
    Given 店家人員選擇 SVG、HTML、毀損或超出限制的檔案
    When 店家人員嘗試匯入簽名
    Then 系統應拒絕建立簽名 profile
    And 錯誤訊息不得包含簽名 Base64

  Scenario: 設定唯一預設簽名
    Given 已有「店主」與「媽媽」兩組簽名
    And 「店主」目前為預設
    When 店家人員將「媽媽」設為預設
    Then 「媽媽」應成為唯一預設簽名
    And 「店主」不應再是預設

  Scenario: 刪除預設簽名後不自動猜選其他簽名
    Given 「媽媽」是預設簽名
    And 尚有「店主」簽名
    When 店家人員確認刪除「媽媽」
    Then 「媽媽」的 profile 與 PNG 應被刪除
    And 系統應處於沒有預設簽名的狀態
    And 不應自動將「店主」設為預設

  Scenario: 契約預覽自動使用預設簽名
    Given 「媽媽」是預設簽名
    When 店家人員開啟契約預覽
    Then 對話框應選取「媽媽」
    And 預覽的美容人員簽名與乙方簽章應顯示「媽媽」的簽名圖
    And 飼主簽名與甲方簽章應保持空白

  Scenario: 切換簽名後預覽與正式 PDF 使用同一快照
    Given 契約預覽目前選取「媽媽」
    When 店家人員切換為「店主」
    And 系統完成更新預覽
    And 店家人員產生正式 PDF
    Then 預覽與正式 PDF 都應使用同一份「店主」簽名快照
    And 簽名只應出現在美容人員簽名與乙方簽章

  Scenario: 未選店家簽名仍可產生 PDF
    Given 系統沒有預設簽名
    And 契約預覽未選取任何簽名
    When 店家人員產生正式 PDF
    Then 系統仍應成功產生 PDF
    And 四個簽名位置都應保持空白

  Scenario: 簽名檔遺失或損毀時警告但不阻擋 PDF
    Given 契約預覽選取的簽名檔已遺失、損毀或無法讀取
    When 系統更新預覽或產生正式 PDF
    Then 系統應顯示簽名無法使用的警告
    And 店家簽名位置應保持空白
    And 系統仍應允許產生 PDF

  Scenario: 備份與還原包含全部店家簽名
    Given 系統已有多組店家簽名及一組預設簽名
    When 店家人員建立備份並於乾淨環境還原
    Then 所有簽名 profile 與 PNG 都應還原
    And 預設簽名設定應保持一致
