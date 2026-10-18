#!/usr/bin/env python3
"""Bake Kenney CC0 models into one compact file the Godot client loads at start.

Kenney's kits (https://kenney.nl, CC0) ship as GLB files that colour faces from a small swatch
texture or from flat material colours. The client draws everything flat-shaded with vertex
colours and merges models into instanced batches, so this script flattens each listed model to
triangles with one sRGB colour per triangle and writes them to godot/assets/models/kenney.bin:

    "KNY1", u32 model count, then per model:
        u8 name length, name (utf-8), u32 triangle count,
        triangles x 9 float32 (positions, y up, metres as Kenney made them),
        triangles x 4 u8 (r, g, b, flag), flag reserved (0).

Usage: python3 tools/kenney_bake.py [--inspect]
The kits are downloaded into a cache folder outside the repository; only the baked file and the
licence texts are committed. The list of models is MODELS below.
"""
import io, json, os, struct, sys, urllib.request, zipfile

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CACHE = os.environ.get("KENNEY_CACHE", os.path.join(os.path.expanduser("~"), ".cache", "kenney"))
OUT = os.path.join(ROOT, "godot", "assets", "models", "kenney.bin")
LICENSES = os.path.join(ROOT, "godot", "assets", "models")

KITS = {
    "town": ("https://kenney.nl/media/pages/assets/fantasy-town-kit/efe948d309-1754222374/kenney_fantasy-town-kit_2.0.zip", "Models/GLB format/"),
    "castle": ("https://kenney.nl/media/pages/assets/castle-kit/a395102d20-1711543616/kenney_castle-kit.zip", "Models/GLB format/"),
    "nature": ("https://kenney.nl/media/pages/assets/nature-kit/37ac38a37b-1677698939/kenney_nature-kit.zip", "Models/GLTF format/"),
}

MODELS = {
    "town": """wall-wood wall-wood-door wall-wood-window-shutters wall-wood-window-small wall-wood-window-glass wall-wood-detail-cross
        wall wall-door wall-window-shutters wall-window-glass wall-window-stone wall-detail-cross wall-block wall-half wall-wood-half
        roof roof-gable roof-gable-end roof-gable-top roof-gable-detail roof-high roof-high-gable roof-high-gable-end roof-high-point roof-point roof-window
        chimney chimney-base chimney-top banner-red banner-green stall stall-red stall-green stall-bench cart cart-high lantern
        fountain-round fountain-round-detail windmill watermill wheel fence fence-gate hedge planks pillar-wood pillar-stone overhang
        rock-large rock-small rock-wide
        tree tree-high tree-high-round tree-crooked fountain-square fountain-square-detail stall-stool hedge-large poles""".split(),
    "castle": """tower-square tower-square-base tower-square-mid tower-square-mid-windows tower-square-top tower-square-top-roof tower-square-roof
        tower-hexagon-base tower-hexagon-mid tower-hexagon-roof tower-hexagon-top tower-base tower-top wall wall-half wall-corner wall-doorway
        wall-pillar gate flag flag-banner-long flag-pennant flag-wide siege-catapult siege-ram siege-tower siege-ballista
        rocks-large rocks-small tree-log""".split(),
    "nature": """tree_pineTallA_detailed tree_pineTallB_detailed tree_pineDefaultA tree_pineRoundA tree_pineSmallA tree_oak tree_detailed tree_default
        tree_fat tree_cone tree_small tree_thin tree_oak_dark tree_detailed_dark tree_fat_darkh tree_plateau tree_simple
        rock_largeA rock_largeB rock_largeC rock_tallA rock_tallB rock_tallC rock_smallA rock_smallB stone_largeA stone_largeB stone_tallA stone_tallB
        cliff_large_rock cliff_large_stone cliff_cave_rock cliff_cave_stone cliff_block_rock
        tent_detailedOpen tent_detailedClosed tent_smallOpen campfire_logs campfire_stones log log_large log_stack log_stackLarge stump_old stump_round
        crops_wheatStageB crops_cornStageC crops_dirtDoubleRow crop_pumpkin fence_simple fence_planks statue_ring statue_head
        plant_bush plant_bushLarge mushroom_redGroup lily_large bridge_wood bridge_stone platform_stone sign pot_small
        flower_redA flower_yellowA flower_purpleA flower_redB flower_yellowB plant_bushDetailed tree_default tree_pineRoundB tree_tall
        statue_obelisk statue_column statue_block pot_large campfire_bricks fence_simpleHigh""".split(),
}

COMP = {5120: np.int8, 5121: np.uint8, 5122: np.int16, 5123: np.uint16, 5125: np.uint32, 5126: np.float32}
WIDTH = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}


def kit_zip(kit):
    url, _ = KITS[kit]
    os.makedirs(CACHE, exist_ok=True)
    path = os.path.join(CACHE, url.rsplit("/", 1)[1])
    if not os.path.exists(path):
        print("downloading", url)
        urllib.request.urlretrieve(url, path)
    return zipfile.ZipFile(path)


def read_glb(data):
    jlen = struct.unpack_from("<I", data, 12)[0]
    gltf = json.loads(data[20:20 + jlen])
    off = 20 + jlen
    blen = struct.unpack_from("<I", data, off)[0]
    return gltf, data[off + 8:off + 8 + blen]


