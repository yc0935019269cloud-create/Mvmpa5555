// 3D 診間:長條形診間(受測者 → 綜合驗光儀 → 驗光師 → 6 m → 視力表),投影機在左上角
import * as THREE from '../../vendor/three.module.min.js';
import { fmtSph } from '../sim/optics.js';
import { faceSVG, VB_W, VB_H } from './phoropterSVG.js';

const M = (color, o = {}) => new THREE.MeshStandardMaterial({ color, roughness: o.rough ?? 0.8, metalness: o.metal ?? 0, emissive: o.emissive ?? 0x000000, emissiveIntensity: o.ei ?? 1, transparent: o.opacity !== undefined, opacity: o.opacity ?? 1 });
const box = (w, h, d, mat) => new THREE.Mesh(new THREE.BoxGeometry(w, h, d), mat);
const cyl = (rt, rb, h, mat, seg = 24) => new THREE.Mesh(new THREE.CylinderGeometry(rt, rb, h, seg), mat);
const sph = (r, mat, seg = 24) => new THREE.Mesh(new THREE.SphereGeometry(r, seg, seg), mat);
const cap = (r, l, mat) => new THREE.Mesh(new THREE.CapsuleGeometry(r, l, 6, 14), mat);

export const STATIONS = {
  overview: { pos: [1.35, 1.9, 2.75], look: [-0.15, 0.95, -2.6], fov: 56 },
  phoro: { pos: [0.3, 1.36, -0.82], look: [0, 1.24, 0], fov: 50 },
  ret: { pos: [0.18, 1.32, -0.95], look: [0, 1.25, 0], fov: 40 },
  chart: { pos: [0.12, 1.5, -3.95], look: [0, 1.36, -6.2], fov: 40 },
  desk: { pos: [0.5, 1.55, -0.35], look: [1.15, 0.8, -0.7], fov: 52 },
  patient: { pos: [0.9, 1.45, -0.7], look: [0, 1.15, 0.1], fov: 46 },
};

// 綜合驗光儀相對升降桌面的高度:桌高「適中」時,窺孔剛好對齊受測者的眼睛
const EYE_Y = 1.265; // 受測者眼睛高度(頭 1.25 + 眼 0.015)
const FACE_W = 0.36, FACE_H = FACE_W * (VB_H / VB_W);
const AP_OFF = FACE_H / 2 - (288 / VB_H) * FACE_H; // 窺孔中心在驗光儀中心上方多少
const tableY = (h) => 0.58 + h * 0.6;
const PHORO_ABOVE = EYE_Y - AP_OFF - tableY(0.5);

export class ClinicScene {
  constructor(container, game, chartCanvas, { onHotspot } = {}) {
    this.container = container;
    this.game = game;
    this.chartCanvas = chartCanvas;
    this.onHotspot = onHotspot;
    this.station = 'overview';
    this.look = { yaw: 0, pitch: 0 };
    this.lookTarget = { yaw: 0, pitch: 0 };
    this.clock = new THREE.Clock();
    this.speakT = 0;
    this.dirty = true;

    const r = (this.renderer = new THREE.WebGLRenderer({ antialias: true, alpha: false, powerPreference: 'high-performance' }));
    r.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
    r.outputColorSpace = THREE.SRGBColorSpace;
    r.toneMapping = THREE.ACESFilmicToneMapping;
    r.toneMappingExposure = 1.0;
    container.appendChild(r.domElement);
    r.domElement.className = 'gl';

    this.scene = new THREE.Scene();
    this.scene.background = new THREE.Color(0xdfe6e8);
    this.scene.fog = new THREE.Fog(0xdfe6e8, 7, 15);
    this.camera = new THREE.PerspectiveCamera(58, 1, 0.05, 40);
    const s0 = STATIONS.overview;
    this.camPos = new THREE.Vector3(...s0.pos);
    this.camLook = new THREE.Vector3(...s0.look);
    this.camFov = s0.fov;

    this.buildRoom();
    this.buildFurniture();
    this.buildChart();
    this.buildProjector();
    this.buildPhoropter();
    this.buildPatient();
    this.buildHotspots();
    this.bindDrag();
    this.resize();
    this._ro = new ResizeObserver(() => this.resize());
    this._ro.observe(container);
    this.refresh();
    this.loop = this.loop.bind(this);
    this._raf = requestAnimationFrame(this.loop);
  }

