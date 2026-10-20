#!/usr/bin/env python3
"""Build the town's homes in Blender and bake them for the Godot client.

Chunky, hand-painted-looking houses in the Northgard spirit: thick pillowy thatch and shingle roofs with deep eaves, heavy
timber frames standing proud of the plaster, footings of fat irregular stones, stone chimneys. Every model is made here from
code (our own art, no downloads), so it can be rebuilt or tweaked at any time.

Blender runs headless as a Python module: `python3 -m pip install bpy` (Blender from PyPI, no account), then
`python3 tools/blender_houses.py` writes godot/assets/models/crafted.bin in the same layout as kenney.bin
(see tools/kenney_bake.py): one sRGB colour per triangle. The "painting" is baked into those colours: ambient occlusion
traced against the model and the ground, a warm-to-dark gradient up the walls, and a little brush jitter per face.
Roof faces are written as (255, 0, shade): the client paints them in each house's roof colour (TownView.RoofPaint).

Models are in lot units (a house fills about 0.8 of a lot), y up, front facing +z, origin at the middle of the footprint
on the ground, as the client draws its houses.

Usage: python3 tools/blender_houses.py [--preview out.png]
"""
import math, os, random, struct, sys

import bpy  # first: it makes bmesh and mathutils importable
import bmesh
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "godot", "assets", "models", "crafted.bin")

# Colours (sRGB). ROOF is a marker the client repaints; WINDOW glass lights up at night.
PAL = {
    "plaster": (0.92, 0.86, 0.72),
    "plaster2": (0.88, 0.80, 0.64),
    "timber": (0.52, 0.36, 0.22),
    "timber2": (0.58, 0.41, 0.25),
    "stone": (0.70, 0.66, 0.59),
    "stone2": (0.52, 0.49, 0.45),
    "stone3": (0.78, 0.73, 0.64),
    "door": (0.42, 0.27, 0.16),
    "iron": (0.22, 0.21, 0.20),
    "shutter": (0.33, 0.42, 0.30),
    "shutter2": (0.55, 0.30, 0.20),
    "window": (0.23, 0.19, 0.15),
    "linen": (0.90, 0.87, 0.78),
    "dye": (0.62, 0.30, 0.24),
    "hedge": (0.30, 0.45, 0.22),
    "lawn": (0.42, 0.56, 0.28),
    "path": (0.76, 0.70, 0.56),
    "gold": (0.91, 0.72, 0.23),
    "ROOF": None,
}
FLAGS = {"window": 1}


