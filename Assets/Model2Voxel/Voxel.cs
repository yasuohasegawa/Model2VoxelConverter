using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Assertions;

namespace Model2VoxelConverter
{
    public class Voxel : MonoBehaviour
    {
        [SerializeField] private Material _voxelMaterial;
        [SerializeField] private LayerMask _layerHitMask = 1 << 0;
        [SerializeField] private bool _useJob = true;
        [SerializeField] private bool _useGeometryShader = false;
        [SerializeField] private GeometryShaderCube _geometryShaderCube;
        [SerializeField] private GPUMeshVoxelizer _gPUMeshVoxelizer;

        private CPUMeshVoxelizer _cPUMeshVoxelizer;
        private AdvancedMeshAPICube _advancedMeshAPICube;
        private MeshRenderer _targetObj;

        private float _additionalBlocks = 3f;
        private float _spaceBetweenCubes = 0f;
        private bool _checkGenerationPerf = true;
        private bool _isExporting = false;

        // For now, the library only supports the PLY but we will add more eport fatures later.
        private PlyExporter _plyExporter = new PlyExporter();

        private Vector3[] _hitDirs = new Vector3[6] { Vector3.up, Vector3.down, Vector3.forward, Vector3.back, Vector3.left, Vector3.right };

        public bool isExporting => _isExporting;
        public IVoxel listener { set; get; }
        public GameObject voxelObj => _advancedMeshAPICube.meshRenderer.gameObject;

        private void Awake()
        {
            _advancedMeshAPICube = new AdvancedMeshAPICube(_voxelMaterial, this.transform);
            _cPUMeshVoxelizer = new CPUMeshVoxelizer(_advancedMeshAPICube, _spaceBetweenCubes);
            _gPUMeshVoxelizer.Initialize(_advancedMeshAPICube, _spaceBetweenCubes);
        }

        void Start()
        {

        }

        // [important] Mesh: read/write settings
        private void ValidateTargetObjectMeshSettings()
        {
            Assert.IsNotNull(_advancedMeshAPICube, "The AdvancedMeshAPICube class reference is missing.");
            Assert.IsNotNull(_targetObj, "The target object reference is missing.");
            Assert.IsNotNull(_targetObj.GetComponent<MeshCollider>(), "MeshCollider component not found on the _targetObj GameObject.");

            MeshFilter meshFilter = _targetObj.GetComponent<MeshFilter>();
            Assert.IsNotNull(meshFilter, "MeshFilter component not found on the GameObject.");

            Mesh mesh = meshFilter.sharedMesh;
            Assert.IsNotNull(mesh, "Mesh not found on the MeshFilter component.");

            bool isReadable = mesh.isReadable;
            Assert.IsTrue(isReadable, "Mesh is not set to be readable.");

            MeshRenderer rendererComponent = _targetObj.GetComponent<MeshRenderer>();
            Assert.IsNotNull(rendererComponent, "Renderer component not assigned.");

            Material material = rendererComponent.sharedMaterial;
            Assert.IsNotNull(material, "Material not found on the Renderer component.");

            Texture mainTexture = material.mainTexture;
            Assert.IsNotNull(mainTexture, "Main texture not found in the material.");
        }

        public void GenerateVoxel(float gridSize, MeshRenderer targetObj)
        {
            _targetObj = targetObj;

            ValidateTargetObjectMeshSettings();
            Generate(gridSize);
        }

        public async void ProcessCPUMeshVoxelizer(int size, MeshRenderer targetObj)
        {
            _targetObj = targetObj;

            ValidateTargetObjectMeshSettings();
            System.Diagnostics.Stopwatch sw = null;
            sw = new System.Diagnostics.Stopwatch();
            sw.Start();

            await _cPUMeshVoxelizer.Generate(size, targetObj, OnGenerated);

            if (sw != null)
            {
                sw.Stop();
                Debug.Log($">>>>> {sw.ElapsedMilliseconds / 1000.0f} ms");
            }
        }

        public void ProcessGPUMeshVoxelizer(int size, MeshRenderer targetObj)
        {
            _targetObj = targetObj;
            ValidateTargetObjectMeshSettings();
            System.Diagnostics.Stopwatch sw = null;
            sw = new System.Diagnostics.Stopwatch();
            sw.Start();

            _gPUMeshVoxelizer.Generate(size, targetObj, OnGenerated);

            if (sw != null)
            {
                sw.Stop();
                Debug.Log($">>>>> {sw.ElapsedMilliseconds / 1000.0f} ms");
            }
        }

