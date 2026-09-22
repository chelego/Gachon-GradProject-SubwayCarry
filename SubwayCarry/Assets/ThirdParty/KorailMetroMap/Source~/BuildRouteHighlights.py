"""Build exact-source delivery overlays; no new railway geometry or gameplay changes.
Requires the same packages as BuildMetroMap.py. The original PDF is read-only.
PNG bytes are decoded one route at a time by the phone UI to bound texture memory.
"""
from pathlib import Path
import hashlib, heapq, math, re, runpy, uuid
from PIL import Image, ImageDraw

ROOT = Path(__file__).parent
g = runpy.run_path(str(ROOT / 'BuildMetroMap.py'))
base = Image.open(g['OUTPUT']).convert('RGB')
base_hash = hashlib.sha256(g['OUTPUT'].read_bytes()).hexdigest()
assets = g['ASSETS']
out = assets / 'ThirdParty' / 'KorailMetroMap' / 'RouteHighlights'

def metadata(path, folder=False):
    meta = Path(str(path) + '.meta')
    if meta.exists():
        return re.search(r'^guid: (\w+)', meta.read_text(), re.M)[1]
    guid = uuid.uuid5(uuid.NAMESPACE_URL, 'SubwayCarry/' + path.relative_to(assets).as_posix()).hex
    importer = 'DefaultImporter' if folder else 'TextScriptImporter'
    meta.write_text('fileFormatVersion: 2\nguid: ' + guid + '\n' + ('folderAsset: yes\n' if folder else '') + importer + ':\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n', encoding='utf-8')
    return guid

for directory in [assets / 'ThirdParty', out.parent, out]:
    directory.mkdir(exist_ok=True)
    metadata(directory, True)

# Exact stroke colors in the preserved PDF, not substitute artwork.
colors = {
    '수인분당선': (.999, .760, .052), '1호선': (.000, .367, .669),
    '2호선': (.041, .697, .350), '3호선': (.958, .489, .189),
    '4호선': (.000, .664, .865), '5호선': (.576, .437, .694),
    '8호선': (.932, .162, .457), '9호선': (.695, .645, .552),
    '경춘선': (.059, .550, .448), '신분당선': (.829, .068, .270),
    '공항철도': (.026, .583, .830),
}

def dist(a, b): return math.hypot(a[0]-b[0], a[1]-b[1])

graphs = {}
for name, color in colors.items():
    nodes, graph, buckets = [], [], {}
    def add(point):
        p = (float(point[0]), float(point[1]))
        key = (math.floor(p[0]/2), math.floor(p[1]/2))
        candidates = [i for x in range(key[0]-1,key[0]+2) for y in range(key[1]-1,key[1]+2) for i in buckets.get((x,y), ())]
        near = min(candidates, key=lambda i: dist(nodes[i], p)) if candidates else None
        if near is not None and dist(nodes[near], p) < .35: return near
        i = len(nodes); nodes.append(p); graph.append({}); buckets.setdefault(key, []).append(i)
        # Joins touching runs of this same line only. Other lines never enter this graph.
        for other in candidates:
            distance = dist(nodes[other], p)
            if distance < 1.2: graph[i][other] = distance; graph[other][i] = distance
        return i
    for drawing in g['drawings']:
        if not drawing['color'] or (drawing['width'] or 0) < 2.3 or drawing['rect'].y0 < 500: continue
        if max(abs(a-b) for a,b in zip(drawing['color'],color)) > .002: continue
        for item in drawing['items']:
            if item[0] not in ('l','c'): continue
            pts = item[1:]
            steps = max(1, math.ceil(sum(dist(a,b) for a,b in zip(pts,pts[1:])) / .8))
            previous = None
            for s in range(steps+1):
                t=s/steps; u=1-t
                if item[0]=='l': point=(u*pts[0].x+t*pts[1].x, u*pts[0].y+t*pts[1].y)
                else: point=tuple(u**3*pts[0][j]+3*u*u*t*pts[1][j]+3*u*t*t*pts[2][j]+t**3*pts[3][j] for j in (0,1))
                current=add(point)
                if previous is not None and current != previous:
                    length=dist(nodes[previous],nodes[current]); graph[previous][current]=length; graph[current][previous]=length
                previous=current
    assert nodes, ('No source strokes',name)
    graphs[name]=(nodes,graph)

