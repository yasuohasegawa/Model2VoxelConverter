using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine;

namespace Model2VoxelConverter
{
    public class CPUMeshVoxelizer
    {
        private AdvancedMeshAPICube _advancedMeshAPICube;
        private float _spaceBetweenCubes = 0f;

        public CPUMeshVoxelizer(AdvancedMeshAPICube advancedMeshAPICube, float spaceBetweenCubes)
        {
            _advancedMeshAPICube = advancedMeshAPICube;
            _spaceBetweenCubes = spaceBetweenCubes;
        }

        public async Task Generate(int size, MeshRenderer targetObj, System.Action onGenerated = null)
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

            var textureWidth = (texture) ? texture.width : 0;
            var textureHeight = (texture) ? texture.height : 0;

            var scale = 1.0f;
            var nowBlockNum = size; // 10:20, 20:40, 30:60, 40:80, 50:100
            var cubeSize = (scale / (float)(nowBlockNum));
            var voxelSet = new HashSet<Vector3Int>();
            var newUVs = new Dictionary<Vector3Int, Vector2>();
            var newColors = new Dictionary<Vector3Int, Color32>();

            var objScale = targetObj.transform.localScale;

            Debug.Log($">>>> cubeSize:{cubeSize}");
            var datas = new List<AdvancedMeshAPICube.CubeData>();

            await Task.Run(() =>
            {
                var triangleCount = triangles.Length;
                for (var i = 0; i < triangleCount; i += 3)
                {
                    var i0 = triangles[i];
                    var i1 = triangles[i + 1];
                    var i2 = triangles[i + 2];

                    var v0 = Vector3.Scale(verts[i0], objScale);
                    var v1 = Vector3.Scale(verts[i1], objScale);
                    var v2 = Vector3.Scale(verts[i2], objScale);

                    var u0 = uvs[i0];
                    var u1 = uvs[i1];
                    var u2 = uvs[i2];

                    var localSet = new HashSet<Vector3Int>();
                    var localColors = new Dictionary<Vector3Int, Color32>();
                    var localUVs = new Dictionary<Vector3Int, Vector2>();

                    RasterizeTriangle(v0, v1, v2, u0, u1, u2, cubeSize, ref localSet, ref localColors, ref localUVs, colors, textureWidth, textureHeight);

                    voxelSet.UnionWith(localSet);

                    foreach (var color in localColors)
                    {
                        newColors[color.Key] = color.Value;
                    }

                    foreach (var uv in localUVs)
                    {
                        newUVs[uv.Key] = uv.Value;
                    }
                }

                _advancedMeshAPICube.Initialize();
                _advancedMeshAPICube.baseCubeMesh.UpdateSize(cubeSize - _spaceBetweenCubes);

                foreach (var color in newColors)
                {
                    var vox = color.Key;

                    var newX = (float)(vox.x) * cubeSize;
                    var newY = (float)(vox.y) * cubeSize;
                    var newZ = (float)(vox.z) * cubeSize;
                    var newPos = new Vector3(newX, newY, newZ);

                    var uv = newUVs[vox];

                    var c = new AdvancedMeshAPICube.CubeData { position = newPos, uv = uv, color = color.Value };
                    datas.Add(c);
                }
            });


            _advancedMeshAPICube.cubeDatas = new NativeArray<AdvancedMeshAPICube.CubeData>(datas.ToArray(), Allocator.Persistent);
            _advancedMeshAPICube.RunAllProcess(() =>
            {
                onGenerated?.Invoke();
            });
        }