        private void Generate(float gridSize)
        {
            _targetObj.transform.position = Vector3.zero;
            _targetObj.transform.localPosition = Vector3.zero;
            _targetObj.gameObject.SetActive(true);

            var min = _targetObj.bounds.min;
            var max = _targetObj.bounds.max;
            var center = _targetObj.bounds.center;
            var size = max - min;
            var maxComponent = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            var diff = (size * 0.5f) - center;
            Debug.Log($"{size*0.5f},{center}");
            //Debug.Log(diff);

            var cubeSize = (maxComponent / gridSize);

            _advancedMeshAPICube.Initialize();
            _advancedMeshAPICube.baseCubeMesh.UpdateSize(cubeSize - _spaceBetweenCubes);

            var s = cubeSize;
            var newX = size.x + (s * _additionalBlocks);
            var newY = size.y + (s * _additionalBlocks);
            var newZ = size.z + (s * _additionalBlocks);
            var newSize = new Vector3(newX, newY, newZ);
            var diffX = newSize.x / size.x;
            var diffY = newSize.y / size.y;
            var diffZ = newSize.z / size.z;
            diff.x *= diffX;
            diff.y *= diffY;
            diff.z *= diffZ;
            //Debug.Log($"{diffX},{diffY},{diffZ}");
            var texture = _targetObj.material.mainTexture as Texture2D;
            if (texture != null && !texture.isReadable) // enable the read/write
            {
                texture = VoxelUtils.GetReadableTexture(texture);
            }

            var colors = (texture != null) ? texture.GetPixels32() : null;

            System.Diagnostics.Stopwatch sw = null;
            if (_checkGenerationPerf)
            {
                sw = new System.Diagnostics.Stopwatch();
                sw.Start();
            }

            var textureWidth = (texture) ? texture.width : 0;
            var textureHeight = (texture) ? texture.height : 0;
            var datas = !_useJob ? GenerateVoxelWithDefaultAPI(s, newSize, diff, colors, textureWidth, textureHeight) : GenerateVoxelWithJob(s, newSize, diff, colors, textureWidth, textureHeight);
            if (sw != null)
            {
                sw.Stop();
                Debug.Log($">>>>> {sw.ElapsedMilliseconds} ms");
            }

            Debug.Log(datas.Count);

            if (!_useGeometryShader)
            {
                _advancedMeshAPICube.cubeDatas = new NativeArray<AdvancedMeshAPICube.CubeData>(datas.ToArray(), Allocator.Persistent);
                _advancedMeshAPICube.RunAllProcess(() =>
                {
                    OnGenerated();
                });
            } else
            {
                if(_geometryShaderCube != null)
                {
                    _geometryShaderCube.RunAllProcess(s, datas, () =>
                    {
                        OnGenerated();
                    });
                }
            }
        }

        private void OnGenerated()
        {
            Debug.Log($">>>>>>[{nameof(Voxel)}] Voxel generated");
            _targetObj.gameObject.SetActive(false);
            listener?.OnGenerated();
        }

        [GenerateTestsForBurstCompatibility]
        private List<AdvancedMeshAPICube.CubeData> GenerateVoxelWithDefaultAPI(float s, Vector3 newSize, Vector3 diff, Color32[] colors, int textureWidth, int textureHeight)
        {
            Debug.Log($">>>>>>[{nameof(Voxel)}] GenerateVoxelWithDefaultAPI");
            RaycastHit hit;
            var datas = new List<AdvancedMeshAPICube.CubeData>();
            for (var x = 0f; x < newSize.x; x += s)
            {
                for (var y = 0f; y < newSize.y; y += s)
                {
                    for (var z = 0f; z < newSize.z; z += s)
                    {
                        float px = x - diff.x;
                        float py = y - diff.y;
                        float pz = z - diff.z;
                        var pos = new Vector3(px, py, pz);

                        var checkDist = s;
                        var targetPos = pos;

                        for (var i = 0; i < _hitDirs.Length; i++)
                        {
                            var dir = _hitDirs[i];
                            if (Physics.Raycast(targetPos, dir, out hit, checkDist, _layerHitMask))
                            {
                                //Debug.DrawRay(targetPos, dir * hit.distance, Color.yellow, Mathf.Infinity);
                                var col = GetColor(hit.textureCoord, colors, textureWidth, textureHeight);
                                var c = new AdvancedMeshAPICube.CubeData { position = pos, uv = hit.textureCoord, color = col };
                                datas.Add(c);
                                break;
                            }
                        }

                        //var c = new AdvancedMeshAPICube.CubeData { position = pos, color = new Color32(255, 255, 255, 255) };
                        //datas.Add(c);
                    }
                }
            }

            return datas;
        }