def anchor(name, line):
    nodes,_=graphs[line]
    candidates=[(d,s) for d,s in g['markers'] if s['name']==name]
    best=None
    for d,station in candidates:
        r=d['rect']; p=((r.x0+r.x1)/2,(r.y0+r.y1)/2)
        node=min(range(len(nodes)),key=lambda i:dist(nodes[i],p))
        score=dist(nodes[node],p)
        if best is None or score<best[0]:best=(score,node,d,station)
    assert best is not None and best[0]<35, ('Station not on source line',name,line,best and best[0])
    return best

def segment(start,end,line):
    nodes,graph=graphs[line]; a=anchor(start,line); b=anchor(end,line)
    costs={a[1]:0}; previous={}; heap=[(0,a[1])]
    while heap:
        cost,node=heapq.heappop(heap)
        if node==b[1]:break
        if cost>costs[node]:continue
        for other,length in graph[node].items():
            total=cost+length
            if total<costs.get(other,float('inf')):
                costs[other]=total;previous[other]=node;heapq.heappush(heap,(total,other))
    assert b[1] in costs, ('Disconnected source strokes',start,end,line)
    route=[b[1]]
    while route[-1]!=a[1]:route.append(previous[route[-1]])
    return [nodes[i] for i in route],(a,b)

entries=[]
scale=2
for block in re.split(r'^  - id: ',g['catalog_text'],flags=re.M)[1:]:
    order_id=block.splitlines()[0].strip()
    stops=re.findall(r'^      label: (.+)\n      line: (.+)$',block,re.M)
    mask=Image.new('L',base.size); brush=ImageDraw.Draw(mask)
    station_regions=[]
    for (start,_),(end,line) in zip(stops,stops[1:]):
        points,anchors=segment(start,end,line)
        # Reveal the original pixels around the original stroke, never draw replacement lines.
        brush.line([(round(x*scale),round(y*scale)) for x,y in points],fill=255,width=14,joint='curve')
        for _,_,drawing,station in anchors:
            station_regions.extend([drawing['rect'],station['label_rect']])
    for r in station_regions:
        brush.rectangle((math.floor(r.x0*scale)-2,math.floor(r.y0*scale)-2,math.ceil(r.x1*scale)+2,math.ceil(r.y1*scale)+2),fill=255)
    bounds=mask.getbbox(); assert bounds
    overlay=base.crop(bounds).convert('RGBA'); overlay.putalpha(mask.crop(bounds))
    path=out / ('Route_' + order_id + '.bytes')
    overlay.save(path,format='PNG',optimize=True)
    guid=metadata(path)
    # Round-trip validates every revealed RGB pixel against the unchanged base image.
    with Image.open(path) as saved:
        assert saved.convert('RGB').tobytes()==base.crop(bounds).tobytes()
    x,y,right,bottom=bounds
    entries.append('  - orderId: '+order_id+'\n    png: {fileID: 4900000, guid: '+guid+', type: 3}\n    normalizedRect: {x: '+str(x/base.width)+', y: '+str(y/base.height)+', width: '+str((right-x)/base.width)+', height: '+str((bottom-y)/base.height)+'}\n')
    print(order_id,len(stops),'stops',overlay.size,path.stat().st_size,'PNG bytes')
assert len(entries)==13, ('Review route count',len(entries))
catalog=g['CATALOG'].read_text(encoding='utf-8')
catalog=re.sub(r'^  routeHighlights:\n(?:  - orderId:.*\n    png:.*\n    normalizedRect:.*\n)*','',catalog,flags=re.M)
catalog=catalog.replace('  damageAtlas:', '  routeHighlights:\n'+''.join(entries)+'  damageAtlas:',1)
g['CATALOG'].write_text(catalog,encoding='utf-8')
assert hashlib.sha256(g['OUTPUT'].read_bytes()).hexdigest()==base_hash
print('13 cropped highlights built; base map RGB and delivery orders preserved.')
