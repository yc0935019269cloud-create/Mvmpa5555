# 2D 模式資源來源

- `acp8-photo.png`：使用者於 2026-10-26 提供的實物 ACP-8 遙控器照片。以原照片顯示，互動區依照片按鍵位置配置。
- `acp8-chart-source.svg`：TOPCON 原廠 ACP-8 A／IS 型型錄第 3 頁的向量圖；網站僅顯示其中 A 型 30 個圖卡區域。未將原廠圖樣冒稱本專案原創或 CC0。型錄著作權仍屬原權利人。
- 型錄原廠網址：<https://topconhealthcare.eu/uploads/media/60cb66640dc46/en-acp-8-topcon-brochure.pdf>。本次抽取使用較早 A／IS 版：<https://surveye.sk/kniznica/MEDICAL/Topcon%20brochure%20ACP-8%20en%20lr.pdf>；新版四組圖卡型錄的版面不同，不能套用目前座標。
- `chart-data.js`：A 型編號／內容依使用者 ACP-8 說明書印刷頁 21；裁切位置對應上述舊版型錄。`tools/extract-acp8-assets.py` 從 PDF 原向量擷取，不以一般字型／emoji 替代。
- 操作依據：使用者的 `topcon-acp-8-instruction-manual.pdf`，印刷頁 9、15–17。線上相同版本可參考 <https://imemcogroup.com/wp-content/uploads/2021/05/ACP-8E_Ver2_E.pdf>。本機檔 PDF 頁數與頁碼偏移不同，請以印刷頁碼為準。
- `legacy-instruments.js`：`tools/build-practice-assets.mjs` 從本 repo 既有 `legacy/src/sim/optics.js`、`retino.js`、`ui/dom.js`、`phoropterFace.js`、`retinoCanvas.js` 抽離；儀器 SVG 改用 `docs/modes/instrument-svg.js`，依使用者新參考照片重整。
- 參考的主覺光學方向：University of Iowa, Mark E. Wilkinson, *Plus Cylinder Subjective Refraction Techniques for Technicians* (2016), <https://eyerounds.org/video/Refraction/pdfs/Std-subj-Refract-Plus-Cyl-Tech-s.pdf>。本模式採負柱面向量，JCC 保持球面等效，仍屬教學近似。

重新擷取資源（需系統 Python 與 PyMuPDF）：

```powershell
python tools/extract-acp8-assets.py work/acp8-brochure.pdf PATH_TO_USER_REMOTE_PHOTO
node tools/build-practice-assets.mjs
```

精確物理遮罩輪孔徑、完整出廠 P1/P2、投影光學亮度與波前未取得／校準。程式依圖卡有效列／字定位遮罩，不冒稱已復原未公開的實體遮罩幾何。
