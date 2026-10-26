import fs from 'node:fs';
import path from 'node:path';
const root=path.resolve(import.meta.dirname,'..');
const injection=`\n<nav id="practice-modes" aria-label="獨立練習模式" style="position:fixed;top:12px;right:12px;z-index:5;display:flex;gap:6px;font:13px system-ui"><a href="modes/index.html" style="background:#162b2bea;color:#d8bd80;border:1px solid #78908688;border-radius:5px;padding:9px 12px;text-decoration:none">全部練習模式</a><a href="modes/remote.html" style="background:#162b2bea;color:#eaeada;border:1px solid #78908688;border-radius:5px;padding:9px 12px;text-decoration:none">ACP-8 遙控器</a><a href="modes/refraction.html" style="background:#162b2bea;color:#eaeada;border:1px solid #78908688;border-radius:5px;padding:9px 12px;text-decoration:none">2D 驗光</a></nav>\n`;
for(const name of ['docs/index.html','UnityProject/Assets/WebGLTemplates/Clinic/index.html']){const p=path.join(root,name);let t=fs.readFileSync(p,'utf8');if(!t.includes('id="practice-modes"'))t=t.replace('</body>',injection+'</body>');fs.writeFileSync(p,t);}
console.log('Linked standalone practice modes in the current site and Unity WebGL template.');
