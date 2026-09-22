"""Rebuild MetroMap.png without generative editing.
Dependencies: PyMuPDF 1.28.2, pypdf, Pillow.
The original PDF is a third-party work; see MetroMap-SourceNotice.txt before distribution.
Only intermediate stations skipped INSIDE a delivery route are removed.
All stations outside the travelled segments remain, including unused directions/lines.
Assertions protect every retained label/mark and all pixels outside deletion footprints.
"""
import sys, re, json, math, io, hashlib
from pathlib import Path
import pymupdf as f
from pypdf import PdfReader, PdfWriter
from pypdf.generic import ContentStream
from PIL import Image, ImageChops, ImageDraw

ROOT = Path(__file__).parent
SOURCE = ROOT / 'OriginalMap.pdf'
assert hashlib.sha256(SOURCE.read_bytes()).hexdigest() == '625ea7c69fb91f27a50441d1b12c3c180da99ce642ad8856e8d0534472538c2b', 'Source PDF changed: review station coordinates before rebuilding.'
ASSETS = next(p for p in ROOT.parents if p.name == 'Assets')
GAMEPLAY = ASSETS / '_Project' / 'Scenes' / 'Gameplay'
CATALOG = GAMEPLAY / 'Data' / 'SliceDeliveryCatalog.asset'
OUTPUT = (ROOT.parent if ROOT.parent.name == 'KorailMetroMap' else GAMEPLAY / 'UI') / 'MetroMap.png'
catalog_text = CATALOG.read_text(encoding='utf-8').split('  legacyOrders:')[0]
REQUIRED = set(re.findall(r'^      label: (.+)$', catalog_text, re.MULTILINE))
assert REQUIRED, 'No active delivery stops found in catalog'
# Physical station order read from the preserved official PDF. These are display-only
# reference segments, never replacements for the game's stop sequence or transit code.
# Line 2 is circular: use the shorter arc between adjacent planned stops.
LINE_ORDER = {
    '수인분당선': '청량리 왕십리 서울숲 압구정로데오 강남구청 선정릉 선릉 한티 도곡 구룡 개포동 대모산입구 수서 복정 가천대 태평 모란 야탑 이매 서현 수내 정자 미금 오리 죽전 보정 구성 신갈 기흥 상갈 청명 영통 망포 매탄권선 수원시청 매교 수원 고색 오목천 어천 야목 사리 한대앞 중앙 고잔 초지 안산 신길온천 정왕 오이도 달월 월곶 소래포구 인천논현 호구포 남동인더스파크 원인재 연수 송도 인하대 숭의 신포 인천',
    '8호선': '복정 장지 문정 가락시장 송파 석촌 잠실 몽촌토성 강동구청 천호 암사 암사역사공원 장자호수공원 구리 동구릉 다산 별내',
    '3호선': '도곡 매봉 양재 남부터미널 교대 고속터미널 잠원 신사 압구정 옥수 금호 약수 동대입구 충무로 을지로3가 종로3가',
    '1호선': '청량리 회기 외대앞 신이문 석계 광운대',
    '경춘선': '별내 퇴계원 사릉 금곡 평내호평 천마산 마석 대성리 청평 상천 가평 굴봉산 백양리 강촌 김유정 남춘천 춘천',
    '2호선': '강남 역삼 선릉 삼성 종합운동장 잠실새내 잠실 잠실나루 강변 구의 건대입구 성수 뚝섬 한양대 왕십리 상왕십리 신당 동대문역사문화공원 을지로4가 을지로3가 을지로입구 시청 충정로 아현 이대 신촌 홍대입구 합정 당산 영등포구청 문래 신도림 대림 구로디지털단지 신대방 신림 봉천 서울대입구 낙성대 사당 방배 서초 교대',
    '4호선': '동대문역사문화공원 충무로 명동 회현 서울역',
    '신분당선': '정자 미금 동천 수지구청 성복 상현 광교중앙 광교',
    '9호선': '선정릉 언주 신논현 사평 고속터미널 신반포 구반포 동작 흑석 노들 노량진 샛강 여의도',
    '5호선': '여의도 여의나루 마포 공덕',
    '공항철도': '공덕 홍대입구 디지털미디어시티 마곡나루 김포공항 계양 검암 청라국제도시 영종 운서 공항화물청사 인천공항1터미널 인천공항2터미널',
}
SKIP = set()
for order in re.split(r'^  - id: ', catalog_text, flags=re.MULTILINE)[1:]:
    stops = re.findall(r'^      label: (.+)\n      line: (.+)$', order, re.MULTILINE)
    assert len(stops) >= 2, 'Delivery route missing stops'
    for (start, _), (end, line) in zip(stops, stops[1:]):
        sequence = LINE_ORDER[line].split()
        assert start in sequence and end in sequence, ('Extend verified reference segment', line, start, end)
        a, b = sequence.index(start), sequence.index(end)
        lo, hi = sorted((a, b))
        between = sequence[lo + 1:hi]
        if line == '2호선':
            other_arc = sequence[hi + 1:] + sequence[:lo]
            assert len(between) != len(other_arc), 'Ambiguous circular route'
            if len(other_arc) < len(between): between = other_arc
        SKIP.update(between)
