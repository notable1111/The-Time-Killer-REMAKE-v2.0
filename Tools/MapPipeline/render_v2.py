import json
from PIL import Image, ImageDraw

d = json.load(open(r'Assets\Resources\Assets\Maps\CastleWing.ldtk', encoding='utf-8'))
OUT = r'C:\Users\Asus\AppData\Local\Temp\claude\D--The-Time-Killer-Remake\4ddea370-6588-4838-8b6d-669c8c573cb2\scratchpad'
S = 8

LABELS = {  # per level: label -> script-space (x, y) anchor; ground uses cx=x+12, cy=37-y
    'CastleWing': [
        ('HALL', 6, 5), ('GUARDROOM', 29, 5), ('GREAT CHAMBER', 24, 25),
        ('CHAPEL', -8, 25), ('KITCHEN (new)', 28, -8), ('ARMORY (new)', 42, 5),
        ('LIBRARY (new)', 43, 25), ('SERVANT PASSAGE (new)', 11, -6.5),
    ],
    'UpperGallery': [('GALLERY WALKWAY', 2, 1.5), ('STAIR LANDING', 20, 4)],
    'Undercroft': [('CRYPT', 5, 5), ('ALCOVES', 16, 7)],
}
ANCHOR = {'CastleWing': (12, 37), 'UpperGallery': (3, 12), 'Undercroft': (3, 17)}

for lv in d['levels']:
    li = {l['__identifier']: l for l in lv['layerInstances']}
    W, H = lv['pxWid'] // 16, lv['pxHei'] // 16
    img = Image.new('RGB', (W * S, H * S), (16, 16, 20))
    dr = ImageDraw.Draw(img)
    floor = {(t['px'][0] // 16, t['px'][1] // 16) for t in li['Floor']['gridTiles']}
    for (cx, cy) in floor:
        dr.rectangle([cx * S, cy * S, cx * S + S - 1, cy * S + S - 1], fill=(45, 95, 50))
    csv = li['Collision']['intGridCsv']
    for cy in range(H):
        for cx in range(W):
            if csv[cy * W + cx]:
                dr.rectangle([cx * S, cy * S, cx * S + S - 1, cy * S + S - 1], fill=(165, 48, 48))
    for e in li['Entities']['entityInstances']:
        x, y = e['px'][0] // 16 * S, e['px'][1] // 16 * S
        w, h = e['width'] // 16 * S, e['height'] // 16 * S
        dr.rectangle([x, y, x + w - 1, y + h - 1], outline=(250, 205, 40))
    img = img.resize((W * S * 2, H * S * 2), Image.NEAREST)
    dr = ImageDraw.Draw(img)
    ox, k = ANCHOR[lv['identifier']]
    for (txt, x, y) in LABELS.get(lv['identifier'], []):
        px, py = (x + ox) * S * 2, (k - y) * S * 2
        dr.text((px + 1, py + 1), txt, fill=(0, 0, 0))
        dr.text((px, py), txt, fill=(255, 240, 200))
    path = f'{OUT}\\v2_{lv["identifier"]}.png'
    img.save(path)
    print('saved', path, img.size)
