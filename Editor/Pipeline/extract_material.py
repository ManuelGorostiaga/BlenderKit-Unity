import os
import sys
import json
import traceback

def parse_args():
    args = sys.argv
    if "--" not in args:
        raise ValueError("Missing '--' separator for script arguments.")
    custom_args = args[args.index("--") + 1:]
    
    out_dir = None
    for i in range(len(custom_args)):
        if custom_args[i] == "--out" and i + 1 < len(custom_args):
            out_dir = custom_args[i + 1]
            break
            
    if not out_dir:
        raise ValueError("Missing required '--out' parameter.")
    return out_dir

def find_principled_bsdf(material):
    if not material or not material.use_nodes or not material.node_tree:
        return None
    for node in material.node_tree.nodes:
        if node.type == 'BSDF_PRINCIPLED':
            return node
    return None

def trace_image_node(socket):
    if not socket.is_linked:
        return None
    for link in socket.links:
        from_node = link.from_node
        if from_node.type == 'TEX_IMAGE' and from_node.image:
            return from_node.image
        if from_node.type == 'NORMAL_MAP':
            color_socket = from_node.inputs.get('Color')
            if color_socket and color_socket.is_linked:
                return trace_image_node(color_socket)
        if from_node.type in ('DISPLACEMENT', 'VECTOR_DISPLACEMENT'):
            height_socket = from_node.inputs.get('Height')
            if height_socket and height_socket.is_linked:
                return trace_image_node(height_socket)
    return None

def save_image_to_file(image, target_path):
    os.makedirs(os.path.dirname(target_path), exist_ok=True)
    image.save_render(filepath=target_path)

def extract():
    import bpy
    
    out_dir = parse_args()
    os.makedirs(out_dir, exist_ok=True)
    
    selected_material = None
    bsdf_node = None
    
    for mat in bpy.data.materials:
        bsdf = find_principled_bsdf(mat)
        if bsdf:
            selected_material = mat
            bsdf_node = bsdf
            break
            
    if not selected_material or not bsdf_node:
        if len(bpy.data.materials) > 0:
            selected_material = bpy.data.materials[0]
        else:
            raise RuntimeError("No materials found in the .blend file.")
            
    meta = {
        "materialName": selected_material.name,
        "albedoMap": None,
        "normalMap": None,
        "roughnessMap": None,
        "metallicMap": None,
        "occlusionMap": None,
        "emissionMap": None,
        "heightMap": None,
        "baseColor": [1.0, 1.0, 1.0, 1.0],
        "metallic": 0.0,
        "roughness": 0.5,
        "normalStrength": 1.0,
        "emissionColor": [0.0, 0.0, 0.0, 1.0]
    }
    
    if bsdf_node:
        base_color_input = bsdf_node.inputs.get('Base Color')
        if base_color_input:
            meta["baseColor"] = [float(v) for v in base_color_input.default_value]
            img = trace_image_node(base_color_input)
            if img:
                target_filename = "Albedo.png"
                save_image_to_file(img, os.path.join(out_dir, target_filename))
                meta["albedoMap"] = target_filename

        roughness_input = bsdf_node.inputs.get('Roughness')
        if roughness_input:
            meta["roughness"] = float(roughness_input.default_value)
            img = trace_image_node(roughness_input)
            if img:
                target_filename = "Roughness.png"
                save_image_to_file(img, os.path.join(out_dir, target_filename))
                meta["roughnessMap"] = target_filename

        metallic_input = bsdf_node.inputs.get('Metallic')
        if metallic_input:
            meta["metallic"] = float(metallic_input.default_value)
            img = trace_image_node(metallic_input)
            if img:
                target_filename = "Metallic.png"
                save_image_to_file(img, os.path.join(out_dir, target_filename))
                meta["metallicMap"] = target_filename

        normal_input = bsdf_node.inputs.get('Normal')
        if normal_input and normal_input.is_linked:
            for link in normal_input.links:
                if link.from_node.type == 'NORMAL_MAP':
                    strength_socket = link.from_node.inputs.get('Strength')
                    if strength_socket:
                        meta["normalStrength"] = float(strength_socket.default_value)
            img = trace_image_node(normal_input)
            if img:
                target_filename = "Normal.png"
                save_image_to_file(img, os.path.join(out_dir, target_filename))
                meta["normalMap"] = target_filename

        emission_input = bsdf_node.inputs.get('Emission Color') or bsdf_node.inputs.get('Emission')
        if emission_input:
            if hasattr(emission_input, 'default_value'):
                if isinstance(emission_input.default_value, (float, int)):
                    meta["emissionColor"] = [float(emission_input.default_value)] * 3 + [1.0]
                else:
                    meta["emissionColor"] = [float(v) for v in emission_input.default_value]
            img = trace_image_node(emission_input)
            if img:
                target_filename = "Emission.png"
                save_image_to_file(img, os.path.join(out_dir, target_filename))
                meta["emissionMap"] = target_filename

        if selected_material.node_tree:
            for node in selected_material.node_tree.nodes:
                if node.type == 'OUTPUT_MATERIAL':
                    disp_input = node.inputs.get('Displacement')
                    if disp_input and disp_input.is_linked:
                        img = trace_image_node(disp_input)
                        if img:
                            target_filename = "Height.png"
                            save_image_to_file(img, os.path.join(out_dir, target_filename))
                            meta["heightMap"] = target_filename
                            
    for img in bpy.data.images:
        name_lower = img.name.lower()
        if not meta["occlusionMap"] and ("ao" in name_lower or "ambient" in name_lower or "occlusion" in name_lower):
            target_filename = "Occlusion.png"
            save_image_to_file(img, os.path.join(out_dir, target_filename))
            meta["occlusionMap"] = target_filename
        if not meta["albedoMap"] and ("basecolor" in name_lower or "albedo" in name_lower or "diffuse" in name_lower):
            target_filename = "Albedo.png"
            save_image_to_file(img, os.path.join(out_dir, target_filename))
            meta["albedoMap"] = target_filename
        if not meta["normalMap"] and ("normal" in name_lower or "norm" in name_lower or "nor" in name_lower):
            target_filename = "Normal.png"
            save_image_to_file(img, os.path.join(out_dir, target_filename))
            meta["normalMap"] = target_filename
            
    meta_path = os.path.join(out_dir, "material_meta.json")
    with open(meta_path, "w", encoding="utf-8") as f:
        json.dump(meta, f, indent=2)
        
    print("[EXTRACT_SUCCESS]")

if __name__ == "__main__":
    try:
        extract()
        sys.exit(0)
    except Exception as e:
        traceback.print_exc()
        sys.exit(1)
