# 工作紀錄

## 2026-10-15 重製開始

- 使用者授權新建資料夾、用 Unity 與 Blender 重製第一人稱驗光遊戲，完成後替換 Mvmpa5555 並啟用 Pages。
- 新資料夾 E:\NEWTEST\OptometryImmersiveUnity，clone 原倉庫 commit 09c5a5c73307141908494f7d99ff1aa54ee14938；git push --dry-run 通過。
- 本機 Unity 6000.6.5f1 含 WebGLSupport，Blender 5.2。設計與動作對應表在 Documentation/DESIGN.md。
- Claude artifact 經 web 與 Chrome 開啟，Chrome 顯示正確標題「自覺式驗光｜完整流程整理」但內文持續 Loading。先依原倉庫的完整流程實作，勿宣稱看過 artifact 全文。
- 使用者補充：僅期中範圍、不包含鐘面圖。已同步 DESIGN.md 與 AGENTS.md，散光只採 JCC。
- 已建立 13 組 Blender 根模型，來源在 RefractionRoom.blend；模型含真實鏡孔布林切洞。FBX nested empty bake 會錯置子節點，改同時匯出 *.clinicmesh.json（Blender 頂點、材質、公尺座標、父子階層）給 Unity 生成 Mesh assets。FBX 保留作標準交換來源，但遊戲使用精確自製 mesh interchange。
- Reports/scene-geometry.txt 驗證患者眼睛 y=1.445/z=4.475、綜合驗光儀 z=4.23、視標 z=-1.48（顯示面 -1.465）；左右眼 x=-/+0.032，人物/模型尺度正常。
- 光學與六病例流程含 APA／PPAP 有序比較的 178 項檢查通過。Unity 生成場景編譯通過。
- 已修正 WebGL always-included shaders 選到 Editor-only GUI/Text Shader 的建置錯誤；目前建置3進入 IL2CPP/wasm 連結。
- 原頁面已移入 legacy/，另建立 archive/pre-unity-20261015 分支保留原版。發布 workflow 改為檢查並發佈 docs/，Pages 本來已啟用 workflow，git credentials 可正常推送。
- WebGL 建置3成功（零建置錯誤，wasm gzip 6.2MB、data 2.5MB）。首次瀏覽器找到 CreatePrimitive MeshCollider 被裁剪與 PointerLock WrongDocumentError，均已修正：顯示面改明確 Mesh，Web 用可拒絕的安全 pointer-lock 包裝，另可右鍵拖曳轉頭。
- 建置4瀏覽器完整走到 completed=true / missing=0 / PASS_FULL_MIDTERM_FLOW，94 次模型動作；流程涵蓋兩眼檢影、WD、VA/PH、第一MPMVA、紅綠、APA、JCC紀錄與第二MPMVA。該次載入之後無新增 console errors。截圖 Reports/qa-midterm-build4.jpg。
- 圖像檢查繼續改善：提示卡加底色、字幕避免與前置卡重疊、實體針孔完全移開光路、反射面綁定患者瞳孔跟隨微動，手伸向實際旋鈕座標、消毒棉布擦過眼框。
- 新增第二MPMVA +0.50D 再霧視、PH數值、首次MPMVA及JCC中途紀錄、依流程評分與考試漏項回饋。完整回歸需在最終建置再次執行。
- 距離精修：顯示面 z=-1.525、瞳孔 z=4.475，剛好6m；儀器後光學面4.463，距瞳孔12mm。視標每個 E 的名義字高以5分視角/VA計算，筆畫1/5，像素取整仍有離散誤差。

### 最終版本驗收（接續）

