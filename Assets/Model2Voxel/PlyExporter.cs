using UnityEngine;
using UnityEngine.Events;
using System.IO;
using System.Threading.Tasks;

namespace Model2VoxelConverter
{
    public class PlyExporter
    {
        public UnityEvent OnComplete;

        public PlyExporter()
        {

        }

        public async Task ExportToPlyAsync(string filePath, Vector3[] vertices, Color32[] colors, int[] faces)
        {
            if (vertices == null || colors == null || faces == null ||
                vertices.Length == 0 || colors.Length == 0 || faces.Length == 0 ||
                vertices.Length != colors.Length)
            {
                Debug.LogError("Invalid input data for PlyExporter!");
                return;
            }

            using (StreamWriter sw = new StreamWriter(filePath))
            {
                // Write header
                WriteHeader(sw, vertices.Length, faces.Length / 3);

                // Write vertices and colors asynchronously
                for (int i = 0; i < vertices.Length; i++)
                {
                    await WriteVertexAsync(sw, vertices[i], colors[i]);
                }

                // Write faces asynchronously
                for (int i = 0; i < faces.Length; i += 3)
                {
                    await WriteFaceAsync(sw, faces[i], faces[i + 1], faces[i + 2]);
                }
            }

            Debug.Log($">>>>>>[{nameof(PlyExporter)}] PLY file exported successfully to: {filePath}");
            OnComplete?.Invoke();
        }

        private void WriteHeader(StreamWriter sw, int vertexCount, int faceCount)
        {
            sw.WriteLine("ply");
            sw.WriteLine("format ascii 1.0");
            sw.WriteLine("element vertex " + vertexCount);
            sw.WriteLine("property float x");
            sw.WriteLine("property float y");
            sw.WriteLine("property float z");
            sw.WriteLine("property uchar red");
            sw.WriteLine("property uchar green");
            sw.WriteLine("property uchar blue");
            sw.WriteLine("element face " + faceCount);
            sw.WriteLine("property list uchar int vertex_indices");
            sw.WriteLine("end_header");
        }

        private async Task WriteVertexAsync(StreamWriter sw, Vector3 vertex, Color32 color)
        {
            await sw.WriteLineAsync($"{vertex.x} {vertex.y} {vertex.z} {(int)(color.r)} {(int)(color.g)} {(int)(color.b)}");
        }

        private async Task WriteFaceAsync(StreamWriter sw, int v1, int v2, int v3)
        {
            await sw.WriteLineAsync($"3 {v1} {v2} {v3}");
        }
    }
}