        private void RasterizeTriangle(
            Vector3 v0, Vector3 v1, Vector3 v2,
            Vector2 uv0, Vector2 uv1, Vector2 uv2,
            float voxelSize,
            ref HashSet<Vector3Int> voxelSet,
            ref Dictionary<Vector3Int, Color32> colors,
            ref Dictionary<Vector3Int, Vector2> uvs,
            Color32[] texturePixels,
            int textureWidth,
            int textureHeight)
        {
            // Find triangle AABB in voxel space. This will make the bounding box of the triangle.
            var minBound = Vector3.Min(Vector3.Min(v0, v1), v2);
            var maxBound = Vector3.Max(Vector3.Max(v0, v1), v2);

            var minIndex = new Vector3Int(
               (int)(Mathf.Floor(minBound.x / voxelSize)),
               (int)(Mathf.Floor(minBound.y / voxelSize)),
               (int)(Mathf.Floor(minBound.z / voxelSize))
            );

            var maxIndex = new Vector3Int(
               (int)(Mathf.Floor(maxBound.x / voxelSize)),
               (int)(Mathf.Floor(maxBound.y / voxelSize)),
               (int)(Mathf.Floor(maxBound.z / voxelSize))
            );

            // Loop through all voxels in AABB
            var hit = false;
            for (int x = minIndex.x; x <= maxIndex.x; x++)
            {
                for (int y = minIndex.y; y <= maxIndex.y; y++)
                {
                    for (int z = minIndex.z; z <= maxIndex.z; z++)
                    {
                        var voxelIndex = new Vector3Int(x, y, z);
                        var voxelCenter = new Vector3(
                           (float)(x) * voxelSize + voxelSize / 2f,
                           (float)(y) * voxelSize + voxelSize / 2f,
                           (float)(z) * voxelSize + voxelSize / 2f
                        );

                        // Check if voxel overlaps triangle
                        if (TriangleIntersectsVoxel(v0, v1, v2, voxelCenter, voxelSize))
                        {
                            bool inserted = voxelSet.Add(voxelIndex);
                            if (inserted)
                            {
                                Vector3 bary = GetBarycentricCoordinates(voxelCenter, v0, v1, v2);
                                Vector2 uv = bary.x * uv0 + bary.y * uv1 + bary.z * uv2;
                                Color32 col = BilinearSampleTexture(uv, texturePixels, textureWidth, textureHeight);

                                colors[voxelIndex] = col;
                                uvs[voxelIndex] = uv;
                                hit = true;
                            }
                        }
                    }
                }
            }

            // If the triangle is too small to intersect any voxel centers, add the voxel at its centroid
            if (!hit)
            {
                var mid = (v0 + v1 + v2) / 3f;
                var bary = GetBarycentricCoordinates(mid, v0, v1, v2);
                var uv = bary.x * uv0 + bary.y * uv1 + bary.z * uv2;
                var color = BilinearSampleTexture(uv, texturePixels, textureWidth, textureHeight);
                var voxelIndex = new Vector3Int(
                    (int)Mathf.Floor((float)(mid.x) / voxelSize),
                    (int)Mathf.Floor((float)(mid.y) / voxelSize),
                    (int)Mathf.Floor((float)(mid.z) / voxelSize)
                );
                bool inserted = voxelSet.Add(voxelIndex);
                if (inserted)
                {
                    colors[voxelIndex] = color;
                    uvs[voxelIndex] = uv;
                }
                //Debug.Log($">>>>> RasterizeTriangle:{minIndex},{maxIndex}");
            }
        }

        private bool TriangleIntersectsVoxel(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 voxelCenter, float voxelSize)
        {
            var halfSize = voxelSize / 2f;
            var boxHalfSize = new Vector3(halfSize, halfSize, halfSize);

            // Move triangle to box local space
            var tv0 = v0 - voxelCenter;
            var tv1 = v1 - voxelCenter;
            var tv2 = v2 - voxelCenter;

            // Compute triangle edges
            var e0 = tv1 - tv0;
            var e1 = tv2 - tv1;
            var e2 = tv0 - tv2;

            // 1. Test the 9 edge cross products (triangle edges × box axes)
            Vector3[] axes = {
                new Vector3(0, -e0.z, e0.y), new Vector3(0, -e1.z, e1.y), new Vector3(0, -e2.z, e2.y),
                new Vector3(e0.z, 0, -e0.x), new Vector3(e1.z, 0, -e1.x), new Vector3(e2.z, 0, -e2.x),
                new Vector3(-e0.y, e0.x, 0), new Vector3(-e1.y, e1.x, 0), new Vector3(-e2.y, e2.x, 0)
            };

            foreach (var axis in axes)
            {
                if (!OverlapOnAxis(tv0, tv1, tv2, axis, boxHalfSize))
                {
                    return false;
                }
            }

            // 2. Test overlap in the coordinate axes (X, Y, Z)
            for (var i = 0; i < 3; i++)
            {
                var minVal = Mathf.Min(tv0[i], tv1[i], tv2[i]);
                var maxVal = Mathf.Max(tv0[i], tv1[i], tv2[i]);
                if (minVal > boxHalfSize[i] || maxVal < -boxHalfSize[i])
                {
                    return false;
                }
            }

            // 3. Test the triangle normal axis
            var normal = Vector3.Cross(e0, e1);
            if (!PlaneBoxOverlap(normal, tv0, boxHalfSize))
            {
                return false;
            }

            return true;
        }

