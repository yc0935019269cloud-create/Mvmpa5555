# 視光室協作規則

- 主要新專案 `UnityProject/`，模型原始檔 `Blender/`，網站發布根目錄 `docs/`。
- Unity 6000.6.5f1，內建管線，Blender 5.2。不要加入 URP 或另一套 JS 3D 引擎替代 Unity。
- 請先讀 README.md、Documentation/DESIGN.md、SESSION_NOTES.md；每次重大變更更新交接。
- 只在這個 repo 工作，勿更動 PeglinBrowserLab、Steam 或其他刷題專案。
- 醫學空間關係優先：OD 是患者右眼；朝患者看時在畫面左。保持六公尺視標、負柱面軸與 JCC 翻轉方向。
- 每個臨床動作都有實際模型動作，不能只把按鈕勾成完成。
- 不宣稱專業臨床驗證；模糊與回答屬近似模型。期末範圍不冒稱完成。
- 使用者明確限定只有期中範圍，不包含鐘面圖。散光只做 JCC；不得加入雙眼平衡、AA、NPA、NPC。
- 改動光學／任務必須跑 ClinicBuild.Validate；發布必須實際 WebGL build 與瀏覽器驗證。
- 不提交 Library/Temp/Logs/UserSettings、金鑰、其他專案資產。

- 遙控器已移到左下角常駐 UI；主觀驗光不要重新塞回所有調鏡按鈕。工具集中於 LiftDeck 升降桌。JCC 中央點 A/P、滾輪翻面、外支桿拖曳。細節見 README 與 SESSION_NOTES。

## 2026-10-26 獨立 2D 模式

- 使用者另外授權 `docs/modes/` 的 ACP-8 遙控器及 2D 儀器，允許複用本 repo 既有 2D 資源；不替換 Unity 診間。先讀 `Documentation/PRACTICE_MODES.md` 及 SESSION_NOTES 最新的暫緩清單。
- 新 2D 順序明確指定為雙眼檢影／WD／直接 VA，再完整 OD MSR、完整 OS MSR；不要改回 Unity 原有的雙眼分項順序。
- 儀器 SVG 來源 `docs/modes/instrument-svg.js`，bundle 用 `node tools/build-practice-assets.mjs` 重建。保留直接拖曳、大球面輪上拖加負、兩側外輪緣讀數窗、O／OC 真遮蓋、JCC 支桿拖到窺孔與滾輪翻面。不要重新改回滑動桿。
- 暫緩：Risley、R、P、WMV/RMV、內建 ±.50／ADD。外掛 JCC ±.25 保留。暫緩項目不可做成假按鈕。
- ACP-8 完整 A 型圖卡可包含散光放射圖與雙眼視圖卡；這是設備圖庫，不代表新增鐘面圖驗光或期末課程。
- 重大 2D 變更跑 `tools/verify-practice-models.mjs` 及相應瀏覽器操作；維護 session note，區分本機驗證與發布。遮罩幾何／出廠程序／投影亮度未校準，不宣稱實機完全一致。
