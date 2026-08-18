# language: zh-TW
# Source: pm/spec_customer_feedback_round2.md（R3）

Feature: 安全刪除誤建飼主資料
  作為店家人員
  我希望刪除沒有服務歷史的誤建飼主
  以便清除重複資料，同時保留正式預約與美容紀錄

  Background:
    Given 店家人員已開啟「飼主管理」頁
    And 已選取一筆飼主資料

  Scenario: 取消刪除飼主
    Given 刪除確認內容顯示飼主姓名、寵物數量與不可復原提示
    When 店家人員選擇取消
    Then 飼主與所有寵物資料都應保留

  Scenario: 刪除沒有歷史的飼主與寵物
    Given 選取的飼主有兩隻寵物
    And 該飼主與其寵物都沒有預約或美容紀錄
    When 店家人員確認刪除
    Then 系統應在單一交易中刪除兩隻寵物與該飼主
    And 飼主列表不應再顯示該飼主

  Scenario Outline: 有歷史紀錄時禁止永久刪除
    Given 選取的飼主或其寵物已有「<歷史類型>」
    When 店家人員確認刪除
    Then 系統應阻擋永久刪除
    And 應說明需保留歷史紀錄
    And 飼主、寵物與歷史資料都應保持不變

    Examples:
      | 歷史類型 |
      | 預約     |
      | 美容紀錄 |

  Scenario: 刪除過程失敗時全部回滾
    Given 選取的飼主與寵物符合刪除條件
    And 刪除寵物後、刪除飼主前發生儲存錯誤
    When 系統處理刪除
    Then 飼主與所有寵物資料都應保持存在
    And 系統應顯示刪除失敗訊息