  /* ---------------- 房間 ---------------- */
  buildRoom() {
    const S = this.scene;
    this.matWall = M(0xf3f5f5, { rough: 0.95 });
    this.matAccent = M(0x2d6f82, { rough: 0.9 });
    const W = 3.4, Z0 = -6.4, Z1 = 3.3, H = 2.7;
    const floor = new THREE.Mesh(new THREE.PlaneGeometry(W, Z1 - Z0), M(0xb9c3c6, { rough: 0.55 }));
    floor.rotation.x = -Math.PI / 2; floor.position.set(0, 0, (Z0 + Z1) / 2); S.add(floor);
    const ceil = new THREE.Mesh(new THREE.PlaneGeometry(W, Z1 - Z0), M(0xffffff, { rough: 1 }));
    ceil.rotation.x = Math.PI / 2; ceil.position.set(0, H, (Z0 + Z1) / 2); S.add(ceil);
    const wall = (w, h, x, y, z, ry, m = this.matWall) => {
      const p = new THREE.Mesh(new THREE.PlaneGeometry(w, h), m);
      p.position.set(x, y, z); p.rotation.y = ry; S.add(p); return p;
    };
    wall(Z1 - Z0, H, -W / 2, H / 2, (Z0 + Z1) / 2, Math.PI / 2); // 左牆
    wall(Z1 - Z0, H, W / 2, H / 2, (Z0 + Z1) / 2, -Math.PI / 2); // 右牆
    wall(W, H, 0, H / 2, Z0, 0, this.matAccent); // 視力表牆
    wall(W, H, 0, H / 2, Z1, Math.PI); // 後牆
    // 踢腳板
    const bb = M(0x8fa0a6);
    for (const [w, x, z, ry] of [[Z1 - Z0, -W / 2 + 0.01, (Z0 + Z1) / 2, Math.PI / 2], [Z1 - Z0, W / 2 - 0.01, (Z0 + Z1) / 2, Math.PI / 2]]) {
      const b = box(0.02, 0.1, w, bb); b.position.set(x, 0.05, z); S.add(b);
    }
    // 窗(右牆,遮光簾半掩)
    const win = new THREE.Mesh(new THREE.PlaneGeometry(1.4, 1.0), new THREE.MeshBasicMaterial({ color: 0xcfe6f2 }));
    win.position.set(W / 2 - 0.01, 1.55, -2.4); win.rotation.y = -Math.PI / 2; S.add(win);
    this.curtain = box(0.04, 1.1, 1.5, M(0x6f8e99, { opacity: 0.0 }));
    this.curtain.position.set(W / 2 - 0.03, 1.55, -2.4); S.add(this.curtain);
    // 門(後牆)
    const door = box(0.9, 2.05, 0.05, M(0xd9c7a3));
    door.position.set(-0.9, 1.025, Z1 - 0.03); S.add(door);
    // 地上標示:6 m 距離線
    const strip = new THREE.Mesh(new THREE.PlaneGeometry(0.04, 6.0), new THREE.MeshBasicMaterial({ color: 0xe2a42b }));
    strip.rotation.x = -Math.PI / 2; strip.position.set(-0.8, 0.003, -3.0); S.add(strip);

    // 燈
    this.hemi = new THREE.HemisphereLight(0xffffff, 0x8fa0a8, 1.05);
    S.add(this.hemi);
    this.panels = [];
    for (const z of [1.4, -0.9, -3.3, -5.4]) {
      const m = new THREE.MeshStandardMaterial({ color: 0xffffff, emissive: 0xffffff, emissiveIntensity: 1.2 });
      const p = box(0.9, 0.04, 0.5, m); p.position.set(0, H - 0.02, z); S.add(p);
      const L = new THREE.PointLight(0xfff8ee, 9, 7, 1.6); L.position.set(0, H - 0.3, z); S.add(L);
      this.panels.push({ m, L });
    }
    this.dirLight = new THREE.DirectionalLight(0xffffff, 0.35); this.dirLight.position.set(2, 3, 2); S.add(this.dirLight);
  }