- 最終為 build8，Unity 6000.6.5f1 WebGL build 成功、Errors=0；使用 hash 檔名 + gzip decompression fallback，下載 payload 合計約8.8MB。node tools/verify-site.mjs 通過，完整 SHA256 見 Reports/webgl-files.json。
- C# 檢查擴充為188項，全部通過，包含精確6m/12mm座標、六病例、兩眼完整流程、APA/PPAP有序比較、第二次MPMVA再霧視、流程評分與考試漏項。
- 最終 build8 的 Chrome 實際完整回歸結果：stage=16 / eye=1 / missing=0 / completed=true / action=PASS_FULL_MIDTERM_FLOW。Reports/final-runtime-state.json 與 final-midterm-pass.jpg 為證據；動畫紀錄陣列上限100，100不是總動作精確數。
- 最終載入後的 console error 檢查為0；過去版本的 MeshCollider、pointer lock、開發中覆寫多檔造成混載錯誤不列為最終版本通過證據。hash 檔名避免後續發布混用舊快取。
- 實際點擊 Unity 原生選單進入、W 鍵移動（z0.30→0.866，後續繼續走近）、F 坐近到z3.78、H沉浸模式收合工具列，均確認可用。Chrome自動化背景頁面會降低更新頻率，勿把背景測試速度當實體前景效能。
- 新增實體儀器球面/柱面/軸讀數。修正眼框孔道、PH移開位置；最終畫面能看見患者雙眼透過實際開孔，放大檢影視野中的橘紅反射，見 Reports/retinoscopy-final.jpg。
- 修正回選單後 HUD/病歷未隱藏、首次存檔後繼續按鈕未啟用的問題；每次開始病例重置手持工具。存檔恢復仍須接著用最終版重新整理驗證。
- 使用者再次提醒「記得寫session note」，此檔為主要交接來源；發布與公開站驗收完成後必須接著更新此段，README/AGENTS/CLAUDE均已指向此檔。
- 最終瀏覽器重新整理後，實際點擊「繼續上次練習」，恢復 stage16 / eye1 / completed=true / RX -1.50/-0.75x090。Tab病歷顯示雙眼Gross/Net/1st MPMVA/JCC/最終值與VA/PH，截圖 Reports/restored-records.jpg；狀態 Reports/restored-runtime-state.json。
- 在病歷實際點擊「回到選單」，病歷/HUD已隱藏並顯示可用的繼續按鈕。最終網站與全部可編輯來源準備推送至指定 Mvmpa5555 main，發布仍須確認 Actions 成功與公開頁面載入。

## 2026-10-15 下午：驗光儀重建、人物、PD 步驟、消毒動畫、光影