class Model:
    """One model under construction: faces with a material name each."""

    def __init__(self, name, seed):
        self.name = name
        self.rng = random.Random(seed)
        self.bm = bmesh.new()
        self.mat = self.bm.faces.layers.int.new("mat")
        self.names = []

    def mat_id(self, m):
        if m not in self.names:
            self.names.append(m)
        return self.names.index(m)

    def merge(self, part, mat, xf):
        """Copy a part's faces into the model under a transform, all in one material."""
        mid = self.mat_id(mat)
        vmap = {}
        for v in part.verts:
            vmap[v] = self.bm.verts.new(xf @ v.co)
        for f in part.faces:
            nf = self.bm.faces.new([vmap[v] for v in f.verts])
            nf[self.mat] = mid
        part.free()

    # ---- primitives (Blender axes: x right, y back, z up; the front is -y) ----

    def box(self, size, at, mat, rot=0.0, bevel=0.0, seg=1, taper=0.0, tilt=(0.0, 0.0), rough=0.0):
        """A box standing on `at` (its base centre), size (x, y, z), turned `rot` about z, sides drawn in by `taper` toward the top,
        its corners knocked about by `rough` (a share of its size) so it reads as a lumpy stone."""
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1.0)
        for v in bm.verts:
            x, y, z = v.co
            s = 1 - taper * (z + 0.5)
            j = [1 + (self.rng.random() - 0.5) * 2 * rough for _ in range(3)]
            v.co = Vector((x * size[0] * s * j[0], y * size[1] * s * j[1], (z + 0.5) * size[2] * j[2]))
        if bevel > 0:
            bmesh.ops.bevel(bm, geom=list(bm.edges), offset=min(bevel, min(size) * 0.45), segments=seg, profile=0.5, affect="EDGES")
        xf = Matrix.Translation(Vector(at)) @ Matrix.Rotation(rot, 4, "Z") @ Matrix.Rotation(tilt[0], 4, "X") @ Matrix.Rotation(tilt[1], 4, "Y")
        self.merge(bm, mat, xf)

    def prism(self, w, d, h, at, mat, ridge_x=True):
        """A gable end: a triangular prism under a roof, ridge along x (or y)."""
        bm = bmesh.new()
        if ridge_x:
            pts = [(-w / 2, -d / 2, 0), (w / 2, -d / 2, 0), (w / 2, d / 2, 0), (-w / 2, d / 2, 0), (-w / 2, 0, h), (w / 2, 0, h)]
            faces = [(0, 1, 2, 3), (0, 4, 5, 1), (3, 2, 5, 4), (0, 3, 4), (1, 5, 2)]
        else:
            pts = [(-w / 2, -d / 2, 0), (w / 2, -d / 2, 0), (w / 2, d / 2, 0), (-w / 2, d / 2, 0), (0, -d / 2, h), (0, d / 2, h)]
            faces = [(0, 1, 2, 3), (1, 4, 5, 2), (0, 3, 5, 4), (0, 4, 1), (3, 2, 5)]
        vs = [bm.verts.new(p) for p in pts]
        for f in faces:
            bm.faces.new([vs[i] for i in f])
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        self.merge(bm, mat, Matrix.Translation(Vector(at)))

    def roof(self, w, d, rise, at, over=0.07, thick=0.06, ridge_x=True, sag=0.022, puff=1.0):
        """A pillowy gable roof: two thick slabs with rounded edges meeting at the ridge, sagging a touch in the middle, and a ridge cap."""
        half = d / 2 + over
        slope = math.hypot(half, rise + over * rise / (d / 2))
        pitch = math.atan2(rise + over * rise / (d / 2), half)
        length = w + over * 2
        for side in (-1, 1):
            bm = bmesh.new()
            bmesh.ops.create_cube(bm, size=1.0)
            bmesh.ops.subdivide_edges(bm, edges=[e for e in bm.edges if abs(e.verts[0].co.x - e.verts[1].co.x) > 0.5], cuts=2, use_grid_fill=True)
            for v in bm.verts:
                x, y, z = v.co
                # Lay the slab along its slope: u along the ridge, s down the slope (0 at the ridge), t through its thickness.
                u, s, t = x * length, (y + 0.5) * slope, z * thick * puff
                droop = -sag * math.sin(math.pi * (x + 0.5))
                v.co = Vector((u, s, t + droop))
            bmesh.ops.bevel(bm, geom=list(bm.edges), offset=thick * 0.4 * puff, segments=1, profile=0.6, affect="EDGES")
            # Turn the slab so it runs down from the ridge on this side.
            lay = Matrix.Rotation(-pitch, 4, "X")
            if side < 0:
                lay = Matrix.Rotation(math.pi, 4, "Z") @ lay
            if not ridge_x:
                lay = Matrix.Rotation(math.pi / 2, 4, "Z") @ lay
            top = Vector(at) + Vector((0, 0, rise + thick * 0.5))
            xf = Matrix.Translation(top) @ lay
            self.merge(bm, "ROOF", xf)
            # Courses of thatch or shingle stepping down the slope, and a fat rolled eave: they catch the light from above.
            for k in (1, 2, 3):
                strip = bmesh.new()
                bmesh.ops.create_cube(strip, size=1.0)
                sk = slope * k / 4.2
                for v in strip.verts:
                    x, y, z = v.co
                    v.co = Vector((x * (length - 0.02), sk + y * slope * 0.06, thick * 0.5 + (z + 0.5) * thick * 0.35 - sag * math.sin(math.pi * (x + 0.5))))
                self.merge(strip, "ROOF", xf)
            eave = bmesh.new()
            bmesh.ops.create_cube(eave, size=1.0)
            for v in eave.verts:
                x, y, z = v.co
                v.co = Vector((x * (length + 0.01), slope - thick * 0.25 + y * thick * 1.1, z * thick * 1.45 - sag * math.sin(math.pi * (x + 0.5))))
            bmesh.ops.bevel(eave, geom=list(eave.edges), offset=thick * 0.45, segments=1, profile=0.6, affect="EDGES")
            self.merge(eave, "ROOF", xf)
        cap = (length + 0.02, thick * 1.6, thick * 1.1) if ridge_x else (thick * 1.6, length + 0.02, thick * 1.1)
        self.box(cap, (at[0], at[1], at[2] + rise + thick * 0.2), "ROOF", bevel=thick * 0.5, seg=2)

    def stones(self, w, d, h, at, ring=True, count=None):
        """A footing of fat irregular stones round a footprint (or a heap when not a ring)."""
        n = count or int((w + d) * 2 / 0.13)
        for i in range(n):
            t = i / n
            per = 2 * (w + d)
            p = t * per
            if p < w:
                x, y = -w / 2 + p, -d / 2
            elif p < w + d:
                x, y = w / 2, -d / 2 + (p - w)
            elif p < 2 * w + d:
                x, y = w / 2 - (p - w - d), d / 2
            else:
                x, y = -w / 2, d / 2 - (p - 2 * w - d)
            s = 0.08 + self.rng.random() * 0.05
            self.box((s * 1.3, s, h * (0.85 + self.rng.random() * 0.4)), (at[0] + x, at[1] + y, at[2] - 0.005), self.rng.choice(["stone", "stone2", "stone3"]),
                     rot=self.rng.random() * 0.6 - 0.3, taper=0.25, rough=0.22)

    def frame(self, w, d, h, at, beam=0.03, front_door=False):
        """A heavy timber frame standing proud of a wall block: corner posts, sill and head beams, and a mid post on each long side."""
        x0, x1, y0, y1, z = at[0] - w / 2, at[0] + w / 2, at[1] - d / 2, at[1] + d / 2, at[2]
        o = beam * 0.3
        for (x, y) in ((x0, y0), (x1, y0), (x1, y1), (x0, y1)):
            self.box((beam * 1.25, beam * 1.25, h), (x, y, z), "timber")
        for zz in (z + h - beam,):
            self.box((w + beam, beam, beam), (at[0], y0 - o, zz), "timber")
            self.box((w + beam, beam, beam), (at[0], y1 + o, zz), "timber")
            self.box((beam, d + beam, beam), (x0 - o, at[1], zz), "timber")
            self.box((beam, d + beam, beam), (x1 + o, at[1], zz), "timber")
        if w > 0.45:
            if not front_door:
                self.box((beam * 0.9, beam * 0.9, h), (at[0], y0 - o, z), "timber2")
            self.box((beam * 0.9, beam * 0.9, h), (at[0], y1 + o, z), "timber2")

    def window(self, at, face, w=0.075, h=0.09, shutters=True):
        """A small deep-set window on a wall facing `face` ('front', 'back', 'left', 'right'): glass, a sill and shutters."""
        rot = {"front": 0, "back": math.pi, "left": -math.pi / 2, "right": math.pi / 2}[face]
        m = Matrix.Translation(Vector(at)) @ Matrix.Rotation(rot, 4, "Z")

        def put(size, off, mat, bevel=0.0):
            p = m @ Vector(off)
            self.box(size if face in ("front", "back") else (size[1], size[0], size[2]), (p.x, p.y, p.z), mat, bevel=bevel)

        put((w + 0.03, 0.03, h + 0.03), (0, -0.004, -0.015), "timber")
        put((w, 0.03, h), (0, -0.012, 0), "window")
        put((w + 0.05, 0.035, 0.018), (0, -0.018, -0.022), "timber2")
        if shutters:
            c = self.rng.choice(["shutter", "shutter2", "door"])
            for sx in (-1, 1):
                put((w * 0.5, 0.012, h + 0.01), (sx * (w * 0.75 + 0.012), -0.02, -0.005), c)

    def door(self, at, w=0.11, h=0.18):
        self.box((w + 0.035, 0.03, h + 0.02), (at[0], at[1] - 0.004, at[2]), "timber")
        self.box((w, 0.03, h), (at[0], at[1] - 0.012, at[2]), "door")
        for zz in (0.3, 0.7):
            self.box((w * 0.9, 0.034, 0.012), (at[0], at[1] - 0.014, at[2] + h * zz), "iron")

    def chimney(self, at, h=0.32):
        z = at[2]
        while z < at[2] + h:
            s = 0.085 + self.rng.random() * 0.02
            hh = 0.05 + self.rng.random() * 0.02
            self.box((s, s, hh), (at[0] + (self.rng.random() - 0.5) * 0.01, at[1] + (self.rng.random() - 0.5) * 0.01, z), self.rng.choice(["stone", "stone2", "stone3"]), rot=self.rng.random() * 0.3, rough=0.15)
            z += hh * 0.92
        self.box((0.1, 0.1, 0.025), (at[0], at[1], z), "stone2")

    # ---- painting and export ----

    def bake(self):
        """Triangulate and paint each triangle: base colour, brush jitter, a gradient up the walls, and traced ambient occlusion."""
        bm = self.bm
        bmesh.ops.triangulate(bm, faces=bm.faces)
        bm.normal_update()
        tree = BVHTree.FromBMesh(bm)
        top = max(v.co.z for v in bm.verts)
        dirs = []
        for i in range(14):
            a, b = i * 2.39996, (i + 0.5) / 14
            r = math.sqrt(b)
            dirs.append(Vector((math.cos(a) * r, math.sin(a) * r, math.sqrt(1 - b))))
        tris = []
        for f in bm.faces:
            name = self.names[f[self.mat]]
            c = f.calc_center_median()
            n = f.normal
            # A frame round the normal for the hemisphere.
            t = n.orthogonal().normalized()
            bt = n.cross(t)
            hit = 0
            for d in dirs:
                ray = t * d.x + bt * d.y + n * d.z
                o = c + n * 0.003
                if ray.z < 0 and o.z + ray.z * 0.18 < 0:
                    hit += 1
                    continue
                if tree.ray_cast(o, ray, 0.18)[0] is not None:
                    hit += 1
            occ = hit / len(dirs)
            ao = 1 - 0.45 * occ
            grad = 0.86 + 0.2 * min(1.0, max(0.0, c.z / max(top, 1e-3)))
            jit = 0.93 + 0.14 * self.rng.random()
            shade = max(0.0, min(1.0, ao * grad * jit))
            if name == "ROOF":
                # Light the roof by facing: the sunward slope brighter, eaves under-shadowed.
                rgb = (255, 0, int(255 * max(0.0, min(1.0, shade * (0.85 + 0.25 * max(0.0, n.z))))))
            else:
                base = PAL[name]
                warm = (1.03, 1.0, 0.95) if n.z > 0.3 else (1.0, 1.0, 1.02)
                rgb = tuple(int(255 * max(0.0, min(1.0, base[k] * shade * warm[k]))) for k in range(3))
            vs = [v.co.copy() for v in f.verts]
            tris.append((vs, rgb, FLAGS.get(name, 0)))
        return tris