  /* ---------------- 家具 ---------------- */
  buildFurniture() {
    const S = this.scene;
    // 受測者座椅
    const chair = new THREE.Group();
    const seat = box(0.5, 0.08, 0.5, M(0x2d3a42, { rough: 0.6 })); seat.position.y = 0.46;
    const back = box(0.5, 0.55, 0.07, M(0x2d3a42, { rough: 0.6 })); back.position.set(0, 0.82, 0.27);
    const post = cyl(0.04, 0.04, 0.42, M(0x7d8b92, { metal: 0.6, rough: 0.35 })); post.position.y = 0.23;
    const base = cyl(0.3, 0.3, 0.04, M(0x56636a, { metal: 0.5 })); base.position.y = 0.02;
    chair.add(seat, back, post, base); chair.position.set(0, 0, 0.3); S.add(chair);

    // 升降桌
    this.table = new THREE.Group();
    this.tableTop = box(0.8, 0.04, 0.5, M(0xe8e2d3, { rough: 0.5 }));
    const col = cyl(0.05, 0.05, 1, M(0x8f9ca2, { metal: 0.5, rough: 0.4 }));
    this.tableCol = col;
    const foot = box(0.7, 0.04, 0.5, M(0x56636a)); foot.position.y = 0.02;
    this.table.add(this.tableTop, col, foot);
    this.table.position.set(0, 0, -0.55); S.add(this.table);

    // 驗光師凳子 + 桌(右側)
    const desk = new THREE.Group();
    const dTop = box(0.8, 0.04, 0.7, M(0xe9e4d6, { rough: 0.5 })); dTop.position.y = 0.76;
    for (const [x, z] of [[-0.36, -0.3], [0.36, -0.3], [-0.36, 0.3], [0.36, 0.3]]) {
      const l = box(0.04, 0.76, 0.04, M(0x6a777d)); l.position.set(x, 0.38, z); desk.add(l);
    }
    desk.add(dTop); desk.position.set(1.15, 0, -0.65); S.add(desk);
    this.desk = desk;
    // 紀錄單
    this.sheetTex = new THREE.CanvasTexture(this.makeSheetCanvas());
    this.sheetTex.colorSpace = THREE.SRGBColorSpace;
    const paper = new THREE.Mesh(new THREE.PlaneGeometry(0.3, 0.42), new THREE.MeshBasicMaterial({ map: this.sheetTex }));
    paper.rotation.order = 'YXZ'; paper.rotation.y = -Math.PI / 2 + 0.1; paper.rotation.x = -Math.PI / 2; paper.position.set(1.08, 0.785, -0.62); S.add(paper);
    // 檢影鏡
    const ret = new THREE.Group();
    const hnd = cyl(0.014, 0.014, 0.17, M(0x222a2f, { metal: 0.4 })); hnd.rotation.z = Math.PI / 2;
    const head = cyl(0.026, 0.02, 0.06, M(0x3b454b, { metal: 0.5 })); head.position.set(0.1, 0.0, 0);head.rotation.z = Math.PI / 2;
    ret.add(hnd, head); ret.position.set(1.28, 0.8, -0.82); ret.rotation.y = 0.6; S.add(ret);
    // 酒精噴瓶
    const bottle = new THREE.Group();
    const bb = cyl(0.03, 0.03, 0.11, M(0x6fb7d1, { opacity: 0.85 })); bb.position.y = 0.055;
    const nz = cyl(0.012, 0.016, 0.04, M(0xf2f2f2)); nz.position.y = 0.13;
    bottle.add(bb, nz); bottle.position.set(1.34, 0.78, -0.45); S.add(bottle);
    // PD 尺
    const ruler = box(0.2, 0.004, 0.025, M(0xf1e24a)); ruler.position.set(0.98, 0.782, -0.38); ruler.rotation.y = 0.2; S.add(ruler);
    // 燈開關
    const sw = box(0.015, 0.1, 0.07, M(0xffffff)); sw.position.set(-1.69, 1.3, -0.4); S.add(sw);
    // 簡單的牆上海報
    const poster = new THREE.Mesh(new THREE.PlaneGeometry(0.7, 0.9), new THREE.MeshBasicMaterial({ map: this.posterTexture() }));
    poster.position.set(-1.69, 1.55, -3.0); poster.rotation.y = Math.PI / 2; S.add(poster);
    // 懶人假影
    for (const [x, z, r] of [[0, 0.3, 0.45], [1.15, -0.65, 0.6]]) {
      const sh = new THREE.Mesh(new THREE.CircleGeometry(r, 24), new THREE.MeshBasicMaterial({ color: 0x000000, transparent: true, opacity: 0.12 }));
      sh.rotation.x = -Math.PI / 2; sh.position.set(x, 0.004, z); S.add(sh);
    }
  }

