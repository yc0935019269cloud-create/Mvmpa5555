# ACP-8 遙控器與 2D 綜合驗光儀工作紀錄

更新：2026-10-26（Asia/Taipei）。狀態：本機功能與瀏覽器回歸通過，尚未提交／推送／發布；實機物理一致性未標定。

## 使用者要求

在 `E:\NEWTEST\OptometryImmersiveUnity`（origin：`yc0935019269cloud-create/Mvmpa5555`）新增兩個獨立模式：

1. 依使用者照片還原 TOPCON ACP-8 實物遙控器，投影幕在遙控器上方。不可沿用原遊戲錯誤的簡化遙控器邏輯；依手冊重新做遙控器及投影機。
2. 完整可操作的 2D 綜合驗光儀，盡量複用 repo 已有的驗光儀、檢影與光學程式。開始可自訂答案或隨機雙眼數值。

新模式指定順序：雙眼檢影全流程 → VA 直接給數值 → 右眼 MSR（第一次 MPMVA → 紅綠 → JCC → 第二次 MPMVA）→ 左眼 MSR（同右眼）。此新順序僅用於新的 2D 模式。

JCC 用文字回答哪片清楚。檢影的觀察／調節／測量誤差應讓部分病例仍需在 JCC 修正；霧視後的主覺終點有時會比淨檢影值更正。不要在進入 JCC 時偷偷更改最終答案。

使用者要求隨時維護此 Markdown 與 `SESSION_NOTES.md`。

後續指定的手感：直接拖曳各旋鈕，避免滑動桿；大球面轉輪向上拖加負、向下拖加正。球面數值小窗口放在兩側大轉盤外輪緣（使用者圈圖），O／OC 與實際遮眼／視野／作答同步。這覆寫既有程式中的向上加正。此變更需以兩側實際拖曳驗證。

## 暫緩功能（使用者明確要求，非遺漏）

- 旋轉稜鏡 Risley Prism（0–20Δ）。
- R 檢影鏡片（+1.50D／+2.00D）。R 不應再被當成紅色濾片。
- P 偏光鏡片。
- WMV／RMV 白／紅 Maddox 馬氏桿。
- 內建 ±0.50 交叉圓柱鏡、ADD 檢查。

外掛 JCC ±0.25 已完成，可直接拖曳至窺孔前、翻面及收回；外觀依使用者最新實機照片重整，已通過本機拖曳驗證。

## 來源與已確認事項

- 使用者實物照片：`C:\Users\USER\AppData\Local\Temp\codex-clipboard-31be894d-058b-4a2d-9fa0-f2d2d851df73.png`。面板與手冊 A 型相符（43 個實體按鍵：30 圖卡＋13 控制；中央九點不是按鍵）。
- 使用者提供手冊：`E:\下載資料夾\topcon-acp-8-instruction-manual.pdf`，38 個 PDF 頁面，印刷頁碼 15–17 對應 PDF 17–19。
- 額外找到 TOPCON 原廠 A 型 30 圖卡型錄。原廠連結：<https://topconhealthcare.eu/uploads/media/60cb66640dc46/en-acp-8-topcon-brochure.pdf>；本次可下載鏡像：<https://surveye.sk/kniznica/MEDICAL/Topcon%20brochure%20ACP-8%20en%20lr.pdf>，暫存在 `work/acp8-brochure.pdf`。
- 手冊明確：選表解除遮罩；LIGHT 開關投影燈；RESET 回程序第 1 步；PROGRAM ▲／▼ 前進／後退程序；方向鍵移動遮罩，超出圖卡邊界切換同類圖卡；R/G 僅適用支援的圖卡；P1/P2 選程序；操作鍵可喚醒熄燈；閒置 10 分鐘自動熄燈。
- 編程：按住 RESET 開主電源 → 選 P1/P2 → ▲ 確認 → 選圖／遮罩 → ▲ 寫入一步 → RESET 結束；最多 30 步，斷電保留。手冊沒有列出完整出廠程序，不能虛構為已核對原廠預設。
- 型錄規格：open 1、horizontal 5、vertical 8、single 21、R/G 1。手冊沒有逐一公布每張圖卡適用的實體孔徑、遮罩輪幾何、燈色與光學亮度，不能宣稱此未公開部分已與實機完全相同。
- A 型遙控器包含散光放射圖與雙眼視圖卡；它們屬獨立遙控器模式的完整圖庫，2D 驗光課程仍不新增鐘面圖驗光／雙眼平衡／AA／NPA／NPC。

