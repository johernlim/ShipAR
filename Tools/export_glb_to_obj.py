"""Minimal GLB 2.0 to OBJ/MTL exporter for this Unity assignment.

The source model uses ordinary triangle primitives and embedded PNG/JPEG textures.
This keeps the final Unity project self-contained without a runtime glTF dependency.
"""

from __future__ import annotations

import argparse
import json
import math
import struct
from pathlib import Path

import numpy as np


COMPONENT_DTYPES = {
    5120: np.dtype("i1"),
    5121: np.dtype("u1"),
    5122: np.dtype("<i2"),
    5123: np.dtype("<u2"),
    5125: np.dtype("<u4"),
    5126: np.dtype("<f4"),
}

TYPE_WIDTHS = {
    "SCALAR": 1,
    "VEC2": 2,
    "VEC3": 3,
    "VEC4": 4,
    "MAT2": 4,
    "MAT3": 9,
    "MAT4": 16,
}


def read_glb(path: Path) -> tuple[dict, bytes]:
    data = path.read_bytes()
    magic, version, declared_length = struct.unpack_from("<4sII", data, 0)
    if magic != b"glTF" or version != 2 or declared_length != len(data):
        raise ValueError("Expected a valid GLB 2.0 file")

    json_chunk = None
    bin_chunk = b""
    offset = 12
    while offset < len(data):
        chunk_length, chunk_type = struct.unpack_from("<II", data, offset)
        offset += 8
        chunk = data[offset : offset + chunk_length]
        offset += chunk_length
        if chunk_type == 0x4E4F534A:
            json_chunk = chunk
        elif chunk_type == 0x004E4942:
            bin_chunk = chunk

    if json_chunk is None:
        raise ValueError("GLB contains no JSON chunk")
    return json.loads(json_chunk.decode("utf-8").rstrip(" \t\r\n\0")), bin_chunk


def accessor_array(doc: dict, blob: bytes, accessor_index: int) -> np.ndarray:
    accessor = doc["accessors"][accessor_index]
    view = doc["bufferViews"][accessor["bufferView"]]
    dtype = COMPONENT_DTYPES[accessor["componentType"]]
    width = TYPE_WIDTHS[accessor["type"]]
    count = accessor["count"]
    start = view.get("byteOffset", 0) + accessor.get("byteOffset", 0)
    element_bytes = dtype.itemsize * width
    stride = view.get("byteStride", element_bytes)

    if stride == element_bytes:
        values = np.frombuffer(blob, dtype=dtype, count=count * width, offset=start)
        return values.reshape(count, width).copy()

    values = np.empty((count, width), dtype=dtype)
    for row in range(count):
        values[row] = np.frombuffer(
            blob, dtype=dtype, count=width, offset=start + row * stride
        )
    return values


def quaternion_matrix(q: list[float]) -> np.ndarray:
    x, y, z, w = q
    length = math.sqrt(x * x + y * y + z * z + w * w)
    if length == 0:
        return np.identity(4)
    x, y, z, w = x / length, y / length, z / length, w / length
    return np.array(
        [
            [1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w), 0],
            [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w), 0],
            [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y), 0],
            [0, 0, 0, 1],
        ],
        dtype=np.float64,
    )


def node_matrix(node: dict) -> np.ndarray:
    if "matrix" in node:
        return np.array(node["matrix"], dtype=np.float64).reshape((4, 4), order="F")
    translation = np.identity(4)
    translation[:3, 3] = node.get("translation", [0, 0, 0])
    rotation = quaternion_matrix(node.get("rotation", [0, 0, 0, 1]))
    scale = np.identity(4)
    scale[0, 0], scale[1, 1], scale[2, 2] = node.get("scale", [1, 1, 1])
    return translation @ rotation @ scale


def safe_name(value: str, fallback: str) -> str:
    cleaned = "".join(c if c.isalnum() or c in "_-" else "_" for c in value.strip())
    return cleaned or fallback


def image_extension(mime: str | None, payload: bytes) -> str:
    if mime == "image/jpeg" or payload.startswith(b"\xff\xd8"):
        return ".jpg"
    if mime == "image/webp" or payload.startswith(b"RIFF"):
        return ".webp"
    return ".png"


def extract_images(doc: dict, blob: bytes, output_dir: Path) -> dict[int, str]:
    result: dict[int, str] = {}
    for index, image in enumerate(doc.get("images", [])):
        if "bufferView" not in image:
            continue
        view = doc["bufferViews"][image["bufferView"]]
        start = view.get("byteOffset", 0)
        payload = blob[start : start + view["byteLength"]]
        filename = f"texture_{index:02d}{image_extension(image.get('mimeType'), payload)}"
        (output_dir / filename).write_bytes(payload)
        result[index] = filename
    return result


def material_texture_image(doc: dict, material: dict) -> int | None:
    pbr = material.get("pbrMetallicRoughness", {})
    texture_ref = pbr.get("baseColorTexture")
    if texture_ref is None:
        return None
    texture = doc.get("textures", [])[texture_ref["index"]]
    return texture.get("source")