  makeSheetCanvas() {
    const c = document.createElement('canvas'); c.width = 300; c.height = 420; this.sheetCanvas = c; return c;
  }
  drawSheet() {
    const c = this.sheetCanvas, x = c.getContext('2d'), g = this.game;
    x.fillStyle = '#fdfdf8'; x.fillRect(0, 0, 300, 420);
    x.fillStyle = '#243a44'; x.font = '700 15px sans-serif'; x.fillText('自覺式驗光 紀錄單', 12, 24);
    x.font = '11px sans-serif'; x.fillStyle = '#5b6a72';
    x.fillText(`受測者:${g.patient.name}   PD ${g.sheet.pd ?? '＿'}   WD ${g.sheet.wdCm ?? '＿'} cm`, 12, 42);
    const rows = [['① Ret', 'ret'], ['② 1st MPMVA', 'mp1'], ['③ JCC', 'jcc'], ['④ 2nd MPMVA', 'mp2']];
    let y = 60;
    x.strokeStyle = '#b7c2c7';
    for (const [lab, slot] of rows) {
      x.fillStyle = '#243a44'; x.font = '700 11px sans-serif'; x.fillText(lab, 12, y + 12);
      for (const [i, eye] of ['OD', 'OS'].entries()) {
        const yy = y + 14 + i * 22;
        x.strokeRect(12, yy, 276, 20);
        x.fillStyle = '#6a7a82'; x.font = '10px sans-serif'; x.fillText(eye, 16, yy + 14);
        const e = g.sheet.slots[slot][eye];
        const va = g.sheet.va[slot]?.[eye];
        x.fillStyle = '#0d2a8f'; x.font = '12px "Segoe Script","Bradley Hand",cursive';
        if (e) x.fillText(`${fmtSph(e.rx.s)} ${e.rx.c ? fmtSph(e.rx.c) + ' x ' + e.rx.a : ''}`, 40, yy + 14);
        if (va) x.fillText(`VA ${va.row}${va.delta ? (va.delta > 0 ? '+' : '') + va.delta : ''}`, 220, yy + 14);
      }
      y += 80;
    }
    this.sheetTex.needsUpdate = true;
  }
  posterTexture() {
    const c = document.createElement('canvas'); c.width = 256; c.height = 330;
    const x = c.getContext('2d');
    x.fillStyle = '#fff'; x.fillRect(0, 0, 256, 330);
    x.strokeStyle = '#2d6f82'; x.lineWidth = 6; x.strokeRect(6, 6, 244, 318);
    x.fillStyle = '#2d6f82'; x.font = '700 24px sans-serif'; x.textAlign = 'center';
    x.fillText('眼球構造', 128, 46);
    x.beginPath(); x.arc(128, 160, 70, 0, Math.PI * 2); x.fillStyle = '#f5efe6'; x.fill(); x.stroke();
    x.beginPath(); x.arc(128, 160, 30, 0, Math.PI * 2); x.fillStyle = '#4a8aa3'; x.fill();
    x.beginPath(); x.arc(128, 160, 13, 0, Math.PI * 2); x.fillStyle = '#111'; x.fill();
    x.fillStyle = '#51606a'; x.font = '14px sans-serif'; x.fillText('角膜 · 水晶體 · 視網膜', 128, 280);
    const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; return t;
  }

  /* ---------------- 視力表(6 m) ---------------- */
  buildChart() {
    const S = this.scene;
    this.chartTex = new THREE.CanvasTexture(this.chartCanvas);
    this.chartTex.colorSpace = THREE.SRGBColorSpace;
    this.chartTex.anisotropy = 4;
    const frame = box(0.66, 0.9, 0.06, M(0x1b2227, { rough: 0.5 }));
    frame.position.set(0, 1.4, -6.33); S.add(frame);
    this.chartMesh = new THREE.Mesh(new THREE.PlaneGeometry(0.6, 0.84), new THREE.MeshBasicMaterial({ map: this.chartTex }));
    this.chartMesh.position.set(0, 1.4, -6.298); S.add(this.chartMesh);
    this.chartGlow = new THREE.Mesh(new THREE.PlaneGeometry(1.4, 1.5), new THREE.MeshBasicMaterial({ color: 0xfff6dd, transparent: true, opacity: 0.0, blending: THREE.AdditiveBlending, depthWrite: false }));
    this.chartGlow.position.set(0, 1.4, -6.29); S.add(this.chartGlow);
    const lab = box(0.5, 0.02, 0.02, M(0x1b2227)); lab.position.set(0, 0.92, -6.3);
    S.add(lab);
  }

