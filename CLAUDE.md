# 接手

先讀 AGENTS.md、README.md、Documentation/DESIGN.md、SESSION_NOTES.md。UnityProject 為新遊戲，Blender 為所有自製模型來源。不要改原來的 Peglin 專案。所有醫療操作須有可見模型動作，教學範圍與近似模型限制見 DESIGN.md。

2026-10-15 操作改版：左下遙控器、精簡主觀驗光 UI、小升降桌、JCC 支桿拖曳與滾輪翻面。沿用既有 Unity／Blender 路徑，驗收證據與限制見 SESSION_NOTES.md。

2026-10-26：新增 `docs/modes/` 的 ACP-8 與 2D 儀器。先讀 `Documentation/PRACTICE_MODES.md`、`Reports/PRACTICE_ACCEPTANCE.md` 及 SESSION_NOTES 的暫緩清單。2D 儀器主操作是拖曳，外輪上拖加負，JCC 可移到窺孔前；不要改回滑動桿。使用者指定先不做 Risley、R、P、WMV/RMV、內建 ±.50／ADD。修改圖面後以 `node tools/build-practice-assets.mjs` 重建，跑 `node tools/verify-practice-models.mjs`；新模式未推送遠端。