- 綜合驗光儀依 Reichert Phoroptor 使用手冊 11625-101 的 Parts Identification 重建（Blender 自行建模，未用第三方網格）：中央橫桿＋安裝座、PD 鈕/水平鈕/水平氣泡/PD 刻度窗、額托鈕與額托、兩個鏡片筒（外側弱球面大轉盤、強球面環、輔助鏡片旋鈕與刻度、同心柱面度數內鈕/軸位外鈕與量角盤、窺孔軸位參考刻度、讀數窗正黑負紅）、轉盤（turret）帶 JCC 與旋轉稜鏡、角膜對位器、面罩。正面（旋鈕刻度）朝檢查者、背面（面罩、額托）朝受測者；後光學面 z=4.463，距瞳孔 12 mm。
- 新增 PhoropterStand：立柱、可旋轉支臂（ArmSwing）、鎖定桿、檢查燈。Unity 執行時把 Phoropter 掛到 ArmSwing 下；stage0 支臂轉開 80°，做水平/桌高等準備或進入 stage1 時轉入。
- 病人改成連續曲面頭部（參數化變形）＋覆蓋殼頭髮/眉毛/唇（埋入式邊界，避免鋸齒）、弧形眼瞼可眨眼、眼球可轉動注視、瀏海、身體/手指/腿用 loft 管狀網格。眼位依病例 TruePD 調整。
- Unity：ClinicWorld 重寫——旋鈕直接操作（滑鼠指向發亮＋提示，滾輪/左右鍵）、旋鈕平滑驅動不鎖輸入、手部伸向實際旋鈕（左側用左手）；輔助鏡片 OC/PH 由內部鏡盤轉入窺孔；JCC 由轉盤轉入、與柱面軸齒輪連動、以手柄軸翻面。
- PD：對應邊緣法。空白鍵指示→Q 閉右眼（相機移到檢查者左眼）→←→ 讓 0 對準 OD 瞳孔右緣（顳側）→Enter→E 閉左眼→讀 OS 瞳孔鼻側緣→Enter。讀數由實際眼位、注視方向與尺面幾何計算（含視差）。教學模式先要求消毒。
- 消毒動畫：支臂轉開→雙手持酒精噴瓶噴在棉布上（霧粒子、按壓泵頭、音效）→擦額托、右/左面罩（背面，由內向外繞圈、水光粒子）→拿起 PD 尺擦正反兩面。QA 可用 `SendMessage('Clinic runtime','QATimeScale','0.3')` 慢動作檢查。
- 檢影：光帶是帶狹縫 cookie 的聚光燈，拖曳滑鼠掃動；反射帶速度/寬度/亮度依殘餘度數，破裂現象依殘餘散光主子午線；考試模式不念順逆動。
- 光影：Linear + HDR、ClinicPost（bloom、ACES、暗角）、窗光陰影（牆/天花板不投影）、檢查燈柔和陰影、檢查者側補光、執行時擷取房間 cubemap 作反射（不可把 Hidden/Cube* 加入 Always Included，會讓建置寫 unity_builtin_extra 失敗）。
- 字型子集需 fonttools：`uv run --no-project --with fonttools python tools/subset_font.py`。
- 驗證：ClinicBuild.Validate 188 項通過；WebGL 建置 Errors=0；verify-site 通過；瀏覽器 ?qa=1 完整自動流程 completed=true / missing=0（消毒、PD 64mm、9 項準備、雙眼檢影到第二次 MPMVA）。
- 使用者提供期中紀錄單：流程改為「雙眼完成同一項才進下一項」。stage 表（RefractionModel StageEye/StagePhase）：4/5 右左眼淨檢影 VA、6/7 右眼 MPMVA+紅綠、8/9 左眼、10/11 右眼 JCC+紀錄、12/13 左眼、14/15 右左第二次 MPMVA。病歷與下載檔照紀錄單 ①②③④。Validate 擴為 248 項並檢查順序；瀏覽器 QA 實際走 4:0 5:1 6:0 7:0 8:1 … 14:0 15:1 16 completed、missing=0。
- 平板：ClinicTouch.cs（全螢幕觸控板：拖曳轉頭/掃光帶、點物件互動；虛擬搖桿）、觸控按鈕列、點旋鈕出 −/＋ 列、PD 按鈕列、HUD 依比例貼齊上下緣、UI 依寬高比縮放、行動裝置降陰影/關 bloom、模板 touch-action:none。`?touch=1` 可強制觸控介面（iPadOS 回報桌面 UA 時，第一次觸控也會自動切換）。內建預覽瀏覽器的瞬間點擊（無滑鼠移動）不會觸發 uGUI，需先 mousemove；真實滑鼠/觸控正常。
- 2026-10-15 已推送 8081e74 到 Mvmpa5555 main，Actions 成功，公開站 ?qa=1 完整流程 completed / missing=0、無 console error。
- 之後依使用者回饋（842704d 已推送；Actions 38748318102 成功，公開站載入新 hash 檔，?qa=1 完整流程 stage 0→16 completed / missing=0、無 console error，WD 57cm 與光帶 +90° 介面已在線上確認）：工作距離選擇器 40/50/57/60/67/75/80/100 cm（預設 57 cm＝1.75 D，PlayerPrefs ClinicWD 記住；扣除後鎖定）；檢影：臉上畫出依光帶角度的細光帶（Clinic/Streak，瞳孔處挖空讓反射可見），光帶 0° 水平、90° 垂直，Q／按鈕 +90°，光帶方向靠撥動檢影鏡套筒滾輪（RetSleeve 轉動、手伸過去），本體不轉，只轉窺孔狹縫；檢影鏡拿在畫面中隨掃動位移傾斜，窺孔畫面有掃動方向線與指示；主觀驗光時右上角顯示 6 m 視標即時畫面（放大到目前這一列）；VA 每列 5 字，0.02 logMAR／字，記法 5/5→該列、3–4→該列 −錯數、1–2→上一列 +對數（視力表補 0.9 列）；PD 量測時暗化閉上那隻眼的一側，移除 W 雙眼張開鍵。PD 遙測確認：步驟② OD 0°、OS −8.8°（看檢查者左眼）；步驟④ OD +8.7°、OS 0°（看檢查者右眼）。Validate 249 項。
- 尚未：實體平板（iPad Safari / Android Chrome）實機測試。
- 2026-10-16 人物換成 MPFB／MakeHuman：Blender 5.2 安裝 MPFB 2.0.18 擴充（GPL，工具不入庫）與 makehuman_system_assets（CC0，下載檔 SHA-256 已核對）。新 `Blender/build_people.py`（在 build_clinic.py 之後跑）：亞洲青年男性、default rig 擺坐姿（兩段骨 IK 腿到腳踏、手放大腿），以 IPD 63.5 mm 等比縮放、角膜頂點對齊 z=4.4752（距視標 6 m）；匯出蒙皮網格（每頂點 4 權重）到 HeadPivot／Jaw／Lid_OD/OS（上眼瞼 orbicularis03 骨）→ Unity 用 SkinnedMeshRenderer 做頭部晃動、眨眼、說話時下巴開合（取代舊 MouthOpening）。眼球用自製程序眼（使用者要求保留），Pupil_/Catchlight 同舊節點名。手：取 MPFB 右手（手腕＋手指權重）轉到相機空間、左手鏡像，Finger_X0..3＝食中無名小指，Hand_ 樞紐位置不變；袖子與手套袖口仍由 build_clinic.py 產生（袖口縮小）。build_clinic.py 不再建舊病人，椅子液壓座降 7 cm 配合坐姿。ClinicBuild.ImportBlender 支援 normals／texture（上限 1024px）／cutout（頭髮眉毛睫毛）／骨骼權重。
- 同日：工作距離改成頂部列常駐按鈕「工作距離 57 cm」，點開可直接選 40/50/57/60/67/75/80/100 cm（原本只在診間準備面板與拿檢影鏡時才看得到）。
- 驗證：Validate 249 項；WebGL Errors=0；verify-site 通過；瀏覽器 ?qa=1 完整流程 PASS_FULL_MIDTERM_FLOW、missing=0、PD 64 mm；PD 畫面近看眼睛與尺無穿模；工作距離選單點選 67 cm 生效。
- 尚未：實體平板測試；人物只有靜態蒙皮（無表情 shape key）。
- 2026-10-16 依使用者回饋改主觀驗光：
  - JCC PPAP 的第一個 P 改為「四方向找柱面」：JCC 度數位 P 的紅點依序放 180°、45°、90°、135°（此輪轉柱軸鈕一次轉 45°，只轉 JCC 不改鏡片），每個方向翻面問一次；選第一面（紅點）＝接受該方向負柱面 → 柱面加 −0.25 且軸自動放在該方向；選第二面（白點）＝拒絕。四方向做完且沒有紅點方向才可進下一輪。Session.Searching／JCCRef／AskJCC／RedAngles。
  - 「最後一個 P 一直說第二片」：第二片（白點對軸）＝要減少負柱面，原本沒有說明，學生加負柱面只會更糟；現在教學／自由模式每次回答後提示 P／A 該怎麼調（考試模式不提示）。
  - 紅綠：不再預設「一樣清楚」。比真值少負 ≥0.25 回答紅色，其餘回答綠色 → 紅加負、綠加正、停在第一次出現綠色（＝真值）。第一次 MPMVA 容許停在真值 +0.25 內，讓紅綠真的有得練。
  - JCC 框（retainer）刻 A（手柄方向）與紅色 P（紅白點方向），哪個字對準下方柱軸就是哪種檢查。
  - 選單病例 − 從 01 往前一格＝「隨機病患（19–21 歲）」：約 80% 近視（−0.75 至約 −6.5，少數高度近視到 −9）、少數正視／遠視；75% 有散光（多為 −0.25 至 −1.25，順規 70%）、兩眼等效球面差小、PD 59–68。
  - Validate 254 項；WebGL Errors 0；瀏覽器 ?qa=1 完整流程 PASS、stage 0→16；QADirectScene 驗證病例 1 右眼紅點在 180° 時回答第一面。