  buildProjector() {
    const S = this.scene;
    const g = new THREE.Group();
    const body = box(0.3, 0.1, 0.22, M(0xf1f1ee, { rough: 0.5 })); g.add(body);
    const lens = cyl(0.04, 0.04, 0.05, M(0x1c2328)); lens.rotation.x = Math.PI / 2; lens.position.set(0, 0, -0.13); g.add(lens);
    const rod = cyl(0.015, 0.015, 0.25, M(0x8a979c)); rod.position.set(0, 0.17, 0); g.add(rod);
    g.position.set(-0.85, 2.45, -0.35);
    S.add(g);
    this.projector = g;
    // 光束(只在調暗時隱約可見)
    const from = new THREE.Vector3(-0.85, 2.45, -0.5), to = new THREE.Vector3(0, 1.4, -6.28);
    const dir = to.clone().sub(from); const len = dir.length();
    const cone = new THREE.Mesh(new THREE.ConeGeometry(0.4, len, 24, 1, true), new THREE.MeshBasicMaterial({ color: 0xcfe8ff, transparent: true, opacity: 0.0, depthWrite: false, side: THREE.DoubleSide, blending: THREE.AdditiveBlending }));
    cone.position.copy(from.clone().add(to).multiplyScalar(0.5));
    cone.quaternion.setFromUnitVectors(new THREE.Vector3(0, -1, 0), dir.clone().normalize());
    S.add(cone); this.beam = cone;
  }

  /* ---------------- 綜合驗光儀 ---------------- */
  buildPhoropter() {
    const S = this.scene;
    const P = (this.phoro = new THREE.Group());
    const FW = FACE_W, FH = FACE_H; // 正面板尺寸(約 36 × 24 cm)
    const ivory = new THREE.MeshPhongMaterial({ color: 0xd6d0c1, specular: 0x555555, shininess: 40 });
    const body = new THREE.Mesh(new THREE.BoxGeometry(FW - 0.01, FH - 0.012, 0.11), ivory); P.add(body);
    // 上方橫樑與背面圓殼,讓側面看起來有厚度
    const beam = new THREE.Mesh(new THREE.BoxGeometry(FW * 0.66, 0.045, 0.1), ivory); beam.position.set(0, FH / 2 - 0.02, 0.006); P.add(beam); // 退到正面板後面,不擋到面板
    for (const k of [-1, 1]) {
      const back = cyl(0.085, 0.085, 0.07, ivory, 40); back.rotation.x = Math.PI / 2; back.position.set(k * 0.1, -0.01, 0.075); P.add(back);
    }
    // 貼圖正面(驗光師看到的那一面)
    this.faceCanvas = document.createElement('canvas');
    this.faceCanvas.width = VB_W; this.faceCanvas.height = VB_H;
    this.faceTex = new THREE.CanvasTexture(this.faceCanvas);
    this.faceTex.colorSpace = THREE.SRGBColorSpace;
    this.faceTex.anisotropy = 8;
    const face = new THREE.Mesh(new THREE.PlaneGeometry(FW, FH), new THREE.MeshBasicMaterial({ map: this.faceTex, transparent: true }));
    face.rotation.y = Math.PI; face.position.z = -0.0565; P.add(face);
    this.faceMesh = face;
    // 額靠(朝受測者)
    const rest = box(0.1, 0.035, 0.05, M(0x8da0a8)); rest.position.set(0, 0.04, 0.12); P.add(rest);
    const rod = cyl(0.008, 0.008, 0.1, M(0x8a979c)); rod.rotation.x = Math.PI / 2; rod.position.set(0, 0.04, 0.085); P.add(rod);
    // 吊掛桿(接到上方的支撐臂)
    const hang = cyl(0.012, 0.012, 0.06, M(0x9aa5aa, { metal: 0.6, rough: 0.3 })); hang.position.set(0, FH / 2 + 0.03, 0.01); P.add(hang);
    const hub = cyl(0.028, 0.028, 0.02, M(0x6a777d, { metal: 0.5, rough: 0.35 })); hub.position.set(0, FH / 2 + 0.06, 0.01); P.add(hub);
    P.position.set(0, tableY(0.5) + PHORO_ABOVE, -0.14);
    S.add(P);
    // 升降桌上的立柱 + 支撐臂:驗光儀從上方吊下來(跟著桌子升降)
    const metal = M(0x8f9ca2, { metal: 0.55, rough: 0.35 });
    const st = (this.stand = new THREE.Group());
    const armY = PHORO_ABOVE + FH / 2 + 0.06;
    const colX = 0.34, colZ = -0.5;
    const column = cyl(0.03, 0.03, armY + 0.04, metal); column.position.set(colX, (armY + 0.04) / 2, colZ); st.add(column);
    const cap0 = cyl(0.04, 0.04, 0.05, M(0x56636a, { metal: 0.4 })); cap0.position.set(colX, armY + 0.02, colZ); st.add(cap0);
    const dx = 0 - colX, dz = -0.13 - colZ, len = Math.hypot(dx, dz);
    const arm = box(len, 0.035, 0.05, metal); arm.position.set(colX + dx / 2, armY + 0.02, colZ + dz / 2); arm.rotation.y = -Math.atan2(dz, dx); st.add(arm);
    S.add(st); // 世界座標;y 跟著桌面高度在 refresh() 更新
    this._faceTok = 0;
    this._faceSvg = '';
  }