def accessor(gltf, buf, i):
    a = gltf["accessors"][i]
    v = gltf["bufferViews"][a["bufferView"]]
    dt = np.dtype(COMP[a["componentType"]])
    n, w = a["count"], WIDTH[a["type"]]
    start = v.get("byteOffset", 0) + a.get("byteOffset", 0)
    stride = v.get("byteStride", dt.itemsize * w)
    raw = np.frombuffer(buf, dtype=np.uint8, count=stride * (n - 1) + dt.itemsize * w, offset=start)
    out = np.lib.stride_tricks.as_strided(raw, shape=(n, dt.itemsize * w), strides=(stride, 1)).copy()
    arr = out.view(dt).reshape(n, w)
    if a.get("normalized"):
        arr = arr.astype(np.float32) / np.iinfo(dt).max
    return arr.astype(np.float32) if w > 1 else arr.reshape(n)


def node_matrix(n):
    if "matrix" in n:
        return np.array(n["matrix"], dtype=np.float64).reshape(4, 4).T
    t = np.eye(4)
    if "scale" in n:
        t = np.diag(list(n["scale"]) + [1]) @ t
    if "rotation" in n:
        x, y, z, w = n["rotation"]
        r = np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w), 0],
                      [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w), 0],
                      [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y), 0], [0, 0, 0, 1]])
        t = r @ t
    if "translation" in n:
        m = np.eye(4)
        m[:3, 3] = n["translation"]
        t = m @ t
    return t


def linear_to_srgb(c):
    c = np.clip(c, 0, 1)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * np.power(c, 1 / 2.4) - 0.055)


def bake(zf, prefix, name):
    data = zf.read(prefix + name + ".glb")
    gltf, buf = read_glb(data)
    images = {}
    for i, im in enumerate(gltf.get("images", [])):
        uri = im.get("uri")
        if uri:
            images[i] = np.asarray(Image.open(io.BytesIO(zf.read(prefix + uri))).convert("RGB"), dtype=np.float32) / 255
    tris, cols = [], []

    def walk(ni, parent):
        n = gltf["nodes"][ni]
        m = parent @ node_matrix(n)
        if "mesh" in n:
            for prim in gltf["meshes"][n["mesh"]]["primitives"]:
                pos = accessor(gltf, buf, prim["attributes"]["POSITION"])
                pos = (np.c_[pos, np.ones(len(pos))] @ m.T)[:, :3]
                idx = accessor(gltf, buf, prim["indices"]) if "indices" in prim else np.arange(len(pos))
                idx = idx.astype(np.int64).reshape(-1, 3)
                mat = gltf["materials"][prim["material"]] if "material" in prim else {}
                pbr = mat.get("pbrMetallicRoughness", {})
                factor = np.array(pbr.get("baseColorFactor", [1, 1, 1, 1])[:3], dtype=np.float32)
                tex = pbr.get("baseColorTexture")
                if tex is not None and "TEXCOORD_0" in prim["attributes"]:
                    img = images[gltf["textures"][tex["index"]]["source"]]
                    uv = accessor(gltf, buf, prim["attributes"]["TEXCOORD_0"])
                    c = uv[idx].mean(axis=1)
                    h, w = img.shape[:2]
                    px = np.clip((c[:, 0] % 1) * w, 0, w - 1).astype(int)
                    py = np.clip((c[:, 1] % 1) * h, 0, h - 1).astype(int)
                    col = img[py, px] * linear_to_srgb(factor)
                else:
                    col = np.tile(linear_to_srgb(factor), (len(idx), 1))
                tris.append(pos[idx])
                cols.append(col)
        for c in n.get("children", []):
            walk(c, m)

    scene = gltf["scenes"][gltf.get("scene", 0)]
    for r in scene["nodes"]:
        walk(r, np.eye(4))
    tris = np.concatenate(tris).astype(np.float32)
    cols = np.concatenate(cols)
    rgba = np.c_[np.clip(cols * 255 + 0.5, 0, 255), np.zeros(len(cols))].astype(np.uint8)
    return tris, rgba


def main():
    inspect = "--inspect" in sys.argv
    out = io.BytesIO()
    models = []
    os.makedirs(LICENSES, exist_ok=True)
    for kit, names in MODELS.items():
        zf = kit_zip(kit)
        prefix = next(n for n in zf.namelist() if n.endswith(KITS[kit][1]))
        lic = next(n for n in zf.namelist() if n.endswith("License.txt"))
        with open(os.path.join(LICENSES, f"LICENSE-kenney-{kit}.txt"), "wb") as f:
            f.write(zf.read(lic))
        for name in names:
            tris, rgba = bake(zf, prefix, name)
            models.append((f"{kit}/{name}", tris, rgba))
            if inspect:
                p = tris.reshape(-1, 3)
                print(f"{kit}/{name:32} tris {len(tris):5}  min {np.round(p.min(0), 2)}  max {np.round(p.max(0), 2)}")
    out.write(b"KNY1")
    out.write(struct.pack("<I", len(models)))
    for name, tris, rgba in models:
        b = name.encode()
        out.write(struct.pack("<B", len(b)))
        out.write(b)
        out.write(struct.pack("<I", len(tris)))
        out.write(tris.tobytes())
        out.write(rgba.tobytes())
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "wb") as f:
        f.write(out.getvalue())
    print(f"wrote {OUT}: {len(models)} models, {sum(len(t) for _, t, _ in models)} triangles, {out.tell() // 1024} KB")


if __name__ == "__main__":
    main()