# A stop used by ANY of the 13 deliveries remains visible, even if another skips it.
SKIP -= REQUIRED
assert SKIP.isdisjoint(REQUIRED)
doc = f.open(SOURCE); page = doc[0]
spans = [s for b in page.get_text('dict')['blocks'] if b['type']==0 for l in b['lines'] for s in l['spans']]
station_spans = [s for s in spans if 520 < s['bbox'][1] < 2020 and abs(s['size']-10)<.3 and re.search('[가-힣]',s['text']) and s['color'] != 16777215]
stations = []
for s in station_spans:
    r=f.Rect(s['bbox']); name=s['text'].strip()
    if not name: continue
    previous=next((x for x in stations if abs(x['rect'].x0-r.x0)<2 and 7<r.y0-x['last_y']<11),None)
    if previous:
        previous['name']+=name; previous['rect']|=r; previous['parts'].append(s);previous['last_y']=r.y0
    else: stations.append(dict(name=name,rect=r,parts=[s],last_y=r.y0))
# Display line breaks and the source's optional word spacing are not changes to text.
for x in stations:
    x['name']=x['name'].replace(' ',''); x['label_rect']=f.Rect(x['rect'])
all_names = set(x['name'] for x in stations)
assert SKIP <= all_names, ('Skipped label missing from source', sorted(SKIP-all_names))
KEEP = all_names - SKIP
found=REQUIRED&all_names
print('required stations',len(found),'skip inside routes',len(SKIP),'retained source labels',sum(x['name'] in KEEP for x in stations))
print('skip list',', '.join(sorted(SKIP)))
if '--inspect' in sys.argv:
    print('\n'.join(f"{x['name']} {tuple(round(v,1) for v in x['rect'])}" for x in stations if x['name'] in SKIP))
    print('sizes', sorted(set(round(s['size'],3) for s in spans if 520<s['bbox'][1]<2020)))
    sys.exit()
assert not REQUIRED-found, sorted(REQUIRED-found)

def distance(a,b):
    return math.hypot(max(a.x0-b.x1,b.x0-a.x1,0), max(a.y0-b.y1,b.y0-a.y1,0))

def label_owner(rect):
    # English/subnames are immediately beneath a Korean name. Never cross to a nearby row.
    def score(st):
        r=st['rect'];dy=rect.y0-r.y1
        if dy < -3: return 10000+abs(dy)
        dx=abs((rect.x0+rect.x1-r.x0-r.x1)*.5)
        overlap=min(rect.x1,r.x1)-max(rect.x0,r.x0)
        return max(0,dy)+dx*.3+(25 if overlap<0 else 0)
    return min(stations,key=score)

station_part_ids={id(s):x for x in stations for s in x['parts']}
delete_rects=[]; keep_rects=[]; names=[]
for s in spans:
    rect=f.Rect(s['bbox'])
    if not 520<rect.y0<2020: continue
    if s['color']==16777215 or s['size']<5.5: continue
    if id(s) in station_part_ids: st=station_part_ids[id(s)]
    elif abs(s['size']-5.91)<.1 or ('(' in s['text'] or ')' in s['text']): st=label_owner(rect)
    else: continue
    st['label_rect']|=rect
    (keep_rects if st['name'] in KEEP else delete_rects).append(rect)
    if st['name'] in KEEP: names.append((st['name'],s['text']))

drawings=page.get_drawings()
markers=[]
for d in drawings:
    r=d['rect'];fill=d['fill']
    if not 520<r.y0<2020 or not fill or min(fill)<.97: continue
    commands=[item[0] for item in d['items']]
    half_dot=(len(commands)==5 and commands.count('c')==2 and commands.count('l')==3 and 1.4<min(r.width,r.height)<4.5)
    transfer=(d['type']=='fs' and 8<min(r.width,r.height)<11 and max(r.width,r.height)<65)
    if not (half_dot or transfer): continue
    st=min(stations,key=lambda x:distance(r,x['rect'])+.02*abs(r.x0+r.x1-x['rect'].x0-x['rect'].x1))
    # Ambiguous adjacent-label placements checked against the original PDF.
    # In particular, off-route Banpo must not be mistaken for skipped Sapyeong.
    for px,py,name in [(846.294,1357.286,'사평'),(841.954,1331.924,'반포'),(1107.490,1508.420,'가락시장')]:
        if abs((r.x0+r.x1)/2-px)<1 and abs((r.y0+r.y1)/2-py)<1:
            st=next(x for x in stations if x['name']==name)
    markers.append((d,st))
