"""
glb 모델 하나로 LOD(멀리서 볼 때 쓰는 가벼운 모델)를 만든다.

    blender -b -P Tools/Blender/make_lods.py -- <모델.glb> [--ratios 0.3 0.08] [--sizes 512 256]

- 결과: Assets/CookieClicker/Models/LOD/<이름>_LOD1.glb, <이름>_LOD2.glb (원본이 LOD0)
- 단계마다: 정점 병합 → 삼각형 줄이기(Decimate) → 새 UV → 원본에서 색·노멀 굽기(베이크)
  단순히 줄이기만 하면 UV 경계가 찢어져 얼룩이 생기므로, 원본 모습을 새 UV에 다시 구워 넣는다.
- 원본과 같은 좌표계로 내보내므로 Unity에서 원본과 같은 위치·크기에 겹쳐 놓으면 된다.
"""
import argparse
import math
import os
import sys
import time

import bmesh
import bpy
from mathutils import Vector

PROJECT_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
DEFAULT_OUT = os.path.join(PROJECT_ROOT, "Assets", "CookieClicker", "Models", "LOD")


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    p = argparse.ArgumentParser()
    p.add_argument("input", help="원본 glb (LOD0)")
    p.add_argument("--ratios", nargs="+", type=float, default=[0.3, 0.08], help="원본 대비 삼각형 비율 (LOD1, LOD2 ...)")
    p.add_argument("--sizes", nargs="+", type=int, default=[512, 256], help="단계별 텍스처 크기")
    p.add_argument("--out", default=DEFAULT_OUT)
    a = p.parse_args(argv)
    if len(a.sizes) < len(a.ratios):
        a.sizes += [a.sizes[-1]] * (len(a.ratios) - len(a.sizes))
    return a


def tri_count(objs):
    total = 0
    for o in objs:
        for poly in o.data.polygons:
            total += len(poly.vertices) - 2
    return total


def select_only(objs, active=None):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = active or (objs[0] if objs else None)


def import_source(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=path)
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    # 부모·노드 변환을 정점에 적용해 둔다 → 내보낼 때 원본과 같은 월드 좌표가 된다.
    select_only(meshes)
    bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    for o in list(bpy.context.scene.objects):
        if o.type != "MESH":
            bpy.data.objects.remove(o, do_unlink=True)
    return meshes


def bounds_diagonal(objs):
    lo = Vector((math.inf,) * 3)
    hi = Vector((-math.inf,) * 3)
    for o in objs:
        for v in o.data.vertices:
            co = o.matrix_world @ v.co
            lo = Vector(map(min, lo, co))
            hi = Vector(map(max, hi, co))
    return (hi - lo).length


def make_low(high, ratio, diag):
    select_only(high)
    bpy.ops.object.duplicate()
    copies = list(bpy.context.selected_objects)
    bpy.context.view_layer.objects.active = copies[0]
    if len(copies) > 1:
        bpy.ops.object.join()
    low = bpy.context.view_layer.objects.active
    low.name = "LOD"

    # UV 경계마다 갈라진 정점을 붙여야 줄일 때 구멍·찢어짐이 덜 생긴다.
    bm = bmesh.new()
    bm.from_mesh(low.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=diag * 1e-5)
    bm.to_mesh(low.data)
    bm.free()

    original = tri_count(high)
    current = tri_count([low])
    mod = low.modifiers.new("Decimate", "DECIMATE")
    mod.decimate_type = "COLLAPSE"
    mod.use_collapse_triangulate = True
    mod.ratio = max(0.0001, min(1.0, original * ratio / max(1, current)))
    bpy.ops.object.modifier_apply(modifier=mod.name)

    # 새 UV를 펼치고 재질을 하나로 정리한다 (구운 텍스처 한 장을 쓴다).
    mesh = low.data
    while mesh.uv_layers:
        mesh.uv_layers.remove(mesh.uv_layers[0])
    mesh.uv_layers.new(name="UVMap")
    mesh.materials.clear()
    select_only([low])
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.004)
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.shade_smooth()
    return low


def make_bake_material(low, name, size):
    mat = bpy.data.materials.new(f"{name}_Mat")
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = nodes.get("Principled BSDF")
    bsdf.inputs["Metallic"].default_value = 0.0
    bsdf.inputs["Roughness"].default_value = 0.6

    color_img = bpy.data.images.new(f"{name}_Color", size, size, alpha=False)
    normal_img = bpy.data.images.new(f"{name}_Normal", size, size, alpha=False)
    normal_img.colorspace_settings.name = "Non-Color"

    color_node = nodes.new("ShaderNodeTexImage")
    color_node.image = color_img
    normal_node = nodes.new("ShaderNodeTexImage")
    normal_node.image = normal_img
    normal_map = nodes.new("ShaderNodeNormalMap")
    links.new(color_node.outputs["Color"], bsdf.inputs["Base Color"])
    links.new(normal_node.outputs["Color"], normal_map.inputs["Color"])
    links.new(normal_map.outputs["Normal"], bsdf.inputs["Normal"])

    low.data.materials.append(mat)
    return mat, color_node, normal_node


def bake(high, low, mat, target_node, bake_type, diag):
    # 굽는 대상 이미지 노드를 활성으로 둬야 그 이미지에 구워진다.
    mat.node_tree.nodes.active = target_node
    select_only(high + [low], active=low)
    kwargs = dict(type=bake_type, use_selected_to_active=True,
                  cage_extrusion=diag * 0.01, max_ray_distance=diag * 0.03, margin=8)
    if bake_type == "DIFFUSE":
        kwargs["pass_filter"] = {"COLOR"}
    else:
        kwargs["normal_space"] = "TANGENT"
    bpy.ops.object.bake(**kwargs)


def export(low, path):
    for img in (n.image for n in low.active_material.node_tree.nodes if n.type == "TEX_IMAGE"):
        img.pack()
    select_only([low])
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", use_selection=True,
                              export_image_format="AUTO", export_yup=True)


def main():
    a = parse_args()
    name = os.path.splitext(os.path.basename(a.input))[0]
    os.makedirs(a.out, exist_ok=True)

    for i, (ratio, size) in enumerate(zip(a.ratios, a.sizes), start=1):
        started = time.time()
        high = import_source(os.path.abspath(a.input))
        # 파일을 새로 열 때마다 씬이 바뀌므로 매번 다시 가져온다.
        scene = bpy.context.scene
        scene.render.engine = "CYCLES"
        scene.cycles.device = "CPU"
        scene.cycles.samples = 4
        diag = bounds_diagonal(high)

        low = make_low(high, ratio, diag)
        mat, color_node, normal_node = make_bake_material(low, f"{name}_LOD{i}", size)
        bake(high, low, mat, color_node, "DIFFUSE", diag)
        bake(high, low, mat, normal_node, "NORMAL", diag)

        for o in high:
            bpy.data.objects.remove(o, do_unlink=True)
        out_path = os.path.join(a.out, f"{name}_LOD{i}.glb")
        export(low, out_path)
        print(f"[make_lods] {name}_LOD{i}: {tri_count([low]):,} tris, texture {size}px, "
              f"{os.path.getsize(out_path) / 1e6:.2f}MB, {time.time() - started:.0f}s")


if __name__ == "__main__":
    main()
