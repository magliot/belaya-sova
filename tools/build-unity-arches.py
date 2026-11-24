"""Bake the nine painted gate designs to closed meshes for Mountain.unity.

Run from the repository root with Python, Pillow and NumPy. Coordinates describe
individual pieces on the supplied design sheet. Fronts use the original paint;
the snow lip, sides, underside and back of each piece have their own geometry.
"""
from collections import defaultdict
from pathlib import Path
import math
import struct

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / "art/course-kit/arches-reference.png"
RACE = ROOT / "unity/OwlSlope/Assets/Resources/Race"
ATLAS = "arch-paint"
SHEET = Image.open(ART).convert("RGB")
MASK = Image.new("L", SHEET.size)
INK = ImageDraw.Draw(MASK)
SWATCH = {
    "wood": (129, 79, 40), "umber": (83, 61, 46), "blue": (37, 69, 125),
    "stone": (112, 119, 130), "iron": (51, 62, 77), "silver": (136, 156, 183),
    "gold": (234, 164, 42), "orange": (219, 90, 24), "teal": (43, 125, 134),
    "green": (36, 80, 54), "wine": (110, 40, 46), "snow": (226, 236, 247),
    "cyan": (95, 226, 255), "amber": (255, 195, 77)
}

# Local pixel coordinates; material, front relief (metres), depth, polygon.
def part(material, relief, depth, outline):
    pts = [tuple(map(float, pair.split(","))) for pair in outline.split()]
    return material, relief, depth, pts

def pair(material, relief, depth, left, right):
    return [part(material, relief, depth, s) for s in (left, right)]

