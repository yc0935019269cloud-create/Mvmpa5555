# 視光室驗收紀錄 · 2026-10-15

## 原始資料與範圍

- 使用者指定 Mvmpa5555 原始 commit 09c5a5c73307141908494f7d99ff1aa54ee14938；已保留 legacy/ 與原版分支。
- 僅期中，不含鐘面圖及期末項目。JCC 依原教材含 APA／PPAP 有序比較。
- 指定 Claude artifact 在 Chrome 顯示正確標題但內文 Loading，未宣稱讀取其全文。教材內容取自原專案與醫學參考文件。

## 已完成的自動檢查

- `Reports/clinical-validation.txt`：188 項通過，含六病例完整期中狀態流程、功率向量、子午線屈光度、兩方向檢影中和、工作距離、紅綠方向、PH、柱面球面等效補償、JCC APA／PPAP 順序、第二次 MPMVA、紀錄失效與考試評分。
- `Reports/scene-geometry.txt`：Blender 公尺座標、左右眼、人物與儀器尺度；另有6公尺視標及12mm後光學面到瞳孔的算式檢查。這不是光學設備校準認證。
- Unity 6000.6.5f1：編譯與 WebGL 建置成功、建置錯誤0。最終 build report 為 `Reports/webgl-build.txt`。
- `node tools/verify-site.mjs`：實際 wasm magic、gzip解壓、data/framework/loader存在、模板展開、.nojekyll、臨床測試及建置報告通過。
- 發布 payload 的 SHA256 與檔案大小在 `Reports/webgl-files.json`。WebGL 檔案使用內容 hash 命名，避免更新時混用瀏覽器舊快取。

## 瀏覽器檢查

使用 Chrome、127.0.0.1:9035。測試版 `?qa=1` 顯示驗收控制及引擎即時狀態，普通網址不會顯示這些控制。

- 實際點擊 Unity 選單的「進入診間」成功。
- 實際鍵盤 W 移動（z從0.30增加），F 走近後坐近（camera z=3.78）、H 收合工具列成功。
- 修正後實際看見患者眼睛透過模型鏡孔、檢影窺孔內的橘紅反射、手部靠近旋鈕、器械讀數與鏡片連動。
- 建置4曾以相同公用操作方法完整跑完雙眼期中流程，`completed=true / missing=0 / PASS_FULL_MIDTERM_FLOW`，94項動作紀錄，之後沒有新增 console errors。
- 最終建置的完整流程、儲存恢復與公開站台結果，見下方最終發布段落及 SESSION_NOTES.md。

## 誠實界線

- 風格化成人、分件剛性模型與程式動畫，不是照片擬真或真人動作捕捉。手臂不是完整物理手部追蹤。
- VA、PH、調節、檢影亮度與問答是簡化教學模型；不代表真實患者反應，不用作處方。
- 鍵盤滑鼠為本次支援目標，未提供完整手機觸控操作，也未驗收 iOS Safari／Android 實機。
- 中文語音取決於瀏覽器安裝的語音，字幕可獨立使用。
- 有像素取整與風格化模型誤差；不是全部臨床檢查設備的數位孿生。

## 最終發布

- 最終 build8 實際完整期中回歸通過，包含新增的第二次霧視：`stage=16 / eye=1 / missing=0 / completed=true / PASS_FULL_MIDTERM_FLOW`，證據 `final-runtime-state.json` 與 `final-midterm-pass.jpg`。動畫記錄保留最近100項，並非只執行100個動作。
- 此次最終載入之後無新增 console errors。
- 重新整理後實際點擊繼續，恢復 stage16 / completed=true 與 OS -1.50/-0.75×090；Tab病歷的雙眼紀錄均保留，見 restored-runtime-state.json / restored-records.jpg。病歷的返回選單也已實際點擊驗證。
- Git commit、Actions 與 Pages 上線證據接著補記。

## 2026-10-15 下午重建（驗光儀／人物／PD／消毒／光影）

- `ClinicBuild.Validate` 188 項通過；Unity 6000.6.5f1 WebGL 建置成功、Errors=0；`node tools/verify-site.mjs` 通過。
- 本機 Chrome 內建瀏覽器 `?qa=1` 完整自動流程：消毒動畫→PD 對應邊緣法（記錄 64 mm）→9 項準備→雙眼檢影、WD、VA/PH、霧視 MPMVA、紅綠、JCC APA/PPAP、第二次 MPMVA，最終 `completed=true / missing=0`。
- 以截圖人工檢查：驗光儀正面朝檢查者、背面面罩/額托朝受測者；PD 尺在鼻樑前、刻度可讀；消毒動畫雙手持噴瓶與棉布、擦額托與面罩；檢影窺孔顯示瞳孔反射。
- 限制：人物仍為風格化程式建模，非照片擬真；動作為程式動畫非動作捕捉。尚未推送與公開站驗收。

## 2026-10-16 MPFB 人物替換

- 病人與第一人稱雙手改用 MakeHuman CC0 素材（MPFB 2.0.18 產生，工具本身不入庫），眼睛保留自製程序眼。
- ClinicBuild.Validate 249 項 PASS；Unity 6000.6.5f1 WebGL 建置 Succeeded、Errors 0；`node tools/verify-site.mjs` 通過。
- 本機瀏覽器 `?qa=1`：完整期中流程 PASS_FULL_MIDTERM_FLOW、missing=0、PD 64 mm；工作距離頂部選單可選並更新 D 值。

## 2026-10-16 JCC 四方向／紅綠／隨機病患

- Validate 254 項 PASS（新增：紅綠終點為第一次綠色、四方向 P 只在隱藏負柱軸選紅點、隨機病例 400 組皆在範圍內）。
- WebGL Succeeded、Errors 0；verify-site 通過；瀏覽器 ?qa=1 完整流程 PASS_FULL_MIDTERM_FLOW、missing=0。