def write(models, path):
    with open(path, "wb") as out:
        out.write(b"KNY1")
        out.write(struct.pack("<I", len(models)))
        for name, tris in models:
            nb = name.encode()
            out.write(struct.pack("<B", len(nb)) + nb)
            out.write(struct.pack("<I", len(tris)))
            for vs, _, _ in tris:
                # Blender (x, y, z) with front -y to the game's (x, y up, z) with front +z.
                for v in vs:
                    out.write(struct.pack("<3f", v.x, v.z, -v.y))
            for _, rgb, flag in tris:
                out.write(struct.pack("<4B", rgb[0], rgb[1], rgb[2], flag))


# ---------- the homes ----------


def walls(m, w, d, h, z, mat="plaster", frame=True, door=False, windows=(("front", 2), ("left", 1), ("right", 1), ("back", 1)), at=(0, 0)):
    """A wall block with its frame, door and windows, base at height z."""
    m.box((w, d, h), (at[0], at[1], z), mat)
    if mat.startswith("stone"):
        # Dressed stones standing a little proud, so the wall reads as stone and not paint.
        for i in range(int((w + d) * h * 60)):
            face = m.rng.choice(("front", "back", "left", "right"))
            sx, sz = 0.05 + m.rng.random() * 0.03, 0.035 + m.rng.random() * 0.015
            u, zz = (m.rng.random() - 0.5) * 0.9, z + m.rng.random() * (h - sz)
            c = m.rng.choice(("stone", "stone2", "stone3"))
            if face in ("front", "back"):
                m.box((sx, 0.012, sz), (at[0] + u * w, at[1] + (-d / 2 - 0.004 if face == "front" else d / 2 + 0.004), zz), c)
            else:
                m.box((0.012, sx, sz), (at[0] + (-w / 2 - 0.004 if face == "left" else w / 2 + 0.004), at[1] + u * d, zz), c)
    if frame:
        m.frame(w, d, h, (at[0], at[1], z), front_door=door)
    if door:
        m.door((at[0] + w * 0.12, at[1] - d / 2, z), h=min(0.19, h * 0.75))
    for face, n in windows:
        for i in range(n):
            if face in ("front", "back"):
                span = w
                x = at[0] - span / 2 + span * (i + 1) / (n + 1)
                if door and face == "front":
                    x = at[0] - w * 0.25 if i == 0 else at[0] + w * 0.38
                y = at[1] - d / 2 if face == "front" else at[1] + d / 2
                m.window((x, y, z + h * 0.42), face)
            else:
                y = at[1] - d / 2 + d * (i + 1) / (n + 1)
                x = at[0] - w / 2 if face == "left" else at[0] + w / 2
                m.window((x, y, z + h * 0.42), face)


