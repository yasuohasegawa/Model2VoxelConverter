using System;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Model2VoxelConverter
{
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct GeometryInfo
    {
        public Vector3 pos;
        public Vector3 normal;
        public Vector4 tangent;
        public Color32 color;
        public Vector2 uv;
        public Vector2 uv2;
    }

    public class AdvancedMeshAPICube : IDisposable
    {
        public class BaseCubeMesh
        {
            private static float _SIZE = 0.1f;

            private static float _length = _SIZE;
            private static float _width = _SIZE;
            private static float _height = _SIZE;
            private static Vector3[] _v = new Vector3[] {
                new Vector3(-_length * .5f, -_width * .5f, _height * .5f),
                new Vector3(_length* .5f, -_width* .5f, _height* .5f),
                new Vector3(_length* .5f, -_width* .5f, -_height* .5f),
                new Vector3(-_length* .5f, -_width* .5f, -_height* .5f),
                new Vector3(-_length* .5f, _width* .5f, _height* .5f),
                new Vector3(_length* .5f, _width* .5f, _height* .5f),
                new Vector3(_length* .5f, _width* .5f, -_height* .5f),
                new Vector3(-_length* .5f, _width* .5f, -_height* .5f),
            };

            public NativeArray<Vector3> vertices = new NativeArray<Vector3>(new[] {
                _v[0], _v[1], _v[2], _v[3], // Bottom
	            _v[7], _v[4], _v[0], _v[3], // Left
	            _v[4], _v[5], _v[1], _v[0], // Front
	            _v[6], _v[7], _v[3], _v[2], // Back
	            _v[5], _v[6], _v[2], _v[1], // Right
	            _v[7], _v[6], _v[5], _v[4]  // Top
            }, Allocator.Persistent);

            private static Vector3 _up = Vector3.up;
            private static Vector3 _down = Vector3.down;
            private static Vector3 _forward = Vector3.forward;
            private static Vector3 _back = Vector3.back;
            private static Vector3 _left = Vector3.left;
            private static Vector3 _right = Vector3.right;

            public NativeArray<Vector3> normals = new NativeArray<Vector3>(new[] {
                _down, _down, _down, _down,             // Bottom
	            _left, _left, _left, _left,             // Left
	            _forward, _forward, _forward, _forward,	// Front
	            _back, _back, _back, _back,             // Back
	            _right, _right, _right, _right,         // Right
	            _up, _up, _up, _up                      // Top
            }, Allocator.Persistent);

            public NativeArray<int> triangles = new NativeArray<int>(new[] {
                3, 1, 0,        3, 2, 1,        // Bottom	
	            7, 5, 4,        7, 6, 5,        // Left
	            11, 9, 8,       11, 10, 9,      // Front
	            15, 13, 12,     15, 14, 13,     // Back
	            19, 17, 16,     19, 18, 17,	    // Right
	            23, 21, 20,     23, 22, 21,     // Top
            }, Allocator.Persistent);

            public void UpdateSize(float size)
            {
                _SIZE = size;
                _length = _SIZE;
                _width = _SIZE;
                _height = _SIZE;
                _v = new Vector3[] {
                    new Vector3(-_length * .5f, -_width * .5f, _height * .5f),
                    new Vector3(_length* .5f, -_width* .5f, _height* .5f),
                    new Vector3(_length* .5f, -_width* .5f, -_height* .5f),
                    new Vector3(-_length* .5f, -_width* .5f, -_height* .5f),
                    new Vector3(-_length* .5f, _width* .5f, _height* .5f),
                    new Vector3(_length* .5f, _width* .5f, _height* .5f),
                    new Vector3(_length* .5f, _width* .5f, -_height* .5f),
                    new Vector3(-_length* .5f, _width* .5f, -_height* .5f),
                };

                vertices.Dispose();
                vertices = new NativeArray<Vector3>(new[] {
                    _v[0], _v[1], _v[2], _v[3], // Bottom
	                _v[7], _v[4], _v[0], _v[3], // Left
	                _v[4], _v[5], _v[1], _v[0], // Front
	                _v[6], _v[7], _v[3], _v[2], // Back
	                _v[5], _v[6], _v[2], _v[1], // Right
	                _v[7], _v[6], _v[5], _v[4]  // Top
                }, Allocator.Persistent);
            }
        }

        private VertexAttributeDescriptor[] _layout = new[]
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32, 2),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1,VertexAttributeFormat.Float32, 2),
        };

        public struct CubeData
        {
            public Vector3 position;
            public Vector2 uv;
            public Color32 color;
        }

        private GameObject _cubes;

        private NativeArray<GeometryInfo> _vertexBuffer;
        private NativeArray<int> _indexBuffer;
        private NativeArray<CubeData> _cubeDatas;

        private Mesh _mesh;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private BaseCubeMesh _baseCubeMesh;
        private bool _isDebug = false;

        public BaseCubeMesh baseCubeMesh => _baseCubeMesh;
        public NativeArray<CubeData> cubeDatas { set { _cubeDatas = value; } get { return _cubeDatas; } }
        public MeshRenderer meshRenderer => _meshRenderer;
        public Mesh mesh => _mesh;

        public AdvancedMeshAPICube(Material material, Transform parent)
        {
            if (material == null) return;
            _cubes = new GameObject(nameof(AdvancedMeshAPICube));
            _cubes.transform.SetParent(parent);
            _meshFilter = _cubes.AddComponent<MeshFilter>();
            _meshRenderer = _cubes.AddComponent<MeshRenderer>();
            _meshRenderer.material = material;
            _mesh = new Mesh();
            _mesh.indexFormat = IndexFormat.UInt32;
            _mesh.MarkDynamic();
            _meshFilter.mesh = _mesh;
        }

        public void Initialize()
        {
            DisposeCubeMemory();
            _baseCubeMesh = new BaseCubeMesh();
        }

        private void TestGenerateGrids()
        {
            _cubeDatas = new NativeArray<CubeData>(3, Allocator.Persistent);

            var c = new CubeData { position = Vector3.zero, color = new Color32(255, 0, 0, 255) };
            _cubeDatas[0] = c;
            c = new CubeData { position = new Vector3(0.1f, 0, 0), color = new Color32(0, 255, 0, 255) };
            _cubeDatas[1] = c;
            c = new CubeData { position = new Vector3(-0.1f, 0, 0), color = new Color32(0, 0, 255, 255) };
            _cubeDatas[2] = c;
        }

        private async Task GenerateMesh()
        {
            int cubeCount = _cubeDatas.Length;

            int vertexCount = cubeCount * 24;
            int indexCount = cubeCount * 36;

            _vertexBuffer = new NativeArray<GeometryInfo>(vertexCount, Allocator.Persistent,
            NativeArrayOptions.UninitializedMemory);

            _indexBuffer = new NativeArray<int>(indexCount, Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);

            if (_mesh == null)
            {
                Debug.Log($">>>>>>[{nameof(AdvancedMeshAPICube)}] Mesh is null.");
                DisposeBuffers();
                return;
            }

            await Task.Run(() => UpdateBuffer(cubeCount));
        }

        private bool UpdateBuffer(int cubeCount)
        {
            var index = 0;
            for (int i = 0; i < cubeCount; i++)
            {
                int vertexIndex = index * 24;
                int triangleIndex = index * 36;

                Vector3 cubePosition = _cubeDatas[i].position;
                Vector3 uv = _cubeDatas[i].uv;

                var col = _cubeDatas[i].color;
                for (int j = 0; j < 24; j++)
                {
                    var geo = new GeometryInfo();
                    geo.pos = cubePosition + _baseCubeMesh.vertices[j];
                    geo.normal = _baseCubeMesh.normals[j];
                    geo.color = col;
                    geo.uv = uv;
                    _vertexBuffer[vertexIndex + j] = geo;
                }

                for (int j = 0; j < 36; j++)
                {
                    _indexBuffer[triangleIndex + j] = _baseCubeMesh.triangles[j] + vertexIndex;
                }
                index++;
            }

            return true;
        }

        private void UpdateMesh()
        {
            int cubeCount = _cubeDatas.Length;
            int vertexCount = cubeCount * 24;
            int indexCount = cubeCount * 36;

            if (_mesh == null)
            {
                Debug.Log($">>>>>>[{nameof(AdvancedMeshAPICube)}] Mesh is null.");
                DisposeBuffers();
                return;
            }

            _mesh.Clear();
            _mesh.SetVertexBufferParams(vertexCount, _layout);
            _mesh.SetIndexBufferParams(indexCount, IndexFormat.UInt32);

            _mesh.SetVertexBufferData(_vertexBuffer, 0, 0, vertexCount);
            _mesh.SetIndexBufferData(_indexBuffer, 0, 0, indexCount);

            // Submesh definition
            var meshDesc = new SubMeshDescriptor(0, indexCount, MeshTopology.Triangles);
            _mesh.subMeshCount = 1;
            _mesh.SetSubMesh(0, meshDesc);
            _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000);
            //_mesh.RecalculateNormals();
            //_mesh.RecalculateBounds();
            DisposeBuffers();

            Debug.Log($">>>>>>[{nameof(AdvancedMeshAPICube)}] len:{_mesh.triangles.Length}");
        }

        private void DisposeBuffers()
        {
            //Debug.Log($"_vertexBuffer:{_vertexBuffer.Length}");
            if (_vertexBuffer != null && _vertexBuffer.IsCreated)
            {
                _vertexBuffer.Dispose();
                _indexBuffer.Dispose();
                //Debug.Log($"Disposed:{_vertexBuffer.IsCreated}");
            }
        }

        public async void RunAllProcess(System.Action callback = null)
        {
            if (_isDebug) TestGenerateGrids();
            await GenerateMesh();
            UpdateMesh();
            callback?.Invoke();
        }

        private void DisposeCubeMemory()
        {
            if (_baseCubeMesh != null)
            {
                _baseCubeMesh.vertices.Dispose();
                _baseCubeMesh.normals.Dispose();
                _baseCubeMesh.triangles.Dispose();
                _baseCubeMesh = null;
            }

            if (_cubeDatas != null && _cubeDatas.IsCreated)
            {
                _cubeDatas.Dispose();
            }
        }

        public void Dispose()
        {
            DisposeCubeMemory();

            if (_cubes != null)
            {
                GameObject.Destroy(_cubes);
                _cubes = null;
            }
            Debug.Log($">>>>>>[{nameof(AdvancedMeshAPICube)}] Dispose called");
        }
    }
}