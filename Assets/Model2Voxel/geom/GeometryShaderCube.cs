using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

// note:Geometry shader only works on Android and Windows
namespace Model2VoxelConverter
{
    public class GeometryShaderCube : MonoBehaviour
    {
        [SerializeField] private Material _mat;

        private List<Vector3> _points = new List<Vector3>();
        private List<int> _indices = new List<int>();
        private List<Color> _colors = new List<Color>();
        private ComputeBuffer _buffer;

        void Start()
        {
            
        }

        public async void RunAllProcess(float size, List<AdvancedMeshAPICube.CubeData> datas, System.Action callback = null)
        {
            _points.Clear();
            _indices.Clear();
            _colors.Clear();

            await Task.Run(() => {
                for (var i = 0; i < datas.Count; i++)
                {
                    var data = datas[i];
                    var col = new Color(data.color.r / 255f, data.color.g / 255f, data.color.b / 255f, 1);
                    _points.Add(data.position);
                    _colors.Add(col);
                    _indices.Add(i);
                }
            });

            Mesh mesh = new Mesh
            {
                vertices = _points.ToArray(),
            };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetColors(_colors.ToArray());
            mesh.SetIndices(_indices.ToArray(), MeshTopology.Points, 0);

            GetComponent<MeshFilter>().mesh = mesh;

            Dispose();
            _buffer = new ComputeBuffer(_colors.Count, Marshal.SizeOf(typeof(Color)), ComputeBufferType.Default);
            _buffer.SetData(_colors);
            _mat.SetBuffer("_colors", _buffer);
            _mat.SetFloat("_BoxSize", size);
            callback?.Invoke();
        }

        void Update()
        {
            Matrix4x4 rotMatrix = Matrix4x4.Rotate(transform.rotation);
            _mat.SetMatrix("_Rotation", rotMatrix);
        }

        private void Dispose()
        {
            if (_buffer != null)
            {
                _buffer.Dispose();
                _buffer = null;
            }
        }

        private void OnDestroy()
        {
            Dispose();
        }
    }
}