        private bool OverlapOnAxis(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 axis, Vector3 boxHalfSize)
        {
            var p0 = Vector3.Dot(v0, axis);
            var p1 = Vector3.Dot(v1, axis);
            var p2 = Vector3.Dot(v2, axis);
            var r = boxHalfSize.x * Mathf.Abs(axis.x) + boxHalfSize.y * Mathf.Abs(axis.y) + boxHalfSize.z * Mathf.Abs(axis.z);
            var minP = Mathf.Min(p0, p1, p2);
            var maxP = Mathf.Max(p0, p1, p2);
            return !(minP > r || maxP < -r);
        }

        private bool PlaneBoxOverlap(Vector3 normal, Vector3 vert, Vector3 maxBox)
        {
            var vmin = Vector3.zero;
            var vmax = Vector3.zero;

            for (var i = 0; i < 3; i++)
            {
                var v = vert[i];
                if (normal[i] > 0f)
                {
                    vmin[i] = -maxBox[i] - v;
                    vmax[i] = maxBox[i] - v;
                }
                else
                {
                    vmin[i] = maxBox[i] - v;
                    vmax[i] = -maxBox[i] - v;
                }
            }

            return Vector3.Dot(normal, vmin) <= 0f || Vector3.Dot(normal, vmax) >= 0f;
        }

        // this will return the uv coordinate based on the vertices.
        private Vector3 GetBarycentricCoordinates(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 v0 = b - a;
            Vector3 v1 = c - a;
            Vector3 v2 = p - a;
            float d00 = Vector3.Dot(v0, v0);
            float d01 = Vector3.Dot(v0, v1);
            float d11 = Vector3.Dot(v1, v1);
            float d20 = Vector3.Dot(v2, v0);
            float d21 = Vector3.Dot(v2, v1);
            float denom = d00 * d11 - d01 * d01;

            float v = (d11 * d20 - d01 * d21) / denom;
            float w = (d00 * d21 - d01 * d20) / denom;
            float u = 1.0f - v - w;

            return new Vector3(u, v, w);
        }

        // straight forward to sample the texture color
        private Color32 SampleTexture(Vector2 uv, Color32[] texturePixels, int width, int height)
        {
            var x = Mathf.FloorToInt(uv.x * width);
            var y = Mathf.FloorToInt(uv.y * height);

            x = Mathf.Clamp(x, 0, width - 1);
            y = Mathf.Clamp(y, 0, height - 1);

            return texturePixels[y * width + x];
        }

        private Color32 BilinearSampleTexture(Vector2 uv, Color32[] texturePixels, int width, int height)
        {
            float fx = uv.x * width - 0.5f;
            float fy = uv.y * height - 0.5f;

            int x = Mathf.Clamp(Mathf.FloorToInt(fx), 0, width - 2);
            int y = Mathf.Clamp(Mathf.FloorToInt(fy), 0, height - 2);

            float tx = fx - x;
            float ty = fy - y;

            Color32 c00 = texturePixels[y * width + x];
            Color32 c10 = texturePixels[y * width + (x + 1)];
            Color32 c01 = texturePixels[(y + 1) * width + x];
            Color32 c11 = texturePixels[(y + 1) * width + (x + 1)];

            Color c = Color.Lerp(
                Color.Lerp(c00, c10, tx),
                Color.Lerp(c01, c11, tx),
                ty
            );

            return c;
        }


    }
}