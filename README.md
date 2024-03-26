# Model2VoxelConverter
The Model2VoxelConverter is a tool designed to transform a single mesh model into a voxel model within the Unity environment.<br>

![Screenshot](screenshots.png)

**Setup:**
1. Import the `Model2Voxel` folder into your Unity project.
2. Attach the `Voxel` class to a GameObject.


Refer to the base setup example provided in the Voxel scene, where a GameObject is already attached to the `Voxel` class component.
Run the project and click the `Generate` button to view the result.

**Features:**
- Converts a Voxel model into a single GameObject with a MeshRenderer.
- Currently supports export to .ply format only.

**Not Implemented:**
- The target model's world and local positions/rotations must be set to `0,0,0`. This feature does not currently adjust based on the target object's transform.

**Partially Implemented:**
- FBX import feature

**UnityFBXLoader:**
The UnityFBXLoader is an adaptation of the Three-Fbx-Loader, focusing on extracting geometry data to generate the mesh. 
Note: This class is experimental and not recommended for production use. However, it serves as a useful starting point for further development based on the Three-Fbx-Loader.

Reference: [Three-Fbx-Loader](https://www.npmjs.com/package/three-fbx-loader?activeTab=code)


**Unity Editor Compatibility:**
Version 2023.2.4f1 and above.

**Tested Platforms:**
- Android OS9
- Unity Editor

Further device testing will be conducted in the future.