def house(seed):
    """A cottage: fat stone footing, framed plaster walls, a deep pillowy roof and a stone chimney."""
    m = Model("cc/house", seed)
    w, d = 0.66, 0.54
    m.stones(w + 0.04, d + 0.04, 0.07, (0, 0, 0))
    walls(m, w, d, 0.32, 0.05, door=True, windows=(("front", 2), ("left", 1), ("right", 1), ("back", 1)))
    m.prism(w, d, 0.36, (0, 0, 0.37), "plaster2")
    m.roof(w, d, 0.36, (0, 0, 0.37), over=0.085, thick=0.065)
    m.chimney((w / 2 - 0.1, 0.1, 0.4), h=0.42)
    return m


def timber_house(seed):
    """Two storeys: a stone ground floor and a jettied timber upper floor under a steep roof."""
    m = Model("cc/timber_house", seed)
    w, d = 0.66, 0.6
    m.stones(w + 0.04, d + 0.04, 0.06, (0, 0, 0))
    walls(m, w, d, 0.3, 0.04, mat="stone3", frame=False, door=True, windows=(("front", 2), ("left", 1), ("right", 1)))
    m.box((w + 0.1, d + 0.1, 0.035), (0, 0, 0.34), "timber")
    walls(m, w + 0.07, d + 0.07, 0.28, 0.375, windows=(("front", 2), ("left", 1), ("right", 1), ("back", 2)))
    m.prism(w + 0.07, d + 0.07, 0.36, (0, 0, 0.655), "plaster2")
    m.roof(w + 0.07, d + 0.07, 0.36, (0, 0, 0.655), over=0.08, thick=0.06)
    m.chimney((-w / 2 + 0.06, 0.12, 0.62), h=0.44)
    return m


