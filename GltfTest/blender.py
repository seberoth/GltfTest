import bpy
from pprint import pprint
from io_scene_gltf2.io.com.gltf2_io import TextureInfo
from io_scene_gltf2.io.com.gltf2_io_extensions import Extension
from io_scene_gltf2.blender.imp.gltf2_blender_image import BlenderImage

bl_info = {
    "name": "Cyberpunk 2077 Material Test",
    "category": "Generic",
    "version": (1, 0, 0),
    "blender": (3, 1, 0),
    "location": "File > Import > glTF 2.0",
    "description": "Cyberpunk 2077 Material Test"
}

# glTF extensions are named following a convention with known prefixes.
# See: https://github.com/KhronosGroup/glTF/tree/main/extensions#about-gltf-extensions
# also: https://github.com/KhronosGroup/glTF/blob/main/extensions/Prefixes.md
glTF_extension_name = "CP_MaterialInstance"

class glTF2ImportUserExtension:

    def __init__(self):
        self.extensions = [Extension(name=glTF_extension_name, extension={}, required=False)]

    def CreateShaderNodeTexImage(self, cur_mat, blender_image_name = None, x = 0, y = 0, name = None):
        ImgNode = cur_mat.nodes.new("ShaderNodeTexImage")
        ImgNode.location = (x, y)
        ImgNode.hide = True
        if name is not None:
            ImgNode.label = name
        if blender_image_name is not None:
            ImgNode.image = bpy.data.images[blender_image_name]

        return ImgNode
    
    def get_image(self, name):
        texture = self.gltf_material.extensions[glTF_extension_name].get(name, None)
        if texture is not None and texture['$type'] == 'Texture':
            BlenderImage.create(self.gltf, texture['$value']['image'])
            return self.gltf.data.images[texture['$value']['image']]
        return None

    def gather_import_material_after_hook(self, gltf_material, vertex_color, blender_mat, gltf):
        if hasattr(gltf_material.extensions, glTF_extension_name) == False:
            pass

        self.gltf_material = gltf_material
        self.gltf = gltf

        cur_mat = blender_mat.node_tree

        pprint(gltf_material.extensions[glTF_extension_name])

        albedo_texture = self.get_image('Albedo')
        if albedo_texture is not None:
            self.CreateShaderNodeTexImage(cur_mat, albedo_texture.blender_image_name, -800, 550, "Albedo")

        normal_texture = self.get_image('Normal')
        if normal_texture is not None:
            nMap = self.CreateShaderNodeTexImage(cur_mat, normal_texture.blender_image_name, -1800, -300, "Normal")
            nMap.image.colorspace_settings.name='Non-Color'

        detail_normal_texture = self.get_image('DetailNormal')
        if detail_normal_texture is not None:
            self.CreateShaderNodeTexImage(cur_mat, detail_normal_texture.blender_image_name, -1800, -450, "DetailNormal")

        roughness_texture = self.get_image('Roughness')
        if roughness_texture is not None:
            self.CreateShaderNodeTexImage(cur_mat, roughness_texture.blender_image_name, -1600, 50, "Roughness")


def register():
    pass


def unregister():
    pass