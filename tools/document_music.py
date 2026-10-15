from pathlib import Path
p=Path('tools/verify-site.mjs');s=p.read_text(encoding='utf-8-sig').replace("import fs from 'node:fs';", "import fs from 'node:fs';\nimport crypto from 'node:crypto';")
s=s.replace('console.log(`Unity', '''const musicRel='StreamingAssets/Audio/blue-oiia-uiia-cat.mp3';
assert(html.includes('id="clinic-music" loop'), 'Background track must loop');
const music=fs.readFileSync(path.join(site,musicRel));
const original=fs.readFileSync(path.join(root,'UnityProject/Assets',musicRel));
assert.equal(crypto.createHash('sha256').update(music).digest('hex'),crypto.createHash('sha256').update(original).digest('hex'),'Published music matches supplied audio');
console.log(`Unity''');p.write_text(s,encoding='utf-8')
p=Path('README.md');s=p.read_text(encoding='utf-8')+'\n## 背景音樂與 ESC 選單\n\n背景音樂為使用者提供的「Blue Oiia - Uiia Cat.mp3」，瀏覽器第一次點擊或按鍵後開始循環播放。ESC 可調整音量（預設 20%，0% 靜音，跨次保留）、繼續、保存進度回主選單或放棄這輪。放棄前會再確認，只清除這輪存檔；音量與工作距離偏好保留。暫停時驗光動作及計時停止，背景音樂繼續。\n';p.write_text(s,encoding='utf-8')