# For each design the inner edges of the supports define the old clear width.
def designs():
    return [
        (122, 395, 226, 5.02, 89, [
            *pair("wood", .28, 1.08,
                  "94,89 122,83 116,145 112,207 86,210",
                  "400,84 424,91 433,207 406,210"),
            part("blue", .42, .95, "93,86 105,76 151,64 213,51 228,37 245,31 263,30 280,35 296,47 304,54 348,59 396,69 420,80 427,92 402,103 399,132 369,119 322,108 273,103 232,103 188,107 148,116 123,132 118,102 94,107"),
            *pair("blue", .58, 1.23, "94,137 121,139 119,151 92,150", "400,140 427,137 429,150 401,154"),
            *pair("stone", .64, 1.36, "80,204 86,200 113,199 122,205 123,224 77,224", "398,205 404,201 430,200 439,206 441,223 396,224")
        ]),
        (133, 380, 225, 5.05, 94, [
            *pair("wood", .24, 1.27, "96,74 119,64 126,74 118,188 88,203", "383,65 409,68 422,200 391,191"),
            *pair("wood", .32, .78, "111,119 150,77 160,77 122,129", "346,75 357,77 401,120 397,129"),
            part("green", .38, .74, "122,91 252,37 385,88 392,112 122,113"),
            part("wood", .56, 1.08, "74,85 92,77 245,22 255,21 267,26 427,82 438,91 436,102 414,102 256,39 94,101 77,110"),
            part("wood", .52, .93, "99,110 410,109 413,117 100,120"),
            *pair("wood", .67, 1.38, "95,68 101,58 119,54 128,60 123,119 96,118", "380,60 390,54 409,59 416,65 413,120 388,119"),
            *pair("stone", .74, 1.58, "67,211 74,199 86,195 88,188 98,183 113,182 124,188 130,198 133,217 126,224 66,224", "378,219 381,202 388,192 391,185 406,180 421,185 430,193 435,205 445,216 445,225 384,224")
        ]),
        (115, 403, 233, 5.04, 94, [
            *pair("gold", .30, .96, "91,93 113,90 105,199 78,203", "407,92 429,90 442,201 416,200"),
            *pair("gold", .32, .74, "103,127 163,85 171,87 111,139", "344,86 350,85 419,128 417,138"),
            part("gold", .42, 1.04, "92,93 117,87 142,80 152,69 160,66 173,64 182,67 190,66 200,58 210,48 220,44 229,45 240,50 247,50 256,46 266,38 273,33 282,31 291,33 301,38 312,50 319,57 328,61 340,61 349,58 357,57 364,58 375,66 388,78 406,84 426,88 431,101 402,105 373,91 350,83 303,78 252,77 201,79 155,84 119,98 93,104"),
            part("blue", .56, .78, "156,85 202,78 247,75 291,75 338,78 364,83 363,114 315,110 269,108 218,110 158,115"),
            *pair("iron", .68, 1.19, "74,184 109,184 112,215 68,215", "408,184 442,183 450,216 407,215"),
            *pair("stone", .72, 1.36, "59,215 68,211 107,210 115,216 116,233 56,234", "403,216 411,210 448,210 458,215 462,232 403,232")
        ]),
        (111, 404, 225, 5.06, 83, [
            part("orange", .36, 1.03, "131,57 376,56 388,107 120,108"),
            part("iron", .46, .77, "150,64 366,64 375,101 140,101"),
            *pair("orange", .56, 1.22, "43,220 111,53 119,48 141,49 148,54 103,215 95,222", "366,56 371,50 398,50 406,56 471,221 415,222"),
            *pair("iron", .69, 1.40, "61,192 103,193 108,221 49,222", "407,193 450,193 464,221 411,222")
        ]),
        (100, 408, 244, 5.22, 84, [
            *pair("silver", .33, 1.17, "46,215 70,114 98,101 83,214", "408,105 434,110 457,212 423,216"),
            *pair("blue", .36, .89, "79,155 156,104 170,106 83,176", "342,106 355,104 430,155 429,170"),
            part("blue", .42, .90, "164,75 252,31 267,38 341,76 357,104 150,104"),
            part("silver", .54, 1.18, "75,114 253,16 258,17 434,111 436,124 256,34 81,129"),
            part("silver", .50, .96, "126,103 373,103 381,110 118,111"),
            *pair("iron", .65, 1.28, "76,113 101,102 97,127 71,137", "412,105 436,115 441,136 416,127"),
            *pair("amber", .75, .19, "82,117 96,112 93,122 80,128", "418,113 432,119 435,127 419,122"),
            *pair("stone", .71, 1.60, "48,215 56,204 91,204 101,212 98,239 41,244", "407,213 414,205 448,204 461,214 468,238 414,240")
        ]),
        (130, 386, 239, 5.07, 83, [
            part("iron", .37, 1.20, "107,56 136,51 383,51 407,56 397,111 123,111"),
            part("iron", .43, .86, "139,64 376,63 387,101 132,101"),
            *pair("iron", .45, 1.32, "70,187 104,64 111,54 145,54 109,187", "368,55 399,55 411,64 444,185 408,186"),
            *pair("orange", .58, .48, "73,176 101,96 116,96 92,177", "406,96 420,96 448,177 430,176"),
            part("silver", .65, .55, "235,44 256,22 279,44 257,67"),
            *pair("stone", .73, 1.88, "31,223 37,207 52,200 59,181 71,175 92,175 104,178 118,176 130,188 128,231 91,235 72,233 46,237", "386,230 392,197 403,181 420,175 439,178 452,191 462,204 476,210 482,235 445,235 429,232 404,235")
        ]),
        (107, 409, 229, 5.11, 80, [
            part("iron", .38, .87, "149,58 365,58 370,98 153,98"),
            *pair("blue", .46, 1.19, "57,212 87,105 94,92 145,54 160,52 163,65 117,106 102,209", "354,54 373,55 421,92 429,105 458,209 417,211 401,110 351,66"),
            part("blue", .50, 1.02, "145,55 156,52 363,52 375,56 370,63 149,64"),
            *pair("cyan", .59, .17, "80,169 96,107 103,103 88,166 84,169", "416,104 423,108 440,166 436,170 433,168"),
            *pair("cyan", .59, .17, "145,59 152,55 162,55 160,59 151,60 148,63", "357,55 367,55 374,60 373,63 368,61 359,60"),
            *pair("stone", .66, 1.47, "51,216 57,209 94,205 107,212 106,226 48,228", "409,214 416,207 450,205 463,211 470,226 413,228")
        ]),
        (107, 398, 226, 5.12, 81, [
            *pair("wood", .29, 1.20, "77,83 106,81 98,203 66,207", "401,81 430,84 440,203 407,203"),
            part("wine", .45, 1.06, "74,79 90,67 124,53 170,39 215,31 250,28 294,31 339,39 389,55 422,70 431,82 428,95 411,91 407,130 377,114 341,103 296,97 256,95 210,97 173,103 137,115 100,133 100,90 75,98"),
            *pair("gold", .58, 1.32, "76,124 101,125 98,138 73,136", "407,126 431,123 434,138 408,139"),
            part("gold", .73, .48, "234,23 250,22 256,6 263,21 279,22 267,33 270,48 257,40 244,48 247,34"),
            *pair("stone", .68, 1.52, "56,201 64,193 90,190 105,197 106,221 56,225", "398,201 405,193 437,191 446,199 451,219 401,226")
        ]),
        (119, 399, 239, 5.14, 81, [
            *pair("umber", .33, 1.22, "76,209 99,86 123,68 127,77 112,211", "384,71 410,83 438,210 405,214"),
            *pair("wood", .38, .84, "109,103 165,71 175,73 121,113", "337,73 347,71 406,103 397,113"),
            part("teal", .46, .84, "176,75 258,36 351,75 351,100 176,100"),
            part("umber", .49, 1.23, "81,77 96,69 287,13 300,13 309,24 91,98 81,97"),
            part("wood", .61, 1.23, "254,34 271,27 423,72 438,82 438,94 423,96"),
            *pair("teal", .72, 1.36, "120,71 141,66 130,111 107,118", "390,65 408,71 419,115 397,109"),
            *pair("stone", .77, 1.66, "49,222 60,215 75,214 82,207 92,204 110,204 121,214 118,232 96,234 80,232 69,237 45,235", "402,226 409,213 424,206 439,205 453,211 465,225 478,229 490,240 449,238 429,231 412,233")
        ])
    ]


