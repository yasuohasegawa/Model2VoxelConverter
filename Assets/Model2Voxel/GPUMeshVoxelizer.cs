using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEngine;

namespace Model2VoxelConverter
{
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    struct GPUVoxel
    {
        public Vector3 position;
        public Vector2 uv;
        public Color32 color;
    }

    public class GPUMeshVoxelizer : MonoBehaviour
    {
        [SerializeField] private ComputeShader _computeShader;

        private AdvancedMeshAPICube _advancedMeshAPICube;
        private float _spaceBetweenCubes = 0f;

        private ComputeBuffer _vertexBuffer;
        private ComputeBuffer _uvBuffer;
        private ComputeBuffer _triangleBuffer;
        private ComputeBuffer _colorBuffer;
        private ComputeBuffer _voxelBuffer;

        void Start()
        {

        }

        public void Initialize(AdvancedMeshAPICube advancedMeshAPICube, float spaceBetweenCubes)
        {
            _advancedMeshAPICube = advancedMeshAPICube;
            _spaceBetweenCubes = spaceBetweenCubes;
        }

        public void Generate(int size, MeshRenderer targetObj, System.Action onGenerated = null)
        {
            var mesh = targetObj.GetComponent<MeshFilter>().mesh;
            var verts = mesh.vertices;
            var triangles = mesh.triangles;
            var uvs = mesh.uv;
            var texture = targetObj.material.mainTexture as Texture2D;
            if (texture != null && !texture.isReadable) // enable the read/write
            {
                texture = VoxelUtils.GetReadableTexture(texture);
            }

            var colors = (texture != null) ? texture.GetPixels32() : null;
            uint[] packedColors = new uint[colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                Color32 c = colors[i];
                packedColors[i] = (uint)(c.r | (c.g << 8) | (c.b << 16) | (c.a << 24));
            }


            var textureWidth = (texture) ? texture.width : 0;
            var textureHeight = (texture) ? texture.height : 0;

            var scale = 1.0f;
            var nowBlockNum = size; // 10:20, 20:40, 30:60, 40:80, 50:100
            var cubeSize = (scale / (float)(nowBlockNum));

            var objScale = targetObj.transform.localScale;

            Bounds meshBounds = mesh.bounds;
            Vector3 bsize = Vector3.Scale(meshBounds.size, objScale);
            Vector3Int voxelGridSize = new Vector3Int(
                Mathf.CeilToInt(bsize.x / cubeSize),
                Mathf.CeilToInt(bsize.y / cubeSize),
                Mathf.CeilToInt(bsize.z / cubeSize)
            );

            int voxelMax = voxelGridSize.x * voxelGridSize.y * voxelGridSize.z * 3;

            Debug.Log($">>>> voxelMax:{voxelMax}");


            // Create buffers
            _vertexBuffer = new ComputeBuffer(verts.Length, sizeof(float) * 3);
            _uvBuffer = new ComputeBuffer(uvs.Length, sizeof(float) * 2);
            _triangleBuffer = new ComputeBuffer(triangles.Length, sizeof(int));
            _colorBuffer = new ComputeBuffer(packedColors.Length, sizeof(uint));


            int voxelStride = sizeof(float) * 3 + sizeof(float) * 2 + sizeof(uint);
            _voxelBuffer = new ComputeBuffer(voxelMax, voxelStride, ComputeBufferType.Append);

            // Reset counter to 0 before dispatch
            _voxelBuffer.SetCounterValue(0);

            // Set data
            _vertexBuffer.SetData(verts);
            _uvBuffer.SetData(uvs);
            _triangleBuffer.SetData(triangles);
            _colorBuffer.SetData(packedColors);

            int kernel = _computeShader.FindKernel("CSMain");
            int triangleCount = triangles.Length / 3;
            int threadGroupSize = 64;
            int threadGroups = Mathf.CeilToInt((float)triangleCount / threadGroupSize);

            _computeShader.SetBuffer(kernel, "_vertices", _vertexBuffer);
            _computeShader.SetBuffer(kernel, "_uvs", _uvBuffer);
            _computeShader.SetBuffer(kernel, "_triangles", _triangleBuffer);
            _computeShader.SetBuffer(kernel, "_colors", _colorBuffer);
            _computeShader.SetBuffer(kernel, "_voxelBuffer", _voxelBuffer);

            _computeShader.SetInt("_textureWidth", textureWidth);
            _computeShader.SetInt("_textureHeight", textureHeight);
            _computeShader.SetVector("_scale", objScale);
            _computeShader.SetInt("_triangleCount", triangleCount);
            _computeShader.SetFloat("_voxelSize", cubeSize);
            

            _computeShader.Dispatch(kernel, threadGroups, 1, 1);


            // read back data
            ComputeBuffer voxelCountBuffer = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Raw);
            ComputeBuffer.CopyCount(_voxelBuffer, voxelCountBuffer, 0);

            uint[] countArray = new uint[1];
            voxelCountBuffer.GetData(countArray);
            int voxelCount = (int)countArray[0];

            if (voxelCount > voxelMax)
            {
                Debug.LogWarning($"[Clamped] voxelCount ({voxelCount}) exceeds voxelMax ({voxelMax}), clamping.");
                voxelCount = voxelMax;
            }

            GPUVoxel[] voxels = new GPUVoxel[voxelCount];
            _voxelBuffer.GetData(voxels, 0, 0, voxelCount);

            Debug.Log($">>>>>>> voxelCount:{voxelCount}");

            voxelCountBuffer.Release();


            var datas = new List<AdvancedMeshAPICube.CubeData>();
            _advancedMeshAPICube.Initialize();
            _advancedMeshAPICube.baseCubeMesh.UpdateSize(cubeSize - _spaceBetweenCubes);

            foreach (var vox in voxels)
            {
                var c = new AdvancedMeshAPICube.CubeData { position = vox.position, uv = vox.uv, color = vox.color };
                datas.Add(c);
            }

            _advancedMeshAPICube.cubeDatas = new NativeArray<AdvancedMeshAPICube.CubeData>(datas.ToArray(), Allocator.Persistent);
            _advancedMeshAPICube.RunAllProcess(() =>
            {
                onGenerated?.Invoke();
            });
        }

        private void Release()
        {
            if (_vertexBuffer != null) _vertexBuffer.Release();
            if (_uvBuffer != null) _uvBuffer.Release();
            if (_triangleBuffer != null) _triangleBuffer.Release();
            if (_colorBuffer != null) _colorBuffer.Release();
            if (_voxelBuffer != null) _voxelBuffer.Release();
        }

        private void OnDestroy()
        {
            Release();
        }
    }
}