def narrow(m, x, w, d, floors, seed_off, front_gable=True):
    """One narrow house of a row: stone ground floor, jettied framed storeys, gable to the street."""
    m.stones(w + 0.02, d + 0.02, 0.05, (x, 0, 0), count=int((w + d) * 2 / 0.12))
    z = 0.03
    walls(m, w, d, 0.26, z, mat="stone3", frame=False, door=True, windows=(("front", 1),), at=(x, 0))
    z += 0.26
    for f in range(1, floors):
        jut = 0.018 * f
        m.box((w + 0.02, d + 0.03 + jut * 2, 0.025), (x, -jut, z), "timber")
        walls(m, w, d, 0.24, z + 0.02, windows=(("front", 1), ("back", 1)), at=(x, -jut))
        z += 0.26
    jut = 0.018 * (floors - 1)
    m.prism(w, d, 0.24, (x, -jut, z), "plaster2", ridge_x=False)
    m.roof(d, w, 0.24, (x, -jut, z), over=0.05, thick=0.05, ridge_x=False, sag=0.006)
    m.chimney((x + w * 0.22, d * 0.3, z), h=0.3)


def townhouse(seed):
    """A pair of narrow gable-fronted houses, one a storey taller."""
    m = Model("cc/townhouse", seed)
    narrow(m, -0.2, 0.36, 0.7, 3, 0)
    narrow(m, 0.2, 0.36, 0.7, 2, 1)
    return m


