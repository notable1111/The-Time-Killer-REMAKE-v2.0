import json
d = json.load(open(r'Assets\Resources\Assets\Maps\CastleWing.ldtk', encoding='utf-8'))
print('levels:', [(l['identifier'], l['uid'], l['worldX'], l['worldY'], l['pxWid'], l['pxHei']) for l in d['levels']])
def_names = [x['identifier'] for x in d['defs']['layers']]
for lv in d['levels']:
    names = [li['__identifier'] for li in lv['layerInstances']]
    assert names == def_names, lv['identifier'] + ' order mismatch'
    for li in lv['layerInstances']:
        assert li['__cWid'] * 16 == lv['pxWid'] and li['__cHei'] * 16 == lv['pxHei'], 'dims mismatch'
        if li['__type'] == 'IntGrid':
            assert len(li['intGridCsv']) == li['__cWid'] * li['__cHei'], 'csv len'
        for t in li.get('gridTiles', []):
            cx, cy = t['px'][0] // 16, t['px'][1] // 16
            assert 0 <= cx < li['__cWid'] and 0 <= cy < li['__cHei'], 'tile OOB'
            assert t['d'][0] == cy * li['__cWid'] + cx, 'bad d'
    fl = next(l for l in lv['layerInstances'] if l['__identifier'] == 'Floor')
    col = next(l for l in lv['layerInstances'] if l['__identifier'] == 'Collision')
    W, H = col['__cWid'], col['__cHei']
    floor = {(t['px'][0] // 16, t['px'][1] // 16) for t in fl['gridTiles']}
    gaps = []
    for (cx, cy) in floor:
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                c = (cx + dx, cy + dy)
                if c in floor: continue
                ok = 0 <= c[0] < W and 0 <= c[1] < H and col['intGridCsv'][c[1] * W + c[0]] == 1
                if not ok: gaps.append(c)
    onfloor = [1 for (cx, cy) in floor if col['intGridCsv'][cy * W + cx] != 0]
    ents = next(l for l in lv['layerInstances'] if l['__identifier'] == 'Entities')['entityInstances']
    print(' ', lv['identifier'], ': floor', len(floor), '| gaps', len(gaps), '| on-floor', len(onfloor), '| zones', len(ents))
print('nextUid:', d['nextUid'])