def cross(a, b, c):
    return (b[0]-a[0])*(c[1]-a[1]) - (b[1]-a[1])*(c[0]-a[0])


def triangulate(points):
    # The gables, crescent lintels and star have concave profiles.
    active = list(range(len(points)))
    area = sum(p[0]*q[1]-q[0]*p[1] for p,q in zip(points, points[1:]+points[:1]))
    if area < 0:
        active.reverse()
    result = []
    while len(active) > 2:
        for i, b in enumerate(active):
            a, c = active[i-1], active[(i+1) % len(active)]
            turn = cross(points[a], points[b], points[c])
            if abs(turn) < .001:
                active.pop(i)
                break
            if turn < 0 or any(min(cross(points[a],points[b],points[j]),
                                   cross(points[b],points[c],points[j]),
                                   cross(points[c],points[a],points[j])) >= -.0001
                               for j in active if j not in (a,b,c)):
                continue
            result.append((a,b,c))
            active.pop(i)
            break
        else:
            raise ValueError(f"Intersecting outline at {points}")
    return result


class Gate:
    def __init__(self, stage, half, start=False):
        left, right, self.ground, centre_height, sign_y, pieces = designs()[stage]
        self.mid = (left+right)*.5
        self.sx = 2*half/(right-left)
        self.sy = (5.55 if start else centre_height*1.34)/(self.ground-sign_y)
        self.ox, self.oy = stage % 3 * 512, (0,342,683)[stage//3]
        self.stage = stage
        self.buffers = defaultdict(lambda: ([], [], [], [], []))
        for material, relief, depth, path in pieces:
            self.solid(material, relief, depth, path)
        if stage == 7:
            for x,y in [(85,86),(114,74),(139,64),(165,56),(194,50),(222,46),
                        (286,46),(316,50),(344,57),(371,65),(397,74),(424,86),(85,131),(422,131)]:
                # Proud glass lenses on the wooden valance and on the post collars.
                ring = [(x+3*math.cos(i*math.pi/4), y+3.5*math.sin(i*math.pi/4)) for i in range(8)]
                self.solid("amber", .74, .19, ring, lamp=True)
        self.fit_opening(half)

    def fit_opening(self, half):
        # The old support interiors are at ±5.82 m on the start and ±6.74 m
        # on transitions. Fit the bevels and slanted posts, including the part
        # that can be exposed on the low side of a bank, to the same corridor.
        low, high = -1.2, 2.4
        edges = []
        for positions, _, _, _, indices in self.buffers.values():
            tri = np.asarray(positions)[indices].reshape((-1,3,3))
            following = tri[:,[1,2,0]]
            within = (tri[:,:,1]>=low) & (tri[:,:,1]<=high)
            edges.extend(tri[:,:,0][within])
            for height in (low,high):
                hit = (tri[:,:,1]<height) != (following[:,:,1]<height)
                a,b = tri[hit],following[hit]
                t = (height-a[:,1])/(b[:,1]-a[:,1])
                edges.extend(a[:,0]+(b[:,0]-a[:,0])*t)
        left = half/min(-x for x in edges if x<0)
        right = half/min(x for x in edges if x>0)
        for positions, _, _, _, _ in self.buffers.values():
            positions[:] = [(x*(left if x<0 else right),y,z) for x,y,z in positions]

    def point(self, xy, z):
        return ((xy[0]-self.mid)*self.sx, (self.ground-xy[1])*self.sy, z)

    def uv(self, xy):
        return ((self.ox+xy[0]+.5)/2048, 1-(self.oy+xy[1]+.5)/1024)

    def face(self, key, points, coords, shades, triangles, normal):
        pos, normals, uv, colors, ix = self.buffers[key]
        base = len(pos)
        pos.extend(points)
        normals.extend([normal]*len(points))
        uv.extend(coords)
        colors.extend(shades)
        ix.extend(base+j for tri in triangles for j in tri)

    def solid(self, mat, relief, depth, points, lamp=False):
        offset = [(self.ox+x,self.oy+y) for x,y in points]
        INK.polygon(offset, fill=255)
        # The buried skirt continues each footing below both sides of the bank.
        # Extend only the lower outline; UVs remain on the last row of painted rock.
        lowest = max(y for x,y in points)
        burial = mat=="stone" or mat=="iron" and self.stage==3
        def vertex(xy,z):
            x,y,z = self.point(xy,z)
            if burial and xy[1]>self.ground-14:
                y -= 1.85*max(0,1-(lowest-xy[1])/8)
            return x,y,z
        is_light = mat in ("amber","cyan") or self.stage==7 and mat=="gold" and depth<.6
        front_key = ("" if lamp else ATLAS, 0, 0, 232 if is_light else (94 if self.stage==6 else 12))
        tri = triangulate(points)
        n = len(points)
        front, back = -relief, depth-relief
        tex = [self.uv(v) for v in points]
        white = [(255,236,154,255) if lamp else (255,255,255,255)]*n
        self.face(front_key, [vertex(v,front) for v in points], tex, white, tri, (0,0,-1))
        self.face((ATLAS,0,1,0), [vertex(v,back) for v in points], tex,
                  [(196,205,217,255)]*n, [t[::-1] for t in tri], (0,0,1))
        winding = 1 if sum(p[0]*q[1]-q[0]*p[1] for p,q in zip(points,points[1:]+points[:1]))>0 else -1
        for j,p in enumerate(points):
            q = points[(j+1)%n]
            a,b = vertex(p,front),vertex(q,front)
            dx,dy = b[0]-a[0],b[1]-a[1]
            norm = math.hypot(dx,dy)
            if norm < 1e-5:
                continue
            normal = (-dy*winding/norm, dx*winding/norm, 0)
            rgb = np.array(SWATCH[mat])
            if normal[1]>.49 and not is_light:
                rgb = np.array(SWATCH["snow"])
            # Separate rolled lip, reveal and exterior wall. Side values depend
            # on facet direction so the cross section reads even in diffuse fog.
            bevel = min(.07,depth*.18)
            ax,ay = normal[0]*bevel,normal[1]*bevel
            rims = [(front,0),(front+bevel,1),(back-bevel,1),(back,0)]
            for (za,k),(zb,l) in zip(rims,rims[1:]):
                verts = [(a[0]+ax*k,a[1]+ay*k,za),(b[0]+ax*k,b[1]+ay*k,za),
                         (b[0]+ax*l,b[1]+ay*l,zb),(a[0]+ax*l,a[1]+ay*l,zb)]
                scale = .86 if k==l else 1.08
                color = tuple(np.clip(rgb*scale,0,255).astype(int))+(255,)
                self.face(("",0,1,8),verts,[(0,0)]*4,[color]*4,[(0,1,2),(0,2,3)],normal)

    def save(self,name):
        with (RACE/"Models"/(name+".bytes")).open("wb") as f:
            def integer(i): f.write(struct.pack("<i",i))
            integer(len(self.buffers))
            for (texture,blend,lit,glow),(pos,norm,uv,col,ix) in self.buffers.items():
                name_bytes = texture.encode()
                integer(len(name_bytes)); f.write(name_bytes)
                for v in (blend,lit,glow,len(pos)): integer(v)
                for data,dtype in [(pos,"<f4"),(np.rint(np.array(norm)*127),"i1"),(uv,"<f4"),(col,"u1")]:
                    f.write(np.asarray(data,dtype=dtype).tobytes())
                integer(len(ix)); f.write(np.asarray(ix,dtype="<i4").tobytes()); integer(0)
        print(name, sum(len(ix)//3 for *_,ix in self.buffers.values()), "triangles")


if __name__ == "__main__":
    for i in range(9):
        Gate(i,6.74).save(f"arch{i}")
    Gate(0,5.82,start=True).save("startArch")
    atlas = Image.new("RGB",(2048,1024),(73,77,85))
    atlas.paste(SHEET,(0,0),MASK.filter(ImageFilter.MaxFilter(9)))
    atlas.save(RACE/"Textures"/(ATLAS+".png"),optimize=True)