        private List<AdvancedMeshAPICube.CubeData> GenerateVoxelWithJob(float s, Vector3 newSize, Vector3 diff, Color32[] colors, int textureWidth, int textureHeight)
        {
            Debug.Log($">>>>>>[{nameof(Voxel)}] GenerateVoxelWithJob");
            var datas = new List<AdvancedMeshAPICube.CubeData>();

            var commands = new List<RaycastCommand>();
            var hits = new List<RaycastHit>();
            var query = QueryParameters.Default;
            query.layerMask = _layerHitMask;
            for (var x = 0f; x < newSize.x; x += s)
            {
                for (var y = 0f; y < newSize.y; y += s)
                {
                    for (var z = 0f; z < newSize.z; z += s)
                    {
                        float px = x - diff.x;
                        float py = y - diff.y;
                        float pz = z - diff.z;
                        var pos = new Vector3(px, py, pz);

                        var checkDist = s;
                        var targetPos = pos;

                        for (var i = 0; i < _hitDirs.Length; i++)
                        {
                            var dir = _hitDirs[i];
                            commands.Add(new RaycastCommand(targetPos, dir, query, checkDist));
                            hits.Add(new RaycastHit());
                        }
                    }
                }
            }

            var resCommands = new NativeArray<RaycastCommand>(commands.ToArray(), Allocator.Persistent);
            var resHits = new NativeArray<RaycastHit>(hits.ToArray(), Allocator.Persistent);

            var handle = RaycastCommand.ScheduleBatch(resCommands, resHits, 100);
            handle.Complete();

            for (var i = 0; i < resHits.Length; i++)
            {
                var command = resCommands[i];
                var targetPos = command.from;
                var hitRes = resHits[i];
                if (hitRes.transform != null)
                {
                    var hasData = datas.Any(o => o.position == targetPos);
                    if (!hasData)
                    {
                        var col = GetColor(hitRes.textureCoord, colors, textureWidth, textureHeight);
                        var c = new AdvancedMeshAPICube.CubeData { position = targetPos, uv = hitRes.textureCoord, color = col };
                        datas.Add(c);
                    }
                }
            }

            resCommands.Dispose();
            resHits.Dispose();

            return datas;
        }

        private Color32 GetColor(Vector2 uv, Color32[] colors, int textureWidth, int textureHeight)
        {
            //return new Color32(255, 255, 255, 255);
            if (colors == null) return new Color32(255, 255, 255, 255);
            var x = Mathf.FloorToInt(uv.x * textureWidth);
            var y = Mathf.FloorToInt(uv.y * textureHeight);

            x = Mathf.Clamp(x, 0, textureWidth - 1);
            y = Mathf.Clamp(y, 0, textureHeight - 1);

            var index = y * textureWidth + x;

            return colors[index];
        }

        public async void ExportPly(string filePath)
        {
            if (_isExporting) return;
            _isExporting = true;
            var vertices = _advancedMeshAPICube.mesh.vertices;
            var colors = _advancedMeshAPICube.mesh.colors32;
            var faces = _advancedMeshAPICube.mesh.GetIndices(0);
            await _plyExporter.ExportToPlyAsync(filePath, vertices, colors, faces);
            _isExporting = false;
            Debug.Log($">>>>>>[{nameof(Voxel)}] Exported");
            listener?.OnExported();
        }

        private void Test()
        {
            var cubeDatas = new NativeArray<AdvancedMeshAPICube.CubeData>(3, Allocator.Persistent);
            var c = new AdvancedMeshAPICube.CubeData { position = Vector3.zero, color = new Color32(255, 0, 0, 255) };
            cubeDatas[0] = c;
            c = new AdvancedMeshAPICube.CubeData { position = new Vector3(0.1f, 0, 0), color = new Color32(0, 255, 0, 255) };
            cubeDatas[1] = c;
            c = new AdvancedMeshAPICube.CubeData { position = new Vector3(-0.1f, 0, 0), color = new Color32(0, 0, 255, 255) };
            cubeDatas[2] = c;
            _advancedMeshAPICube.cubeDatas = cubeDatas;
            _advancedMeshAPICube.RunAllProcess();
        }

        private void Dispose()
        {
            Debug.Log($">>>>>>[{nameof(Voxel)}] Dispose called {_advancedMeshAPICube}");
            if (_advancedMeshAPICube != null)
            {
                _advancedMeshAPICube.Dispose();
                _advancedMeshAPICube = null;
            }
        }

        private void OnDestroy()
        {
            Dispose();
        }

        private void OnApplicationQuit()
        {
            Dispose();
        }
    }
}