def tall_house(seed):
    """Three narrow houses of four and five storeys wall to wall, filling the lot to its edges."""
    m = Model("cc/tall_house", seed)
    for i, floors in enumerate((4, 5, 4)):
        narrow(m, (i - 1) * 0.3, 0.29, 0.86, floors, i)
    return m


def tenement(seed):
    """A crowded three-storey timber block with a washing line across its front."""
    m = Model("cc/tenement", seed)
    w, d = 0.76, 0.7
    m.stones(w + 0.04, d + 0.04, 0.06, (0, 0, 0))
    z = 0.04
    for f in range(3):
        walls(m, w, d, 0.26, z, mat="plaster" if f else "stone3", frame=f > 0, door=f == 0, windows=(("front", 3 if f else 2), ("left", 2), ("right", 2), ("back", 2)))
        z += 0.27
    m.prism(w, d, 0.22, (0, 0, z), "plaster2")
    m.roof(w, d, 0.22, (0, 0, z), over=0.07, thick=0.055)
    m.chimney((-0.26, 0.15, z - 0.02), h=0.36)
    m.chimney((0.26, 0.2, z - 0.02), h=0.3)
    m.box((w + 0.06, 0.006, 0.006), (0, -d / 2 - 0.09, 0.52), "timber")
    for i in range(5):
        x = -0.3 + i * 0.15
        m.box((0.09, 0.004, 0.1), (x, -d / 2 - 0.09, 0.42), "linen" if i % 2 == 0 else "dye")
    return m


def manor(seed):
    """A manor: a broad two-storey stone house with a porch tower behind a hedged garden."""
    m = Model("cc/manor", seed)
    m.box((0.98, 0.98, 0.012), (0, 0, 0), "lawn")
    for x in (-0.46, 0.46):
        m.box((0.07, 0.95, 0.12), (x, 0, 0), "hedge", bevel=0.03, seg=2)
    for x in (-0.29, 0.29):
        m.box((0.34, 0.07, 0.12), (x, -0.46, 0), "hedge", bevel=0.03, seg=2)
    m.box((0.12, 0.3, 0.014), (0, -0.32, 0), "path")
    w, d = 0.84, 0.46
    at = (0, 0.18)
    m.stones(w + 0.04, d + 0.04, 0.06, (at[0], at[1], 0))
    walls(m, w, d, 0.28, 0.04, mat="stone3", frame=False, windows=(("front", 4), ("left", 1), ("right", 1), ("back", 3)), at=at)
    walls(m, w, d, 0.26, 0.32, mat="stone", frame=False, windows=(("front", 4), ("left", 1), ("right", 1), ("back", 3)), at=at)
    m.prism(w, d, 0.3, (at[0], at[1], 0.58), "stone2")
    m.roof(w, d, 0.3, (at[0], at[1], 0.58), over=0.06, thick=0.055)
    m.chimney((-0.36, 0.26, 0.55), h=0.42)
    m.chimney((0.36, 0.26, 0.55), h=0.42)
    tw = 0.24
    walls(m, tw, tw, 0.3, 0.02, mat="stone3", frame=False, door=True, windows=(("front", 0),), at=(0, -0.08))
    walls(m, tw, tw, 0.28, 0.32, mat="stone", frame=False, windows=(("front", 1), ("left", 1), ("right", 1)), at=(0, -0.08))
    walls(m, tw, tw, 0.24, 0.6, mat="stone", frame=False, windows=(("front", 1),), at=(0, -0.08))
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=4, radius1=tw * 0.78, radius2=0.0, depth=0.3)
    for v in bm.verts:
        v.co.z += 0.15
    m.merge(bm, "ROOF", Matrix.Translation(Vector((0, -0.08, 0.84))) @ Matrix.Rotation(math.pi / 4, 4, "Z"))
    m.box((0.03, 0.03, 0.12), (0, -0.08, 1.12), "gold", bevel=0.01)
    return m