  updateFaceTexture() {
    const svg = faceSVG(this.game, { interactive: false });
    if (svg === this._faceSvg) return;
    this._faceSvg = svg;
    const tok = ++this._faceTok;
    const img = new Image();
    img.onload = () => {
      if (tok !== this._faceTok) return;
      const c = this.faceCanvas.getContext('2d');
      c.clearRect(0, 0, VB_W, VB_H);
      c.drawImage(img, 0, 0, VB_W, VB_H);
      this.faceTex.needsUpdate = true;
    };
    img.src = 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(svg);
  }

  /* ---------------- 受測者 ---------------- */
  buildPatient() {
    const S = this.scene;
    const p = this.game.patient;
    const rngc = (i) => ((p.seed * 9301 + i * 49297) % 233280) / 233280;
    const skins = [0xf1c9a5, 0xe3b48b, 0xd39d73, 0xc68863];
    const hairs = [0x1f1812, 0x2c1e14, 0x3d2a1c, 0x14100c];
    const shirts = [0x4f86a1, 0xc66b5c, 0x7da36b, 0xd8b04d, 0x8b7bb5, 0x586d7a];
    const skin = M(skins[Math.floor(rngc(1) * skins.length)], { rough: 0.7 });
    const hair = M(hairs[Math.floor(rngc(2) * hairs.length)], { rough: 0.9 });
    const shirt = M(shirts[Math.floor(rngc(3) * shirts.length)], { rough: 0.9 });
    const pants = M(0x3a4650, { rough: 0.9 });
    const G = (this.patientGroup = new THREE.Group());
    const torso = cap(0.16, 0.3, shirt); torso.position.set(0, 0.9, 0.28); G.add(torso);
    const neck = cyl(0.045, 0.05, 0.08, skin); neck.position.set(0, 1.14, 0.2); G.add(neck);
    const head = (this.head = new THREE.Group()); head.position.set(0, 1.25, 0.17);
    const skull = sph(0.105, skin); skull.scale.set(0.92, 1.08, 1);
    const hairCap = sph(0.112, hair); hairCap.scale.set(0.95, 1.0, 1.0); hairCap.position.set(0, 0.025, 0.03);
    hairCap.geometry = new THREE.SphereGeometry(0.112, 24, 16, 0, Math.PI * 2, 0, Math.PI * 0.62);
    const nose = new THREE.Mesh(new THREE.ConeGeometry(0.014, 0.04, 10), skin); nose.rotation.x = -Math.PI / 2; nose.position.set(0, -0.01, -0.108);
    const earL = sph(0.022, skin); earL.position.set(-0.098, 0, 0.0); const earR = earL.clone(); earR.position.x = 0.098;
    const eyeM = M(0x1b1f22);
    const eL = sph(0.011, eyeM); eL.position.set(-0.032, 0.015, -0.098); const eR = eL.clone(); eR.position.x = 0.032;
    head.add(skull, hairCap, nose, earL, earR, eL, eR); G.add(head);
    // 手臂與腿
    for (const sx of [-1, 1]) {
      const ua = cap(0.04, 0.2, shirt); ua.position.set(sx * 0.2, 0.9, 0.28); ua.rotation.z = sx * 0.12; G.add(ua);
      const fa = cap(0.035, 0.2, skin); fa.position.set(sx * 0.19, 0.74, 0.12); fa.rotation.x = Math.PI / 2.4; G.add(fa);
      const th = cap(0.065, 0.3, pants); th.rotation.x = Math.PI / 2; th.position.set(sx * 0.1, 0.52, 0.0); G.add(th);
      const sh = cap(0.055, 0.34, pants); sh.position.set(sx * 0.1, 0.26, -0.18); G.add(sh);
      const ft = box(0.09, 0.05, 0.2, M(0x20262b)); ft.position.set(sx * 0.1, 0.03, -0.24); G.add(ft);
    }
    S.add(G);
  }

