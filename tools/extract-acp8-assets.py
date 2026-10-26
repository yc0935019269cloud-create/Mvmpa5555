"""Extract the A-type chart artwork as vector source; no invented optotypes.

Usage: python tools/extract-acp8-assets.py work/acp8-brochure.pdf PHOTO_PATH
The source is the 2011 TOPCON A/IS brochure, page 3, not the later 4-set layout.
"""
from pathlib import Path
import json, shutil, sys
import fitz

root = Path(__file__).resolve().parents[1]
dest = root / 'docs/modes/assets'
dest.mkdir(parents=True, exist_ok=True)
page = fitz.open(sys.argv[1])[2]
(dest/'acp8-chart-source.svg').write_text(page.get_svg_image(text_as_path=True), encoding='utf-8')
shutil.copyfile(sys.argv[2], dest/'acp8-photo.png')
# Coordinates in the original 595.276 x 841.89 pt brochure page.
# Rectangles include the full optotype plate; polar charts retain the round field.
dy=188.4
def box(x,y,w=55.8,h=42.5): return [round(x+1.1,2),round(y+dy+1.1,2),round(w-2.2,2),round(h-2.2,2)]
charts=[]
def add(id,name,xy,family='',rows=None,counts=None):
    charts.append(dict(id=id,name=name,box=box(*xy),family=family,rows=rows or [],counts=counts or ([5]*len(rows) if rows else [])))
add(1,'字母 0.05 · K',(179.8,70.8),'letters',[.05],[1])
add(2,'字母 0.1／0.16 · DEC / HON',(240.23,70.8),'letters',[.1,.16],[3,3])
add(3,'字母 0.2／0.3／0.4',(300.01,70.8),'letters',[.2,.3,.4])
add(5,'字母 0.5／0.6／0.7',(360.8,70.85),'letters',[.5,.6,.7])
add(6,'字母 0.8／0.9／1.0',(421.24,70.48),'letters',[.8,.9,1])
add(7,'字母 1.2／1.5／2.0',(481.44,70.27),'letters',[1.2,1.5,2])
add(4,'固定散光放射圖',(59.91,113.82,55.51,55.51))
add(8,'紅綠視標',(123.05,119.42,47.91,42.51))
add(27,'E 字 0.1／0.2',(179.86,118.48),'E',[.1,.2],[3,3])
add(28,'E 字 0.3／0.4／0.5',(239.76,118.48),'E',[.3,.4,.5])
add(29,'E 字 0.6／0.7／0.8',(300.15,118.48),'E',[.6,.7,.8])
add(30,'E 字 1.0／1.2／1.5',(360.78,118.48),'E',[1,1.2,1.5])
add(23,'數字 0.1／0.2',(421.27,118.66),'numbers',[.1,.2],[3,3])
add(24,'數字 0.3／0.4／0.5',(481.06,118.65),'numbers',[.3,.4,.5])
add(25,'數字 0.6／0.7／0.8',(58.39,170.93),'numbers',[.6,.7,.8])
add(26,'數字 1.0／1.2／1.5',(118.23,170.88),'numbers',[1,1.2,1.5])
add(19,'兒童圖 0.1',(180.18,170.91),'children',[.1],[1])
add(20,'兒童圖 0.2／0.3',(240.06,171.0),'children',[.2,.3],[3,3])
add(21,'兒童圖 0.4／0.5／0.6',(300.67,170.74),'children',[.4,.5,.6])
add(22,'兒童圖 0.7／0.8／1.0',(361.38,170.87),'children',[.7,.8,1])
add(9,'交叉圓柱鏡點群',(421.89,163.98,56.1,56.1))
add(14,'注視點',(481.42,164.15,55.8,55.8))
add(11,'偏光紅綠視標',(59.8,222.17,55.8,55.8))
add(10,'雙眼平衡字母表',(119.98,222.17,55.8,55.8))
add(12,'不等像／相合測試 1',(180.28,222.74,55.8,55.8))
add(13,'不等像／相合測試 2',(240.24,222.7,55.8,55.8))
add(15,'偏光十字 1',(300.9,222.74,55.8,55.8))
add(16,'偏光十字 2',(361.21,222.74,55.8,55.8))
add(17,'立體視圖',(421.37,222.85,55.8,55.8))
add(18,'Worth 四點',(481.37,222.85,55.8,55.8))
for c in charts:
    c['round']=c['id'] in [4,9,10,11,12,13,14,15,16,17,18]
(dest/'chart-data.js').write_text('window.ACP8_CHARTS='+json.dumps(sorted(charts,key=lambda c:c['id']),ensure_ascii=False,separators=(',',':'))+';\n',encoding='utf-8')
print('Extracted A-type vector source, 30 chart positions and the supplied remote photograph.')