BUILDERS = [house, timber_house, townhouse, tenement, tall_house, manor]


def preview(models, path):
    """Render every model in a row with a warm sun, for a quick look (not used by the game)."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    x = 0.0
    for name, tris in models:
        mesh = bpy.data.meshes.new(name)
        verts, faces, cols = [], [], []
        for vs, rgb, flag in tris:
            i = len(verts)
            verts.extend([(v.x + x, v.y, v.z) for v in vs])
            faces.append((i, i + 1, i + 2))
            c = (0.66, 0.4, 0.26) if rgb[0] == 255 and rgb[1] == 0 else tuple(k / 255 for k in rgb)
            if rgb[0] == 255 and rgb[1] == 0:
                c = tuple(k * rgb[2] / 255 for k in c)
            cols.append(c)
        mesh.from_pydata(verts, [], faces)
        layer = mesh.color_attributes.new("col", "BYTE_COLOR", "CORNER")
        for p in mesh.polygons:
            for li in p.loop_indices:
                layer.data[li].color = (*cols[p.index], 1.0)
        mat = bpy.data.materials.new("m")
        mat.use_nodes = True
        nt = mat.node_tree
        attr = nt.nodes.new("ShaderNodeVertexColor")
        attr.layer_name = "col"
        bsdf = nt.nodes["Principled BSDF"]
        bsdf.inputs["Roughness"].default_value = 1.0
        nt.links.new(attr.outputs["Color"], bsdf.inputs["Base Color"])
        mesh.materials.append(mat)
        ob = bpy.data.objects.new(name, mesh)
        scene.collection.objects.link(ob)
        x += 1.15
    bpy.ops.mesh.primitive_plane_add(size=20, location=(x / 2, 0, 0))
    ground = bpy.context.object
    gm = bpy.data.materials.new("g")
    gm.use_nodes = True
    gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.3, 0.45, 0.18, 1)
    ground.data.materials.append(gm)
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
    sun.data.energy = 4
    sun.rotation_euler = (math.radians(50), 0, math.radians(35))
    scene.collection.objects.link(sun)
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = x + 0.4
    cam.location = (x / 2 - 0.55, -6, 4.2)
    cam.rotation_euler = (math.radians(56), 0, 0)
    scene.collection.objects.link(cam)
    scene.camera = cam
    world = bpy.data.worlds.new("w")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.55, 0.62, 0.72, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.8
    scene.world = world
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 16
    scene.cycles.device = "CPU"
    scene.render.resolution_x, scene.render.resolution_y = 1800, 520
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def main():
    models = []
    for i, build in enumerate(BUILDERS):
        m = build(1000 + i)
        tris = m.bake()
        models.append((m.name, tris))
        print(f"{m.name}: {len(tris)} triangles")
    write(models, OUT)
    print(f"wrote {OUT}")
    if "--preview" in sys.argv:
        preview(models, sys.argv[sys.argv.index("--preview") + 1])


if __name__ == "__main__":
    main()
