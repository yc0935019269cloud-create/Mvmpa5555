import fs from 'node:fs';
import crypto from 'node:crypto';
import path from 'node:path';
import zlib from 'node:zlib';
import assert from 'node:assert/strict';
import {fileURLToPath} from 'node:url';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const site=path.join(root,'docs');const html=fs.readFileSync(path.join(site,'index.html'),'utf8');
assert(html.includes('createUnityInstance'),'Homepage must load the actual Unity engine');
assert(!html.includes('{{{'),'No unexpanded Unity template placeholders');
let checked=0;
for(const key of ['dataUrl','frameworkUrl','codeUrl']){
 const match=html.match(new RegExp(key+":'([^']+)'"));assert(match,`Missing ${key}`);const file=path.join(site,match[1]);assert(fs.statSync(file).size>1000,`Empty ${key}`);
 const raw=fs.readFileSync(file);const bytes=file.endsWith('.unityweb')?zlib.gunzipSync(raw):raw;
 if(key==='codeUrl')assert.equal(bytes.subarray(0,4).toString('hex'),'0061736d','Wasm magic header');
 checked++;
}
const loader=html.match(/script\.src='([^']+)'/);assert(loader&&fs.existsSync(path.join(site,loader[1])),'Loader exists');
assert(fs.existsSync(path.join(site,'.nojekyll')),'Pages must serve underscore assets');
const clinical=fs.readFileSync(path.join(root,'Reports/clinical-validation.txt'),'utf8');assert(!clinical.includes('FAIL '));assert(/\d+ checks passed/.test(clinical));
const build=fs.readFileSync(path.join(root,'Reports/webgl-build.txt'),'utf8');assert(build.includes('Result: Succeeded'));assert(build.includes('Errors: 0'));
const musicRel='StreamingAssets/Audio/blue-oiia-uiia-cat.mp3';
assert(html.includes('id="clinic-music" loop'), 'Background track must loop');
const music=fs.readFileSync(path.join(site,musicRel));
const original=fs.readFileSync(path.join(root,'UnityProject/Assets',musicRel));
assert.equal(crypto.createHash('sha256').update(music).digest('hex'),crypto.createHash('sha256').update(original).digest('hex'),'Published music matches supplied audio');
console.log(`Unity WebGL deployment preflight passed: ${checked} payloads + loader + clinical/build reports`);
