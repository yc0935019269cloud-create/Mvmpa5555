# 接手

先讀 AGENTS.md、README.md、Documentation/DESIGN.md、SESSION_NOTES.md。UnityProject 為新遊戲，Blender 為所有自製模型來源。不要改原來的 Peglin 專案。所有醫療操作須有可見模型動作，教學範圍與近似模型限制見 DESIGN.md。

2026-10-15 操作改版：左下遙控器、精簡主觀驗光 UI、小升降桌、JCC 支桿拖曳與滾輪翻面。沿用既有 Unity／Blender 路徑，驗收證據與限制見 SESSION_NOTES.md。

2026-10-26：新增 `docs/modes/` 的 ACP-8 與 2D 儀器，原版已在 main（ee19543）。先讀 `Documentation/PRACTICE_MODES.md`、`Reports/PRACTICE_ACCEPTANCE.md` 及 SESSION_NOTES 的暫緩清單。2D 儀器主操作是拖曳，外輪上拖加負；JCC 放入與 A/P 獨立，可自動翻面問答。左側可收合 VA／MPMVA 快捷區、偷看處方、水平套筒與固定軸盤是後續 2D 專屬調整。使用者指定先不做 Risley、R、P、WMV/RMV、內建 ±.50／ADD。修改圖面後以 `node tools/build-practice-assets.mjs` 重建，跑 `node tools/verify-practice-models.mjs`（目前 604 項）；發布由 Pages workflow 控制。
