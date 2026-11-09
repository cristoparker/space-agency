# Model health check: run on a .blend headless:
#   blender -b ArtSource/rocket-parts.blend --python ArtSource/check_models.py [-- --fix]
#   blender -b ArtSource/space-base.blend  --python ArtSource/check_models.py [-- --fix]
# Per object, per connected piece (shell):
#   inconsistent = edges where the two faces wind the same way (one of them faces backwards)
#   inverted     = closed shells whose volume is negative (the whole shell is inside-out)
#   zero / dup   = degenerate or duplicated faces
# --fix recalculates normals outward on every shell that has a problem.
import bpy, bmesh, sys

FIX = "--fix" in sys.argv
rows, problems = [], 0
for ob in sorted(bpy.data.objects, key=lambda o: o.name):
    if ob.type != "MESH":
        continue
    bm = bmesh.new(); bm.from_mesh(ob.data); bm.faces.ensure_lookup_table()
    # connected shells
    seen, shells = set(), []
    for f in bm.faces:
        if f in seen: continue
        stack, shell = [f], []
        seen.add(f)
        while stack:
            cur = stack.pop(); shell.append(cur)
            for e in cur.edges:
                for g in e.link_faces:
                    if g not in seen: seen.add(g); stack.append(g)
        shells.append(shell)
    inconsistent = inverted = zero = dup = 0
    bad_faces = []
    keys = set()
    for shell in shells:
        bad = 0
        for f in shell:
            if f.calc_area() < 1e-9: zero += 1
            k = tuple(sorted(v.index for v in f.verts))
            if k in keys: dup += 1
            keys.add(k)
            for l in f.loops:
                e = l.edge
                if len(e.link_faces) == 2:
                    other = e.link_faces[0] if e.link_faces[1] is f else e.link_faces[1]
                    ol = [x for x in other.loops if x.edge is e][0]
                    if ol.vert is l.vert: bad += 1          # both faces run the same way along the edge
        bad //= 2
        closed = all(len(e.link_faces) == 2 for f in shell for e in f.edges)
        vol = 0.0
        for f in shell:
            vs = [v.co for v in f.verts]
            for i in range(1, len(vs) - 1):
                vol += vs[0].dot(vs[i].cross(vs[i + 1])) / 6.0
        if closed and vol < 0: inverted += 1; bad += 1
        inconsistent += bad
        if bad: bad_faces += shell
    if FIX and bad_faces:
        bmesh.ops.recalc_face_normals(bm, faces=list(set(bad_faces)))
        bm.to_mesh(ob.data); ob.data.update()
    rows.append((ob.name, len(shells), len(bm.faces), inconsistent, inverted, zero, dup))
    problems += inconsistent + inverted + zero + dup
    bm.free()
print("CHECK name shells faces inconsistentEdges invertedShells zeroArea duplicate")
for r in rows:
    if r[3] or r[4] or r[5] or r[6]: print("CHECK %-18s %4d %5d %5d %5d %5d %5d" % r)
print("CHECK objects=%d TOTAL_PROBLEMS=%d" % (len(rows), problems))