print('markers',len(markers),'kept',sum(s['name'] in KEEP for _,s in markers))
assert REQUIRED<=set(s['name'] for _,s in markers)
assert SKIP<=set(s['name'] for _,s in markers), ('Skipped station marker missing', sorted(SKIP-set(s['name'] for _,s in markers)))
delete_markers=[d['rect']+(-.9,-.9,.9,.9) for d,s in markers if s['name'] not in KEEP]
keep_markers=[d['rect']+(-.9,-.9,.9,.9) for d,s in markers if s['name'] in KEEP]
paint_logs=[(i,t,f.Rect(r)) for i,(t,r) in enumerate(page.get_bboxlog()) if t in ('fill-path','stroke-path')]
remove_seq=set()
for d in drawings:
    if any(r.contains(d['rect']) for r in delete_markers) and not any(r.intersects(d['rect']) for r in keep_markers):
        remove_seq.add(d['seqno'])
        if d['type']=='fs': remove_seq.add(d['seqno']+1)
reader=PdfReader(SOURCE); writer=PdfWriter();writer.add_page(reader.pages[0]);content=writer.pages[0].get_contents()
j=0;ops=[]
for args,op in content.operations:
    if op in (b'f',b'F',b'f*',b'S',b's',b'B',b'B*',b'b',b'b*'):
        seq,kind,rect=paint_logs[j];j+=1
        assert (op in (b'f',b'F',b'f*')) == (kind=='fill-path'),(j,op,kind)
        if seq in remove_seq: op=b'n'
    ops.append((args,op))
assert j==len(paint_logs),(j,len(paint_logs))
content.operations=ops;writer.pages[0].replace_contents(content)
vector_buffer=io.BytesIO();writer.write(vector_buffer)
edited=f.open(stream=vector_buffer.getvalue(),filetype='pdf');ep=edited[0]
for rect in delete_rects: ep.add_redact_annot(rect, fill=False, cross_out=False)
ep.apply_redactions(images=0, graphics=0, text=0)
scale=2
before=Image.frombytes('RGB',(int(page.get_pixmap(matrix=f.Matrix(scale,scale)).width),int(page.get_pixmap(matrix=f.Matrix(scale,scale)).height)),page.get_pixmap(matrix=f.Matrix(scale,scale)).samples)
pix=ep.get_pixmap(matrix=f.Matrix(scale,scale));after=Image.frombytes('RGB',(pix.width,pix.height),pix.samples)
# Where the original split a rail at a transfer symbol, extend only matching source-color
# runs into that deleted symbol's footprint. No rail outside this footprint is touched.
def colored(c): return max(c)-min(c)>25 and min(c)<230
pixels=after.load(); repairs=0
for d,st in markers:
    if st['name'] in KEEP or d['type']!='fs':continue
    rr=d['rect']+(-.9,-.9,.9,.9)
    x0,y0,x1,y1=math.floor(rr.x0*scale),math.floor(rr.y0*scale),math.ceil(rr.x1*scale),math.ceil(rr.y1*scale)
    # Only same-row / same-column continuations. Never invent diagonal joins or connect different lines.
    for y in range(y0,y1+1):
        left,right=pixels[x0-2,y],pixels[x1+2,y]
        if colored(left) and max(abs(a-b)for a,b in zip(left,right))<8:
            for x in range(x0,x1+1):
                if min(pixels[x,y])>245:pixels[x,y]=left;repairs+=1
    for x in range(x0,x1+1):
        top,bottom=pixels[x,y0-2],pixels[x,y1+2]
        if colored(top) and max(abs(a-b)for a,b in zip(top,bottom))<8:
            for y in range(y0,y1+1):
                if min(pixels[x,y])>245:pixels[x,y]=top;repairs+=1
print('line-gap pixels filled',repairs)
mask=Image.new('L',before.size);brush=ImageDraw.Draw(mask)
for r in delete_rects+delete_markers:
    brush.rectangle([math.floor(r.x0*scale)-3,math.floor(r.y0*scale)-3,math.ceil(r.x1*scale)+3,math.ceil(r.y1*scale)+3],fill=255)
dr,dg,db=ImageChops.difference(before,after).split()
diff=ImageChops.lighter(ImageChops.lighter(dr,dg),db).point(lambda v:255 if v else 0)
outside=ImageChops.subtract(diff,mask)
assert outside.getbbox() is None, ('changed outside permitted areas',outside.getbbox())
for r in keep_rects:
    box=(math.ceil(r.x0*scale),math.ceil(r.y0*scale),math.floor(r.x1*scale),math.floor(r.y1*scale))
    assert ImageChops.difference(before.crop(box),after.crop(box)).getbbox() is None,('kept label changed',r)
for r in keep_markers:
    box=(math.ceil(r.x0*scale),math.ceil(r.y0*scale),math.floor(r.x1*scale),math.floor(r.y1*scale))
    assert ImageChops.difference(before.crop(box),after.crop(box)).getbbox() is None,('kept marker changed',r)
after.save(OUTPUT)
print('deleted label spans',len(delete_rects),'removed draw operators',len(remove_seq),'outside changes ZERO','retained label rects',len(keep_rects))
