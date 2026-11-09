# Renders one preview PNG per category of the open rocket-parts.blend (workbench, textured by the parts palette).
import bpy, os, math
from mathutils import Vector

OUT = r"C:\Users\devsuki\AppData\Local\Temp\claude\daaea25a8ed138fa\362a6738-e6cd-4191-9fa0-fe258d796f75\scratchpad\prev"
os.makedirs(OUT, exist_ok=True)
pal = bpy.data.images.load(r"C:\Users\devsuki\space-agency\Assets\Assets\Models\Parts\parts-palette.png", check_existing=True)
pal.colorspace_settings.name = "sRGB"
mat = bpy.data.materials["Base"]
mat.use_nodes = True
nt = mat.node_tree
nt.nodes.clear()
tex = nt.nodes.new("ShaderNodeTexImage"); tex.image = pal; tex.interpolation = "Closest"
bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled"); out = nt.nodes.new("ShaderNodeOutputMaterial")
nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"]); nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])

sc = bpy.context.scene
sc.render.engine = "BLENDER_WORKBENCH"
sc.display.shading.light = "STUDIO"; sc.display.shading.color_type = "TEXTURE"
sc.display.shading.show_cavity = False
sc.render.resolution_x, sc.render.resolution_y = 1400, 560
sc.render.film_transparent = False
sc.world = sc.world or bpy.data.worlds.new("W")
sc.world.color = (0.62, 0.7, 0.8)
cam_data = bpy.data.cameras.new("PrevCam"); cam_data.type = "ORTHO"
cam = bpy.data.objects.new("PrevCam", cam_data); sc.collection.objects.link(cam); sc.camera = cam
cam.rotation_euler = (math.radians(78), 0, math.radians(12))

cats = {}
for ob in bpy.data.objects:
    if ob.type == "MESH":
        cats.setdefault(ob.get("cat") or "", []).append(ob)
import json
meta = {m["id"]: m for m in json.load(open(r"C:\Users\devsuki\space-agency\Assets\Data\Parts\parts-catalog.json"))["parts"]}
by = {}
for ob in bpy.data.objects:
    if ob.type == "MESH" and ob.name in meta:
        by.setdefault(meta[ob.name]["category"], []).append(ob)
done = []
from mathutils import Euler
for cat, lst in by.items():
    for ob in bpy.data.objects:
        if ob.type == "MESH":
            ob.hide_render = ob not in lst
    pts = [ob.matrix_world @ Vector(c) for ob in lst for c in ob.bound_box]
    mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    target = (mn + mx) / 2
    cam.rotation_euler = Euler((math.radians(82), 0, math.radians(14)), "XYZ")
    d = cam.rotation_euler.to_matrix() @ Vector((0, 0, -1))
    cam.location = target - d * 60
    w, h = mx.x - mn.x, mx.z - mn.z
    cam_data.ortho_scale = max(w * 1.12, h * 1.12 * 1400 / 560)
    cam_data.clip_end = 400
    sc.render.filepath = os.path.join(OUT, cat + ".png")
    bpy.ops.render.render(write_still=True)
    done.append(cat)
for ob in bpy.data.objects:
    ob.hide_render = False
result = {"rendered": done}