  /* ---------------- 熱點(HTML 按鈕疊在 3D 上) ---------------- */
  buildHotspots() {
    this.hotspotLayer = document.createElement('div');
    this.hotspotLayer.className = 'hotspots';
    this.container.appendChild(this.hotspotLayer);
    const defs = [
      ['phoro', '綜合驗光儀', new THREE.Vector3(0, 1.47, -0.14), ['overview', 'phoro', 'ret', 'patient', 'desk']],
      ['chart', '視力表 · 6 m', new THREE.Vector3(0, 1.98, -6.2), ['overview', 'chart']],
      ['sheet', '紀錄單', new THREE.Vector3(1.08, 0.9, -0.62), ['overview', 'desk', 'phoro']],
      ['ret', '檢影鏡', new THREE.Vector3(1.28, 0.9, -0.82), ['overview', 'desk']],
      ['sanitize', '酒精消毒', new THREE.Vector3(1.34, 0.95, -0.45), ['overview', 'desk']],
      ['switch', '燈光開關', new THREE.Vector3(-1.65, 1.42, -0.4), ['overview']],
      ['table', '升降桌', new THREE.Vector3(0.0, 0.7, -0.55), ['overview']],
      ['pd', 'PD 尺', new THREE.Vector3(0.98, 0.86, -0.38), ['overview', 'desk']],
    ];
    this.hotspots = defs.map(([id, label, pos, st]) => {
      const b = document.createElement('button');
      b.className = 'hs';
      b.type = 'button';
      b.setAttribute('aria-label', label);
      b.innerHTML = `<i></i><span>${label}</span>`;
      b.addEventListener('click', (e) => { e.stopPropagation(); this.onHotspot?.(id); });
      this.hotspotLayer.appendChild(b);
      return { id, el: b, pos, stations: st };
    });
  }

  /* ---------------- 互動 ---------------- */
  bindDrag() {
    const el = this.renderer.domElement;
    let down = null;
    el.addEventListener('pointerdown', (e) => { down = { x: e.clientX, y: e.clientY, yaw: this.lookTarget.yaw, pitch: this.lookTarget.pitch }; el.setPointerCapture(e.pointerId); });
    el.addEventListener('pointermove', (e) => {
      if (!down) return;
      const k = 0.0035;
      this.lookTarget.yaw = Math.max(-0.5, Math.min(0.5, down.yaw - (e.clientX - down.x) * k));
      this.lookTarget.pitch = Math.max(-0.3, Math.min(0.3, down.pitch - (e.clientY - down.y) * k));
    });
    const up = () => { down = null; };
    el.addEventListener('pointerup', up); el.addEventListener('pointercancel', up);
    el.addEventListener('dblclick', () => { this.lookTarget.yaw = 0; this.lookTarget.pitch = 0; });
  }

  goTo(name) {
    if (!STATIONS[name]) return;
    this.station = name;
    this.lookTarget.yaw = 0; this.lookTarget.pitch = 0;
    this.updateHotspots();
  }

  pulseSpeak() { this.speakT = 0.9; }

  /* ---------------- 與遊戲狀態同步 ---------------- */
  refresh() {
    const g = this.game;
    const P = g.phoro;
    // 桌高 → 驗光儀高度
    const ty = tableY(P.height);
    this.table.children[0].position.y = ty; // tableTop
    this.tableCol.scale.y = ty; this.tableCol.position.y = ty / 2;
    this.stand.position.y = ty;
    this.phoro.position.y = ty + PHORO_ABOVE;
    // 水平
    this.phoro.rotation.z = (P.level * Math.PI) / 180;
    // 燈光
    const dim = g.room.dim;
    this.hemi.intensity = dim ? 0.28 : 1.05;
    this.dirLight.intensity = dim ? 0.05 : 0.35;
    for (const p of this.panels) { p.L.intensity = dim ? 1.6 : 9; p.m.emissiveIntensity = dim ? 0.18 : 1.2; }
    this.scene.background.set(dim ? 0x8d979b : 0xdfe6e8);
    this.scene.fog.color.set(dim ? 0x8d979b : 0xdfe6e8);
    this.beam.material.opacity = dim ? 0.05 : 0.0;
    this.chartGlow.material.opacity = dim ? 0.07 : 0.0;
    this.renderer.toneMappingExposure = dim ? 1.05 : 1.0;
    this.faceMesh.material.color.setScalar(dim ? 0.72 : 1);
    this.updateFaceTexture();
    this.drawSheet();
    this.chartTex.needsUpdate = true;
    this.updateHotspots();
  }