- 2026-10-16 平板測試與新手教學：
  - 平板測試方式：本機 Chrome headless（CDP）模擬 iPad 1194×834／834×1194、iPad UA、5 點觸控，送出真的 touchStart/Move/End（scratchpad 的 playwright-core 腳本，不入庫）。搖桿走路、拖曳轉頭、點物件、坐近、點旋鈕出 −／＋、遙控器、拿檢影鏡沿金色線拖曳掃動、病歷、選單皆可用。
  - 修正：遙控器 VA 改 4 欄（加 1.5／2.0 後 2.0 溢出）；旋鈕列關閉鈕字改「關」（✕ 不在字型子集）；主按鈕字改純黑粗體；觸控模式自動拿掉按鈕上的鍵盤字（空白鍵、Enter、R…），Esc 改「選單」，說明列與坐近提示改觸控用語；直向時頂部提示「建議把平板橫放」。
  - 新手操作教學（主選單「新手操作教學」，ClinicGuide.cs，ClinicUI 的 partial）：31 步，金色外框＋上下跳動的箭頭指出要按的按鈕或 3D 物件（世界座標投影到畫面），卡片寫做法（桌機／觸控兩套文字），做到就自動下一步，只需閱讀的步驟按「下一步」（桌機也可按 N）。依序：介面、走路、轉頭、工作距離、準備清單、拿遙控器（自動帶到桌邊）、消毒與 PD 說明、坐近、旋鈕、OC、遙控器換列、詢問、檢影鏡拿起／掃動／+90°／放回、霧視、紅綠、JCC 放入與翻面、記錄、完成此步驟、病歷、回診間、選單。教學中會直接把病例切到需要的階段（VA、檢影、MPMVA、JCC），不存檔、不覆蓋「繼續上次練習」，結束後重置診間。