def write_mtl(doc: dict, image_files: dict[int, str], output_path: Path) -> list[str]:
    names: list[str] = []
    lines = ["# Exported from the supplied GLB for Unity import", ""]
    for index, material in enumerate(doc.get("materials", [])):
        name = safe_name(material.get("name", ""), f"material_{index:02d}")
        names.append(name)
        pbr = material.get("pbrMetallicRoughness", {})
        factor = pbr.get("baseColorFactor", [1, 1, 1, 1])
        lines.extend(
            [
                f"newmtl {name}",
                f"Kd {factor[0]:.6f} {factor[1]:.6f} {factor[2]:.6f}",
                f"d {factor[3]:.6f}",
                "Ka 0.080000 0.080000 0.080000",
                "Ks 0.120000 0.120000 0.120000",
                "Ns 32.000000",
                "illum 2",
            ]
        )
        image_index = material_texture_image(doc, material)
        if image_index is not None and image_index in image_files:
            lines.append(f"map_Kd {image_files[image_index]}")
        lines.append("")
    output_path.write_text("\n".join(lines), encoding="utf-8")
    return names


def scene_nodes(doc: dict):
    scene_index = doc.get("scene", 0)
    root_nodes = doc.get("scenes", [{}])[scene_index].get("nodes", [])

    def walk(index: int, parent: np.ndarray):
        node = doc["nodes"][index]
        world = parent @ node_matrix(node)
        yield index, node, world
        for child in node.get("children", []):
            yield from walk(child, world)

    for root in root_nodes:
        yield from walk(root, np.identity(4))


def export_obj(doc: dict, blob: bytes, output_path: Path, material_names: list[str]):
    obj_lines = [
        "# The Kraken's Embrace - converted from the user-supplied GLB",
        f"mtllib {output_path.with_suffix('.mtl').name}",
        "s 1",
        "",
    ]
    vertex_offset = 1
    uv_offset = 1
    normal_offset = 1
    primitive_count = 0

    for node_index, node, world in scene_nodes(doc):
        mesh_index = node.get("mesh")
        if mesh_index is None:
            continue
        mesh = doc["meshes"][mesh_index]
        for primitive_index, primitive in enumerate(mesh.get("primitives", [])):
            if primitive.get("mode", 4) != 4:
                print(f"Skipping non-triangle primitive {mesh_index}:{primitive_index}")
                continue
            attributes = primitive["attributes"]
            positions = accessor_array(doc, blob, attributes["POSITION"]).astype(np.float64)
            homogeneous = np.concatenate(
                [positions, np.ones((positions.shape[0], 1), dtype=np.float64)], axis=1
            )
            positions = (world @ homogeneous.T).T[:, :3]

            normals = None
            if "NORMAL" in attributes:
                normals = accessor_array(doc, blob, attributes["NORMAL"]).astype(np.float64)
                normal_matrix = np.linalg.inv(world[:3, :3]).T
                normals = (normal_matrix @ normals.T).T
                lengths = np.linalg.norm(normals, axis=1)
                lengths[lengths == 0] = 1
                normals /= lengths[:, None]

            uvs = None
            if "TEXCOORD_0" in attributes:
                uvs = accessor_array(doc, blob, attributes["TEXCOORD_0"]).astype(np.float64)

            if "indices" in primitive:
                indices = accessor_array(doc, blob, primitive["indices"]).reshape(-1)
            else:
                indices = np.arange(positions.shape[0])

            object_name = safe_name(
                node.get("name", mesh.get("name", "")),
                f"mesh_{mesh_index:03d}_{primitive_index:02d}",
            )
            obj_lines.extend([f"o {object_name}_{primitive_index:02d}", f"g {object_name}_{primitive_index:02d}"])
            material_index = primitive.get("material")
            if material_index is not None and material_index < len(material_names):
                obj_lines.append(f"usemtl {material_names[material_index]}")

            obj_lines.extend(f"v {x:.8f} {y:.8f} {z:.8f}" for x, y, z in positions)
            if uvs is not None:
                obj_lines.extend(f"vt {u:.8f} {1.0-v:.8f}" for u, v in uvs[:, :2])
            if normals is not None:
                obj_lines.extend(f"vn {x:.8f} {y:.8f} {z:.8f}" for x, y, z in normals)

            for triangle_start in range(0, len(indices) - 2, 3):
                triangle = indices[triangle_start : triangle_start + 3]
                refs = []
                for raw_index in triangle:
                    local = int(raw_index)
                    vertex = vertex_offset + local
                    uv = uv_offset + local if uvs is not None else None
                    normal = normal_offset + local if normals is not None else None
                    if uv is not None and normal is not None:
                        refs.append(f"{vertex}/{uv}/{normal}")
                    elif uv is not None:
                        refs.append(f"{vertex}/{uv}")
                    elif normal is not None:
                        refs.append(f"{vertex}//{normal}")
                    else:
                        refs.append(str(vertex))
                obj_lines.append("f " + " ".join(refs))

            obj_lines.append("")
            vertex_offset += len(positions)
            if uvs is not None:
                uv_offset += len(uvs)
            if normals is not None:
                normal_offset += len(normals)
            primitive_count += 1

    output_path.write_text("\n".join(obj_lines), encoding="utf-8")
    print(f"Exported {primitive_count} triangle primitives to {output_path}")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()

    args.output.mkdir(parents=True, exist_ok=True)
    doc, blob = read_glb(args.source)
    image_files = extract_images(doc, blob, args.output)
    mtl_path = args.output / "TheKrakensEmbrace.mtl"
    material_names = write_mtl(doc, image_files, mtl_path)
    export_obj(doc, blob, args.output / "TheKrakensEmbrace.obj", material_names)


if __name__ == "__main__":
    main()