  updateHotspots() {
    const step = this.game.stepId;
    const setup = step === 'setup';
    // 前置項目做完的熱點打勾
    const st = setup ? this.game.setupState() : {};
    const done = { sanitize: st.sanitize, switch: st.dim, table: st.height, pd: st.pd };
    for (const h of this.hotspots) {
      let show = h.stations.includes(this.station);
      if (['switch', 'table', 'sanitize', 'pd'].includes(h.id)) show = show && setup;
      else if (setup && ['sheet', 'ret'].includes(h.id)) show = false;
      else if (h.id === 'ret') show = show && (step === 'ret' || step === 'wd' || this.game.mode !== 'exam');
      h.visible = show;
      h.el.style.display = show ? '' : 'none';
      h.el.classList.toggle('done', !!done[h.id]);
    }
  }

  resize() {
    const w = this.container.clientWidth || 800, h = this.container.clientHeight || 600;
    this.renderer.setSize(w, h, false);
    this.renderer.domElement.style.width = '100%'; this.renderer.domElement.style.height = '100%';
    this.camera.aspect = w / h;
    // 窄螢幕(直立)時拉大視角
    this.aspect = w / h;
    this.camera.updateProjectionMatrix();
  }

  loop() {
    const dt = Math.min(0.05, this.clock.getDelta());
    const t = this.clock.elapsedTime;
    // 鏡頭平滑移動
    const st = STATIONS[this.station];
    const k = 1 - Math.exp(-dt * 4.2);
    this.camPos.lerp(new THREE.Vector3(...st.pos), k);
    this.camLook.lerp(new THREE.Vector3(...st.look), k);
    this.camFov += (st.fov - this.camFov) * k;
    this.look.yaw += (this.lookTarget.yaw - this.look.yaw) * k;
    this.look.pitch += (this.lookTarget.pitch - this.look.pitch) * k;
    const dir = this.camLook.clone().sub(this.camPos);
    const dist = dir.length();
    const yaw = Math.atan2(dir.x, -dir.z) + this.look.yaw;
    const pitch = Math.asin(dir.y / dist) + this.look.pitch;
    const d2 = new THREE.Vector3(Math.sin(yaw) * Math.cos(pitch), Math.sin(pitch), -Math.cos(yaw) * Math.cos(pitch));
    this.camera.position.copy(this.camPos);
    this.camera.lookAt(this.camPos.clone().add(d2.multiplyScalar(dist)));
    const portrait = this.aspect < 1;
    this.camera.fov = this.camFov * (portrait ? 1.35 : 1);
    this.camera.updateProjectionMatrix();

    // 受測者待機動作
    this.speakT = Math.max(0, this.speakT - dt);
    if (this.head) {
      this.head.rotation.x = Math.sin(t * 0.9) * 0.012 + (this.speakT > 0 ? Math.sin(t * 20) * 0.03 : 0);
      this.head.rotation.z = Math.sin(t * 0.5) * 0.01;
    }
    if (this.patientGroup) this.patientGroup.children[0].scale.y = 1 + Math.sin(t * 1.6) * 0.008;
    this.renderer.render(this.scene, this.camera);
    this.projectHotspots();
    this._raf = requestAnimationFrame(this.loop);
  }

  projectHotspots() {
    const w = this.container.clientWidth, h = this.container.clientHeight;
    const v = new THREE.Vector3();
    for (const hs of this.hotspots) {
      if (!hs.visible) continue;
      v.copy(hs.pos).project(this.camera);
      const inFront = v.z < 1 && v.z > -1;
      const x = (v.x * 0.5 + 0.5) * w, y = (-v.y * 0.5 + 0.5) * h;
      hs.el.style.transform = `translate(${x.toFixed(1)}px, ${y.toFixed(1)}px) translate(-50%, -50%)`;
      hs.el.style.opacity = inFront && x > -20 && x < w + 20 && y > -20 && y < h + 20 ? '1' : '0';
      hs.el.style.pointerEvents = hs.el.style.opacity === '1' ? 'auto' : 'none';
    }
  }

  dispose() {
    cancelAnimationFrame(this._raf);
    this._ro?.disconnect();
    this.renderer.dispose();
    this.renderer.domElement.remove();
    this.hotspotLayer.remove();
  }
}