- 2026-10-16 平板雙指：搖桿＋拖曳同時操作時，瀏覽器會把兩根手指的編號弄混，畫面亂跳。改成 ClinicWorld.TrackTouches 依位置追蹤手指（一根管搖桿、一根管視角），單次位移上限 60 px、超過 140 px 視為換手指丟棄；觸控模式關閉滑鼠轉頭與 simulateMouseWithTouches。模擬 iPad 雙指測試已不再跳動。
- 新手教學改為教學模式並實際做完九項驗光前置（消毒動畫、PD 六小步：開始／請看我／閉右眼／對零點／閉左眼／讀數、坐近、水平、輔助鏡歸 O、窺孔、桌高頂點距離、紅綠大 E、鎖支臂、調暗燈）並按「完成此步驟」進入檢影，共 45 步。
- 任務卡新增「跳過檢影→MPMVA」（stage 1–5 顯示）：兩眼以參考度數當淨檢影值、扣好 WD，直接進右眼第一次 MPMVA；檢影、WD、檢影後 VA 不計分，考試模式記錄跳過。Validate 255 項。
- 2026-10-16 平板操作改版（ClinicDock.cs、ClinicCoach.cs 新增）：
  - 旋鈕直接拖曳：手指按在球面／柱面／軸位旋鈕上沿圓周拖曳（依旋鈕每格角度換算，轉向依模型朝向修正），PD／水平／額托／桌高上下拖曳；不用先點。ClinicWorld.KnobDown/Move/Up、PickControl（射線優先，否則取手指寬度內最近者）；拿檢影鏡時只吃精確命中，避免掃動時抓到旋鈕。取消點旋鈕後的 −／＋ 列，只留旋鈕名稱／數值提示。
  - 平板坐近版面：底部長條改成右下角兩欄按鈕（依階段只顯示需要的），視角 46°→60°、鏡頭略降，驗光儀完整露出；字幕移到最下緣，提示文字移到頂部。檢影鏡視角維持。
  - 練習模式下一步教練（教學／自由、提示開時；考試與新手教學中不顯示）：金框＋箭頭＋標籤＋手指圓點示範（轉動／掃動），由 session 狀態推導，只指出要操作的位置不給答案。「進入診間／教學」與「自由練習」現在會重設 hints=true。
  - 驗證：Unity WebGL 建置 Errors=0；模擬 iPad 視窗看到驗光儀完整、右下按鈕、金框提示、拖曳外圈球面環以 3.00D 一格變動。尚未：實體 iPad 手感、內側小轉盤轉向方向、完整 ?qa=1 回歸、ClinicBuild.Validate。
- 2026-10-16 後續：PD 步驟③⑤改為按住畫面左右拖曳（ClinicWorld.PDDrag：尺跟手指 1:1，讀數每 24 px 0.5 mm；桌機用滑鼠左鍵拖曳，◀ ▶ 與方向鍵保留）；這兩步的教練提示改成只在上方顯示文字（Coach.tagOnly），不再用箭頭擋 PD 數字。檢影右上窺孔面板加 ClinicSweepPad，在面板上拖曳即可掃動光帶（手指／滑鼠）。建置 Errors=0；拖曳效果僅在模擬視窗粗略看過，未實機驗證。
