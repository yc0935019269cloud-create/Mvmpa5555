# 2026-10-26 獨立模式驗收

範圍：本機新增 ACP-8 遙控器／投影機、2D 綜合驗光儀。未推送遠端，未重新建置 Unity WebGL。本次未聲稱專業臨床驗證或真實患者預測。

## 已實際執行

- `node tools/build-practice-assets.mjs`：產生共用光學／儀器 bundle。
- `node tools/verify-practice-models.mjs`：**585 checks passed**。含 30 圖卡、遮罩跨圖、程序／編程／30 步限制／儲存、24 個完整雙眼病例、主子午線、WD 防重扣、JCC 軸與度數方向、最大正終點、答案不變、恢復及頂點距離。
- Unity `ClinicBuild.Validate`：**255 checks passed**。未修改原 Unity 光學程式，保留既有 WebGL payload。
- Playwright 真實 Chromium／滑鼠操作：雙眼檢影、工作距離、直接 VA、OD 全部 MSR、OS 全部 MSR，13 步完成。OD 最終 −2.00 / −1.00 ×180、OS −1.50 / −0.75 ×090 與本次輸入相同。
- JCC 文字由「第二片比較清楚」調到「兩片差不多清楚」，不是固定按步驟給答案。病例答案在進入 JCC 前後不變。
- 真實拖曳：OD／OS 外輪向上各 0→−0.50D，OD 向下 −0.50→0；額托 12→9mm；O→OC 遮眼、OC→O 開放；JCC 支桿拖到窺孔、滾輪翻面到 2、拖開回 off；檢影圓形套筒拖曳至 90°。
- 直接拖曳的機器可讀結果：`Reports/practice-direct-input-validation.json`。主覺階段檢影光帶已停止，JCC 在窺孔前不會被檢影 Canvas 蓋住；額托另有 12mm 側視刻度示意。
- 瀏覽器逐一點 30 個照片圖卡鍵，43 個實體互動鍵存在；遮罩跨到下一張同類 E 表；實體鍵編寫 P1／RESET 回第一步／重載保留。
- 保存／重新載入恢復完成進度，紀錄文字檔實際下載成功。
- 1440×1100 桌機、1024×768、390×844 版面檢查；整頁無水平溢出。未在實體 iPad 上執行。
- `file://` 開啟 ACP-8 並選圖、開啟 2D 模式並實際調鏡，皆成功。兩個模式各次驗證的 pageerror 為 0。

## 目前限制與使用者指定暫緩

- 按要求暫緩：旋轉稜鏡、R 檢影輔助鏡、P 偏光、WMV／RMV、內建 ±0.50／ADD。這些不以假選項列在 2D 模式。外掛 ±0.25 JCC 已做。
- 新儀器是依照片配置重整的互動 SVG，沒有宣稱照片逐像素相同的三維反射／材質。
- ACP-8 保留實物照片與原廠圖卡，但精確的遮罩輪孔徑、完整出廠 P1/P2、投影照度／對比／視角沒有實機標定。遮罩依有效列／字定位；不能將本次驗收稱為與實機所有物理特性完全相同。
- P1／P2 初始空白，需要自行編寫；手冊的示範不能當作完整出廠程序。
- 隨機患者／檢影偏移是可重現的教學近似，不是研究資料建立的臨床誤差分布。

## 重現瀏覽器檢查

本機 server：`python -m http.server 9046 --bind 127.0.0.1 --directory docs`。

Playwright CLI 測試腳本保存在 `tools/qa-practice-browser/`。可用 `npx --yes --package @playwright/cli playwright-cli -s=optometry26 run-code --filename=SCRIPT_PATH` 執行。先開啟 `/modes/refraction.html`；remote 腳本會自行導航。測試只操作本機存檔，會覆寫該測試瀏覽器中的測試病例／程序。