## 實作決定

- 原有 Unity 診間仍是 Unity，不加入另一套 JS 3D 引擎。
- 新增 `docs/modes/index.html` 模式入口、`remote.html` 與 `refraction.html`。
- 遙控器使用照片面板加準確位置的互動區；圖卡優先使用原廠型錄的原始向量圖卡內容。
- 2D 儀器複用本 repo `legacy/src/render/phoropterSVG.js`、`ui/phoropterFace.js`、`ui/retinoCanvas.js`、`sim/optics.js`、`sim/retino.js`、`ui/dom.js`。只抽離 2D 相關部分，不使用舊 Three.js 場景或舊流程。
- 產生一般 script bundle，離線以 file:// 開啟時不依賴 ES module fetch。
- 將舊 2D 中僅裝飾的頂點距離改為實際控制，補齊需要的輔助鏡／近遠 PD 等操作。
- 患者模型：固定最終答案、種子可重現的檢影測量偏移、依當前鏡片計算的主覺／JCC 回答；自然小波動不能蓋過主要光學方向。

## 已完成

- [x] 確認本機 origin 與工作分支，讀取 AGENTS／README／DESIGN／SESSION_NOTES／CLAUDE。
- [x] 檢查原有 Unity 及封存 2D 原始碼，可複用的資源已定位。
- [x] 讀取使用者 PDF 的關鍵頁並視覺核對遙控器與編程圖示。
- [x] 找到並查看原廠 A 型完整 30 張圖卡。
- [x] 建立模式選單 HTML、遙控器 HTML 及共用 CSS 的初稿。
- [x] 建立並執行 `tools/build-practice-assets.mjs`，抽出既有 2D 儀器控制與光學程式，圖面改用最新照片版 SVG。

## 實作與驗證清單

- [x] 圖卡資源與按鍵位置資料；全部 30 圖卡與 13 控制鍵已串接。
- [x] ACP-8 狀態機（電源、燈、遮罩、同類圖卡、程序、編程、儲存）。
- [x] 2D 病例設定、儀器操作、雙眼檢影與完整逐眼 MSR 流程。
- [x] JCC 文字比較、霧視、紅綠與穩定患者差異。
- [x] 紀錄單、恢復、匯出；1024×768 與 390×844 已做瀏覽器複核。
- [x] 在 Unity 網頁模板及現有 docs 首頁加入口，避免下次 build 覆蓋。
- [x] 模型檢查、瀏覽器實際互動、桌機／平板尺寸圖像檢查；未在實體 iPad 上驗證。
- [x] 執行現有 `ClinicBuild.Validate`：本次 255 項通過。未修改 Unity 模型，不以本次 2D 測試冒稱重建 Unity WebGL。
- [x] 更新 README／CLAUDE／AGENTS／SESSION_NOTES 與 `Reports/PRACTICE_ACCEPTANCE.md`。

## 注意與目前工作區

- 原本已有未追蹤 `Reports/mpfb_assets_preview.png`，勿誤收進本次變更。
- 新增 `work/` 存放手冊頁面渲染與型錄；應列為 Git 忽略，勿提交暫存資料。
- 避免讀取整份長輸出；使用 Headroom skill 的定向讀取與輸出預算。未啟動或改動代理服務。
- Python 系統版本可 `import fitz`；Codex bundled Python 沒有 fitz，但有 pypdf。系統 Python 輸出中文時先 `sys.stdout.reconfigure(encoding='utf-8')`。
- `npx --yes --package @playwright/cli playwright-cli --version` 已可執行，版本 0.1.22。
- 檔案存在不代表功能完成；本文件所有未勾選項都尚未驗收。
- 模型檢查：585 項通過，含 24 個雙眼完整病例；`Reports/practice-model-validation.json`。瀏覽器完整 13 步流程、直接旋鈕／JCC 拖曳、43 按鍵及 file:// 離線操作已通過；詳見驗收報告。未校準之物理特性與暫緩功能仍保留在本文件，並未視為完成。
