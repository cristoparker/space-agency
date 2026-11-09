# Renders a 128x128 transparent icon for every rocket part of the open rocket-parts.blend into Assets/Assets/Graphics/PartIcons.
#   python ArtSource/blender_exec.py ArtSource/render_icons.py   (run build_parts.py first)
import bpy, os, math, json
from mathutils import Vector, Euler

ROOT = r"C:\Users\devsuki\space-agency"
OUT = os.path.join(ROOT, "Assets", "Assets", "Graphics", "PartIcons")
os.makedirs(OUT, exist_ok=True)

pal = bpy.data.images.load(os.path.join(ROOT, "Assets", "Assets", "Models", "Parts", "parts-palette.png"), check_existing=True)
mat = bpy.data.materials["Base"]
mat.use_nodes = True
nt = mat.node_tree
nt.nodes.clear()
tex = nt.nodes.new("ShaderNodeTexImage"); tex.image = pal; tex.interpolation = "Closest"
bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled"); out = nt.nodes.new("ShaderNodeOutputMaterial")
nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"]); nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])

sc = bpy.context.scene
sc.render.engine = "BLENDER_WORKBENCH"
sc.display.shading.light = "STUDIO"; sc.display.shading.color_type = "TEXTURE"; sc.display.shading.show_cavity = False
sc.render.resolution_x = sc.render.resolution_y = 128
sc.render.film_transparent = True
sc.render.image_settings.file_format = "PNG"; sc.render.image_settings.color_mode = "RGBA"
cam_data = bpy.data.cameras.get("IconCam") or bpy.data.cameras.new("IconCam"); cam_data.type = "ORTHO"; cam_data.clip_end = 500
cam = bpy.data.objects.get("IconCam") or bpy.data.objects.new("IconCam", cam_data)
if cam.name not in sc.collection.objects: sc.collection.objects.link(cam)
sc.camera = cam
cam.rotation_euler = Euler((math.radians(68), 0, math.radians(32)), "XYZ")

meta = {m["id"]: m for m in json.load(open(os.path.join(ROOT, "Assets", "Data", "Parts", "parts-catalog.json")))["parts"]}
done = []
for name in meta:
    ob = bpy.data.objects.get(name)
    if ob is None: continue
    for o in bpy.data.objects:
        if o.type == "MESH": o.hide_render = (o is not ob)
    pts = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
    mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    target = (mn + mx) / 2
    d = cam.rotation_euler.to_matrix() @ Vector((0, 0, -1))
    cam.location = target - d * 80
    cam_data.ortho_scale = max((mx - mn).length * 0.92, 0.5)
    sc.render.filepath = os.path.join(OUT, name + ".png")
    bpy.ops.render.render(write_still=True)
    done.append(name)
for o in bpy.data.objects:
    if o.type == "MESH": o.hide_render = False
result = {"icons": len(done)}
