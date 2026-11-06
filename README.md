# BlenderKit Materials for Unity

A Unity Editor extension bringing the **BlenderKit** online PBR materials library directly into Unity. Browse thousands of materials, download them, and drag-and-drop them straight onto 3D objects in your Scene.

---

## Features

- **BlenderKit Material Browser:** Search, filter by category (Wood, Stone, Metal, Marble, Fabric, etc.), resolution (512, 1K, 2K, 4K), and free/subscription licenses.
- **Direct Scene Drag & Drop:** Click and drag any downloaded material card directly onto a 3D mesh in the Unity Scene View to assign it instantly.
- **Apply to Selection:** Select one or multiple GameObjects in the Hierarchy or Scene and assign the material with a single click (with full Undo support).
- **Automated PBR Extraction:** Headless background extraction unpacks `.blend` material data into native textures (Albedo, Normal, Metallic, Roughness, AO, Emission, Height) and generates a packed MetallicGloss map.
- **Multi-Pipeline Compatibility:** Automatically detects and configures materials for **Universal Render Pipeline (URP)**, **High Definition Render Pipeline (HDRP)**, and **Built-in Standard Shader**.

---

## Requirements

- **Unity:** 2021.3 LTS or newer (tested across URP, HDRP, and Built-in).
- **Blender:** Blender 3.6 LTS, 4.x installed on your workstation (used headless in background to unpack and parse material datablocks). The tool auto-detects standard Blender installation paths.

---

## Installation

### 1. Import by URL (Unity Package Manager)
1. Open your Unity Project.
2. Go to **Window > Package Manager**.
3. Click the **+** (plus) icon in the top-left toolbar and select **Add package from git URL...**
4. Paste the repository URL:
   - **From GitHub (once pushed):**
     ```
     https://github.com/<your-username>/BlenderKit-Unity.git
     ```
   - **From Local Git repository:**
     ```
     file:c:/Dev/BlenderKit-Unity
     ```
5. Click **Add**. Unity will automatically clone, index, and load the package into your project.

### 2. Add via `Packages/manifest.json`
You can also add it directly to your Unity project's `Packages/manifest.json` under `"dependencies"`:
```json
{
  "dependencies": {
    "com.blenderkit.unity": "https://github.com/<your-username>/BlenderKit-Unity.git"
  }
}
```
Or for local development:
```json
{
  "dependencies": {
    "com.blenderkit.unity": "file:c:/Dev/BlenderKit-Unity"
  }
}
```

---

## Usage

1. Open **Window > BlenderKit > Materials Browser**.
2. Browse or search for materials (e.g. `Oak Wood`, `Marble Tiles`, `Rusted Metal`).
3. Click **Download** on any material.
4. Once downloaded:
   - **Drag to Scene:** Click and drag the material card directly over any 3D object in the Scene View to apply it.
   - **Apply to Selected:** Select any GameObjects in your scene and click *Apply to Selected*.
   - **Locate in Project:** The generated `.mat` and textures are saved in `Assets/BlenderKit/Materials/<MaterialName>/`.
