using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Model2VoxelConverter
{
    // This class serves as an adaptation of the three-fbx-loader, focusing solely on extracting geometry data to generate the mesh.
    // The bone-related functionality has been omitted in this implementation.
    // Reference: https://www.npmjs.com/package/three-fbx-loader?activeTab=code
    // Note: Supports only binary format and processes single meshes exclusively.
    // Tested with FBX files exported from Blender.

    public class UnityFBXLoader
    {
        private VertexAttributeDescriptor[] _layout = new[]
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32, 2),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1,VertexAttributeFormat.Float32, 2),
        };

        private SynchronizationContext _context;

        private GameObject _exportedModel;

        public GameObject exportedModel => _exportedModel;

        public System.Action<GameObject> OnLoaded = null;

        public UnityFBXLoader()
        {

        }

        public async void Load(string filePath, Material mat = null)
        {
            _context = SynchronizationContext.Current;

            await Task.Run(() => LoadBinaryFBXMesh(filePath,mat));

            OnLoaded?.Invoke(_exportedModel);
            Debug.Log(">>>>>> FBX Loaded");
        }

        private void LoadBinaryFBXMesh(string filePath, Material mat)
        {
            if (!File.Exists(filePath))
            {
                Debug.LogError("FBX file not found: " + filePath);
                return;
            }

            var fileName = System.IO.Path.GetFileNameWithoutExtension(filePath);
            var allnodes = new FBXTree();
            using (BinaryReader reader = new BinaryReader(File.Open(filePath, FileMode.Open)))
            {
                // Read and validate the FBX header
                byte[] headerBytes = reader.ReadBytes(23); // Read the first 23 bytes of the file
                string header = System.Text.Encoding.ASCII.GetString(headerBytes);
                Debug.Log(header);
                if (!header.Contains("Kaydara FBX Binary")) // Check for the FBX binary header signature
                {
                    throw new System.Exception("Invalid FBX file header. This is not a valid FBX binary file.");
                }

                // Read FBX version
                int version = reader.ReadInt32(); // Read the version number
                Debug.Log("FBX Version: " + version);

                while (!this.EndOfContent(reader))
                {
                    var node = ParseNode(reader, version);
                    if (node != null) allnodes.Add(node.Name, node);
                }

                ParseGeometry(allnodes, fileName, mat);
            }
        }

        private void ParseGeometry(FBXTree allnodes, string fileName, Material mat)
        {
            if (!allnodes.KeyVal.ContainsKey("Objects"))
            {
                throw new System.Exception("Objects key is not existed.");
            }

            var mappingType = "";
            var referenceType = "";
            int[] indexBuffer = new int[] { };

            FBXNode geometry = null;
            if (allnodes["Objects"].SubNodes.ContainsKey("Geometry"))
            {
                geometry = allnodes["Objects"].SubNodes["Geometry"] as FBXNode;
            }
            else
            {
                throw new System.Exception("Geometry key is not existed.");
            }

            // vertices
            var vertices = new double[] { };
            if (geometry.SubNodes.ContainsKey("Vertices"))
            {
                vertices = (geometry.SubNodes["Vertices"] as FBXNode).Properties["a"] as double[];
            }

            // uvs
            InfoObject uvInfo = null;
            if (geometry.SubNodes.ContainsKey("LayerElementUV"))
            {
                var LayerElementUV = (geometry.SubNodes["LayerElementUV"] as FBXNode);
                mappingType = System.Text.Encoding.ASCII.GetString((byte[])LayerElementUV.Properties["MappingInformationType"]);
                referenceType = System.Text.Encoding.ASCII.GetString((byte[])LayerElementUV.Properties["ReferenceInformationType"]);
                var uvs = (LayerElementUV.SubNodes["UV"] as FBXNode).Properties["a"] as double[];

                if (referenceType == "IndexToDirect")
                {
                    indexBuffer = (LayerElementUV.SubNodes["UVIndex"] as FBXNode).Properties["a"] as int[];
                }

                uvInfo = new InfoObject
                {
                    dataSize = 2,
                    mappingType = mappingType,
                    referenceType = referenceType,
                    buffer = uvs,
                    indices = indexBuffer
                };
            }

            // indices
            var indices = new int[] { };
            if (geometry.SubNodes.ContainsKey("PolygonVertexIndex"))
            {
                indices = (geometry.SubNodes["PolygonVertexIndex"] as FBXNode).Properties["a"] as int[];
            }

            // normal
            InfoObject normalInfo = null;
            if (geometry.SubNodes.ContainsKey("LayerElementNormal"))
            {
                var LayerElementNormal = (geometry.SubNodes["LayerElementNormal"] as FBXNode);
                mappingType = System.Text.Encoding.ASCII.GetString((byte[])LayerElementNormal.Properties["MappingInformationType"]);
                referenceType = System.Text.Encoding.ASCII.GetString((byte[])LayerElementNormal.Properties["ReferenceInformationType"]);
                var normals = (LayerElementNormal.SubNodes["Normals"] as FBXNode).Properties["a"] as double[];

                indexBuffer = new int[] { };
                if (referenceType == "IndexToDirect")
                {
                    if (LayerElementNormal.SubNodes.ContainsKey("NormalIndex"))
                    {
                        indexBuffer = (LayerElementNormal.SubNodes["NormalIndex"] as FBXNode).Properties["a"] as int[];
                    }
                    else if (LayerElementNormal.SubNodes.ContainsKey("NormalsIndex"))
                    {
                        indexBuffer = (LayerElementNormal.SubNodes["NormalsIndex"] as FBXNode).Properties["a"] as int[];
                    }
                }

                normalInfo = new InfoObject
                {
                    dataSize = 3,
                    mappingType = mappingType,
                    referenceType = referenceType,
                    buffer = normals,
                    indices = indexBuffer
                };
            }

            // colors
            InfoObject colorInfo = null;
            if (geometry.SubNodes.ContainsKey("LayerElementColor"))
            {
                var LayerElementColor = (geometry.SubNodes["LayerElementColor"] as FBXNode);
                mappingType = System.Text.Encoding.ASCII.GetString((byte[])LayerElementColor.Properties["MappingInformationType"]);
                referenceType = System.Text.Encoding.ASCII.GetString((byte[])LayerElementColor.Properties["ReferenceInformationType"]);
                var colors = (LayerElementColor.SubNodes["Colors"] as FBXNode).Properties["a"] as double[];
                if (referenceType == "IndexToDirect")
                {
                    indexBuffer = (LayerElementColor.SubNodes["ColorIndex"] as FBXNode).Properties["a"] as int[];
                }

                colorInfo = new InfoObject
                {
                    dataSize = 4,
                    mappingType = mappingType,
                    referenceType = referenceType,
                    buffer = colors,
                    indices = indexBuffer
                };
            }
            Debug.Log($"colorInfo:{colorInfo}");
            Debug.Log($"done parsing");

            var vertexPositionIndexes = new List<int>();
            var faceNormals = new List<float>();
            var faceUVs = new List<float>();
            var faceColors = new List<float>();
            var polygonIndex = 0;
            var faceLength = 0;

            var vertexBuffer = new List<Vector3>();
            var colorBuffer = new List<Color32>();
            var normalBuffer = new List<Vector3>();
            var uvBuffer = new List<Vector2>();

            var defaultVertexColor = new Color32(255, 255, 255, 255);
            for (var i = 0; i < indices.Length; i++)
            {
                var endOfFace = false;
                var vertexIndex = indices[i];

                // Face index and vertex index arrays are combined in a single array
                // A cube with quad faces looks like this:
                // PolygonVertexIndex: *24 {
                //  a: 0, 1, 3, -3, 2, 3, 5, -5, 4, 5, 7, -7, 6, 7, 1, -1, 1, 7, 5, -4, 6, 0, 2, -5
                //  }
                // Negative numbers mark the end of a face - first face here is 0, 1, 3, -3
                // to find index of last vertex multiply by -1 and subtract 1: -3 * - 1 - 1 = 2
                if (vertexIndex < 0)
                {
                    vertexIndex = vertexIndex ^ -1; // equivalent to ( x * -1 ) - 1
                    indices[i] = vertexIndex;
                    endOfFace = true;
                }

                vertexPositionIndexes.Add(vertexIndex * 3);
                vertexPositionIndexes.Add(vertexIndex * 3 + 1);
                vertexPositionIndexes.Add(vertexIndex * 3 + 2);

                // color
                if (colorInfo != null)
                {
                    var data = GetData(i, polygonIndex, vertexIndex, colorInfo);
                    faceColors.Add((float)data[0]);
                    faceColors.Add((float)data[1]);
                    faceColors.Add((float)data[2]);
                    faceColors.Add((float)data[3]);
                }

                // normal
                if (normalInfo != null)
                {
                    var data = GetData(i, polygonIndex, vertexIndex, normalInfo);
                    faceNormals.Add((float)data[0]);
                    faceNormals.Add((float)data[1]);
                    faceNormals.Add((float)data[2]);
                }

                // uv
                if (uvInfo != null)
                {
                    var data = GetData(i, polygonIndex, vertexIndex, uvInfo);
                    faceUVs.Add((float)data[0]);
                    faceUVs.Add((float)data[1]);
                }

                faceLength++;

                if (endOfFace)
                {
                    for (var j = 2; j < faceLength; j++)
                    {
                        var v1 = new Vector3(
                            (float)vertices[vertexPositionIndexes[0]],
                            (float)vertices[vertexPositionIndexes[1]],
                            (float)vertices[vertexPositionIndexes[2]]);

                        var v2 = new Vector3(
                            (float)vertices[vertexPositionIndexes[(j - 1) * 3]],
                            (float)vertices[vertexPositionIndexes[(j - 1) * 3 + 1]],
                            (float)vertices[vertexPositionIndexes[(j - 1) * 3 + 2]]);

                        var v3 = new Vector3(
                            (float)vertices[vertexPositionIndexes[j * 3]],
                            (float)vertices[vertexPositionIndexes[j * 3 + 1]],
                            (float)vertices[vertexPositionIndexes[j * 3 + 2]]);

                        vertexBuffer.Add(v1);
                        vertexBuffer.Add(v2);
                        vertexBuffer.Add(v3);

                        if (normalInfo != null)
                        {
                            var n1 = new Vector3(
                                faceNormals[0],
                                faceNormals[1],
                                faceNormals[2]);

                            var n2 = new Vector3(
                                faceNormals[(j - 1) * 3],
                                faceNormals[(j - 1) * 3 + 1],
                                faceNormals[(j - 1) * 3 + 2]);

                            var n3 = new Vector3(
                                faceNormals[j * 3],
                                faceNormals[j * 3 + 1],
                                faceNormals[j * 3 + 2]);

                            normalBuffer.Add(n1);
                            normalBuffer.Add(n2);
                            normalBuffer.Add(n3);
                        }

                        if (uvInfo != null)
                        {
                            var uv1 = new Vector2(
                                faceUVs[0],
                                faceUVs[1]);

                            var uv2 = new Vector2(
                                faceUVs[(j - 1) * 2],
                                faceUVs[(j - 1) * 2 + 1]);

                            var uv3 = new Vector2(
                                faceUVs[j * 2],
                                faceUVs[j * 2 + 1]);

                            uvBuffer.Add(uv1);
                            uvBuffer.Add(uv2);
                            uvBuffer.Add(uv3);
                        }

                        if (colorInfo != null)
                        {
                            var color1 = new Color32(
                                (byte)(faceColors[0] * 255f),
                                (byte)(faceColors[1] * 255f),
                                (byte)(faceColors[2] * 255f),
                                (byte)(faceColors[4] * 255f)
                            );

                            var color2 = new Color32(
                                (byte)(faceColors[(j - 1) * 4] * 255f),
                                (byte)(faceColors[(j - 1) * 4 + 1] * 255f),
                                (byte)(faceColors[(j - 1) * 4 + 2] * 255f),
                                (byte)(faceColors[(j - 1) * 4 + 2] * 255f)
                            );

                            var color3 = new Color32(
                                (byte)(faceColors[j * 4] * 255f),
                                (byte)(faceColors[j * 4 + 1] * 255f),
                                (byte)(faceColors[j * 4 + 2] * 255f),
                                (byte)(faceColors[j * 4 + 2] * 255f)
                            );

                            colorBuffer.Add(color1);
                            colorBuffer.Add(color2);
                            colorBuffer.Add(color3);
                        }
                    }

                    polygonIndex++;
                    endOfFace = false;
                    faceLength = 0;

                    vertexPositionIndexes.Clear();
                    faceNormals.Clear();
                    faceUVs.Clear();
                    faceColors.Clear();
                }

            }

            var _vertexBuffer = new NativeArray<GeometryInfo>(vertexBuffer.Count, Allocator.Persistent,
    NativeArrayOptions.UninitializedMemory);

            var _indexBuffer = new NativeArray<int>(vertexBuffer.Count, Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);

            for (var i = 0; i < vertexBuffer.Count; i++)
            {
                var geo = new GeometryInfo();
                geo.pos = vertexBuffer[i];
                geo.color = (colorInfo == null)? defaultVertexColor : colorBuffer[i];
                geo.normal = normalBuffer[i];
                geo.uv = uvBuffer[i];
                _vertexBuffer[i] = geo;
                _indexBuffer[i] = i;
            }

            _context.Post(_ => CreateModel(fileName, mat, _vertexBuffer, _indexBuffer), null);
        }

        private void CreateModel(string fileName, Material mat, NativeArray<GeometryInfo> vertexBuffer, NativeArray<int> indexBuffer)
        {
            var go = new GameObject(fileName);
            var filter = go.AddComponent<MeshFilter>();
            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.material = (mat == null) ? GraphicsSettings.defaultRenderPipeline.defaultMaterial : mat;

            var mesh = new Mesh();
            mesh.SetVertexBufferParams(vertexBuffer.Length, _layout);
            mesh.SetIndexBufferParams(indexBuffer.Length, IndexFormat.UInt32);

            mesh.SetVertexBufferData(vertexBuffer, 0, 0, vertexBuffer.Length);
            mesh.SetIndexBufferData(indexBuffer, 0, 0, indexBuffer.Length);

            var meshDesc = new SubMeshDescriptor(0, indexBuffer.Length, MeshTopology.Triangles);
            mesh.subMeshCount = 1;
            mesh.SetSubMesh(0, meshDesc);

            mesh.indexFormat = IndexFormat.UInt32;
            mesh.MarkDynamic();
            mesh.RecalculateBounds();

            vertexBuffer.Dispose();
            indexBuffer.Dispose();

            filter.mesh = mesh;

            _exportedModel = go;
        }

        private bool EndOfContent(BinaryReader reader)
        {
            // footer size: 160bytes + 16-byte alignment padding
            // - 16bytes: magic
            // - padding til 16-byte alignment (at least 1byte?)
            //    (seems like some exporters embed fixed 15 or 16bytes?)
            // - 4bytes: magic
            // - 4bytes: version
            // - 120bytes: zero
            // - 16bytes: magic
            long size = reader.BaseStream.Length;
            long offset = reader.BaseStream.Position;

            if (size % 16 == 0)
            {
                return ((offset + 160 + 16) & ~0xf) >= size;
            }
            else
            {
                return offset + 160 + 16 >= size;
            }
        }

        private FBXNode ParseNode(BinaryReader reader, int version)
        {
            int endoffset = (version >= 7500) ? (int)reader.ReadInt64() : (int)reader.ReadInt32();
            //Debug.Log($"endoffset: {endoffset}, {reader.BaseStream.Position}");

            int numProperties = (version >= 7500) ? (int)reader.ReadInt64() : (int)reader.ReadInt32();
            //Debug.Log($"numProperties: {numProperties}, {reader.BaseStream.Position}");

            int propertyListLen = (version >= 7500) ? (int)reader.ReadInt64() : (int)reader.ReadInt32();
            //Debug.Log($"propertyListLen: {propertyListLen}, {reader.BaseStream.Position}");

            byte nameLen = reader.ReadByte();
            //Debug.Log($"nameLen: {nameLen}, {reader.BaseStream.Position}");
            byte[] nameBytes = reader.ReadBytes(nameLen);
            string name = System.Text.Encoding.ASCII.GetString(nameBytes);

            if (endoffset == 0) return null;

            List<object> propertyList = new List<object>();

            for (int i = 0; i < numProperties; i++)
            {
                propertyList.Add(ParseProperty(reader));
            }
            
            // Regards the first three elements in propertyList as id, attrName, and attrType
            var id = propertyList.Count > 0 ? propertyList[0] : "";
            var attrName = propertyList.Count > 1 ? propertyList[1] : "";
            var attrType = propertyList.Count > 2 ? propertyList[2] : "";

            // Parse child nodes and properties
            Dictionary<string, object> properties = new Dictionary<string, object>();
            Dictionary<string, object> subNodes = new Dictionary<string, object>();

            bool isSingleProperty = false;

            if (numProperties == 1 && reader.BaseStream.Position == endoffset)
            {
                isSingleProperty = true;
            }
            
            while (reader.BaseStream.Position < endoffset)
            {
                FBXNode node = ParseNode(reader, version);
                if (node == null)
                    continue;

                if (node.IsSingleProperty)
                {
                    // Ensure node.Properties is not null and contains at least one property
                    if (node.PropertyList != null && node.PropertyList.Count > 0)
                    {
                        var value = node.PropertyList[0];
                        if(value != null)
                        {
                            //Debug.Log($"type:{value.GetType()}");
                            if (value.GetType() == typeof(int[]) || value.GetType() == typeof(double[]) || value.GetType() == typeof(bool[]))
                            {
                                subNodes[node.Name] = node;
                                node.Properties["a"] = value;
                            }
                            else
                            {
                                properties[node.Name] = value;
                            }
                        }
                        //Debug.Log($"{node.Name},{value}");
                    }
                    continue;
                }

                if (name == "Connections" && node.Name == "C")
                {
                    var array = new List<object>();
                    foreach(var prop in node.PropertyList)
                    {
                        array.Add(prop);
                    }

                    if (!properties.ContainsKey("connections"))
                        properties["connections"] = new List<object[]>();

                    //Debug.Log($">>>>>> node.Properties.Values:{node.Properties.Values.Count}");

                    List<object[]> connectionList = (List<object[]>)properties["connections"];
                    if (node.Properties != null && node.Properties.Count > 0)
                        connectionList.Add(array.ToArray());
                    continue;
                }

                if (node.Name == "Properties70")
                {
                    foreach (var keyValuePair in node.Properties)
                        properties[keyValuePair.Key] = keyValuePair.Value;
                    continue;
                }

                if (name == "Properties70" && node.Name == "P")
                {
                    if (node.PropertyList != null && node.PropertyList.Count > 0)
                    {
                        
                        var innerPropName = System.Text.Encoding.ASCII.GetString((byte[])node.PropertyList[0]);
                        var innerPropType1 = System.Text.Encoding.ASCII.GetString((byte[])node.PropertyList[1]);
                        var innerPropType2 = System.Text.Encoding.ASCII.GetString((byte[])node.PropertyList[2]);
                        var innerPropFlag = System.Text.Encoding.ASCII.GetString((byte[])node.PropertyList[3]);
                        object[] innerPropValue;

                        if (innerPropName.IndexOf("Lcl ") == 0)
                        {
                            innerPropName = innerPropName.Replace("Lcl ", "Lcl_");
                        }

                        if (innerPropType1.IndexOf("Lcl ") == 0)
                        {
                            innerPropType1 = innerPropType1.Replace("Lcl ", "Lcl_");
                        }

                        if (innerPropType1.IndexOf("ColorRGB") == 0 || innerPropType1.IndexOf("Vector") == 0 || innerPropType1.IndexOf("Vector3D") == 0 || innerPropType1.IndexOf("Lcl_") == 0)
                        {
                            innerPropValue = new object[] {
                                    node.PropertyList[4],
                                    node.PropertyList[5],
                                    node.PropertyList[6]
                            };
                        }
                        else
                        {
                            //Debug.Log(node.PropertyList.Count);
                            if(node.PropertyList.Count >= 5)
                            {
                                if(node.PropertyList[4].GetType() != typeof(byte[]))
                                {
                                    innerPropValue = new object[] { node.PropertyList[4] };
                                } else
                                {
                                    var val = System.Text.Encoding.ASCII.GetString((byte[])node.PropertyList[4]);
                                    innerPropValue = new object[] { val };
                                }
                            } else
                            {
                                innerPropValue = new object[] { };
                            }
                        }

                        properties[innerPropName] = new Dictionary<string, object> {
                            {"type", innerPropType1 },
                            {"type2", innerPropType2 },
                            {"flag", innerPropFlag },
                            {"value", innerPropValue }
                        };
                    }
                    continue;
                }

                if (!subNodes.ContainsKey(node.Name))
                {
                    if(node.Id != null)
                    {
                        if (node.Id.GetType() == typeof(float) || node.Id.GetType() == typeof(double) || node.Id.GetType() == typeof(int))
                        {
                            subNodes[node.Name] = new Dictionary<object, object>();
                            (subNodes[node.Name] as Dictionary<object, object>)[node.Id] = node;
                        }
                        else
                        {
                            subNodes[node.Name] = node;
                        }
                    } else
                    {
                        subNodes[node.Name] = node;
                    }
                }
                else
                {
                    if(node.Id != null)
                    {
                        if (node.Id.GetType() == typeof(string))
                        {
                            if ((node.Id as string) == "")
                            {
                                if (subNodes[node.Name].GetType() != typeof(List<object>))
                                {
                                    subNodes[node.Name] = new List<object>();
                                    ((List<object>)subNodes[node.Name]).Add(subNodes[node.Name]);
                                }

                                List<object> nodeList = (List<object>)subNodes[node.Name];
                                nodeList.Add(node);
                            }
                        } else
                        {
                            if (subNodes[node.Name].GetType() == typeof(Dictionary<object, object>))
                            {
                                if ((subNodes[node.Name] as Dictionary<object, object>)[node.Id] == null)
                                {
                                    (subNodes[node.Name] as Dictionary<object, object>)[node.Id] = node;
                                }
                                else
                                {
                                    if ((subNodes[node.Name] as Dictionary<object, object>)[node.Id].GetType() != typeof(List<object>))
                                    {
                                        (subNodes[node.Name] as Dictionary<object, object>)[node.Id] = new List<object>();
                                        ((subNodes[node.Name] as Dictionary<object, object>)[node.Id] as List<object>).Add((subNodes[node.Name] as Dictionary<object, object>)[node.Id]);
                                    }
                                    else
                                    {
                                        ((subNodes[node.Name] as Dictionary<object, object>)[node.Id] as List<object>).Add(node);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        if(subNodes[node.Name].GetType() == typeof(Dictionary<object, object>))
                        {
                            if ((subNodes[node.Name] as Dictionary<object, object>)[node.Id] == null)
                            {
                                (subNodes[node.Name] as Dictionary<object, object>)[node.Id] = node;
                            } else
                            {
                                if ((subNodes[node.Name] as Dictionary<object, object>)[node.Id].GetType() != typeof(List<object>)){
                                    (subNodes[node.Name] as Dictionary<object, object>)[node.Id] = new List<object>();
                                    ((subNodes[node.Name] as Dictionary<object, object>)[node.Id] as List<object>).Add((subNodes[node.Name] as Dictionary<object, object>)[node.Id]);
                                } else
                                {
                                    ((subNodes[node.Name] as Dictionary<object, object>)[node.Id] as List<object>).Add(node);
                                }
                            }
                        }
                    }
                }

            }

            //Debug.Log(subNodes.Count);
            return new FBXNode(name, id, attrName, attrType, properties, propertyList, subNodes, isSingleProperty);
        }

        private object ParseProperty(BinaryReader reader)
        {
            char type;

            // Check if there is enough data available in the stream to read a character
            if (reader.BaseStream.Position < reader.BaseStream.Length - sizeof(char))
            {
                // Read a character from the stream
                type = reader.ReadChar();
            }
            else
            {
                // Handle end of stream gracefully
                // For example, throw an exception or return a default value
                return null;
            }

            //Debug.Log($"ParseProperty: {type}");

            switch (type)
            {
                case 'C':
                    return reader.ReadBoolean();

                case 'D':
                    return reader.ReadDouble();

                case 'F':
                    return reader.ReadSingle();

                case 'I':
                    return reader.ReadInt32();

                case 'L':
                    return reader.ReadInt64();

                case 'R':
                    int length = reader.ReadInt32();
                    return reader.ReadBytes(length);

                case 'S':
                    length = reader.ReadInt32();
                    return reader.ReadBytes(length);

                case 'Y':
                    return reader.ReadInt16();

                case 'b':
                case 'c':
                case 'd':
                case 'f':
                case 'i':
                case 'l':
                    int arrayLength = (int)reader.ReadInt32();
                    int encoding = (int)reader.ReadInt32(); // 0: non-compressed, 1: compressed
                    int compressedLength = (int)reader.ReadInt32();

                    //Debug.Log($"arrayLength:{arrayLength}, encoding: {encoding}, compressedLength: {compressedLength}");
                    if (encoding == 0)
                    {
                        switch (type)
                        {
                            case 'b':
                            case 'c':
                                bool[] boolArray = new bool[arrayLength];
                                for (int i = 0; i < arrayLength; i++)
                                {
                                    boolArray[i] = reader.ReadBoolean();
                                }
                                return boolArray;
                            case 'd':
                                double[] floatArray = new double[arrayLength];
                                for (int i = 0; i < arrayLength; i++)
                                {
                                    floatArray[i] = reader.ReadDouble();
                                }
                                return floatArray;
                            case 'f':
                                floatArray = new double[arrayLength];
                                for (int i = 0; i < arrayLength; i++)
                                {
                                    floatArray[i] = reader.ReadSingle();
                                }
                                return floatArray;
                            case 'i':
                                int[] intArray = new int[arrayLength];
                                for (int i = 0; i < arrayLength; i++)
                                {
                                    intArray[i] = reader.ReadInt32();
                                }
                                return intArray;
                            case 'l':
                                intArray = new int[arrayLength];
                                for (int i = 0; i < arrayLength; i++)
                                {
                                    intArray[i] = (int)reader.ReadInt64();
                                }
                                return intArray;
                        }
                    }

                    // note: https://stackoverflow.com/questions/20850703/cant-inflate-with-c-sharp-using-deflatestream
                    byte[] magic = reader.ReadBytes(2);
                    compressedLength -= 2;
                    byte[] compressedData = reader.ReadBytes(compressedLength);
                    byte[] inflated;
                    using (MemoryStream compressedStream = new MemoryStream(compressedData))
                    using (MemoryStream decompressedStream = new MemoryStream())
                    {
                        // Reset MemoryStream positions
                        compressedStream.Position = 0;
                        decompressedStream.Position = 0;

                        using (DeflateStream deflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress))
                        {
                            deflateStream.CopyTo(decompressedStream);
                        }

                        inflated = decompressedStream.ToArray();
                    }

                    using (BinaryReader reader2 = new BinaryReader(new MemoryStream(inflated)))
                    {
                        switch (type)
                        {
                            case 'b':
                            case 'c':
                                bool[] boolArray = new bool[arrayLength];
                                for (int i = 0; i < arrayLength; i++)
                                {
                                    boolArray[i] = reader2.ReadBoolean();
                                }
                                return boolArray;
                            case 'd':
                                double[] floatArray = new double[arrayLength];
                                for (int i = 0; i < arrayLength; i++)
                                {
                                    floatArray[i] = reader2.ReadDouble();
                                }
                                //Debug.Log($">>>> floatArray:{floatArray.Length}");
                                return floatArray;
                            case 'f':
                                floatArray = new double[arrayLength];
                                for (int i = 0; i < arrayLength; i++)
                                {
                                    floatArray[i] = reader2.ReadSingle();
                                }
                                return floatArray;
                            case 'i':
                                int[] intArray = new int[arrayLength];
                                for (int i = 0; i < arrayLength; i++)
                                {
                                    intArray[i] = reader2.ReadInt32();
                                }
                                return intArray;
                            case 'l':
                                intArray = new int[arrayLength];
                                for (int i = 0; i < arrayLength; i++)
                                {
                                    intArray[i] = (int)reader2.ReadInt64();
                                }
                                return intArray;
                            default:
                                throw new Exception("Unknown property type " + type);
                        }
                    }
                default:
                    return null;
            }
        }

        public double[] GetData(int polygonVertexIndex, int polygonIndex, int vertexIndex, InfoObject infoObject)
        {
            var mapping = infoObject.mappingType;
            var reference = infoObject.referenceType;

            if (mapping == "ByPolygonVertex")
            {
                if (reference == "Direct")
                {
                    int from = polygonVertexIndex * infoObject.dataSize;
                    int to = (polygonVertexIndex * infoObject.dataSize) + infoObject.dataSize;
                    return Slice(infoObject.buffer, from, to);
                }
                else if (reference == "IndexToDirect")
                {
                    int index = infoObject.indices[polygonVertexIndex];
                    int from = index * infoObject.dataSize;
                    int to = (index * infoObject.dataSize) + infoObject.dataSize;
                    return Slice(infoObject.buffer, from, to);
                }
            }
            else if (mapping == "ByPolygon")
            {
                if (reference == "Direct")
                {
                    int from = polygonIndex * infoObject.dataSize;
                    int to = (polygonIndex * infoObject.dataSize) + infoObject.dataSize;
                    return Slice(infoObject.buffer, from, to);
                }
                else if (reference == "IndexToDirect")
                {
                    int index = infoObject.indices[polygonIndex];
                    int from = index * infoObject.dataSize;
                    int to = (index * infoObject.dataSize) + infoObject.dataSize;
                    return Slice(infoObject.buffer, from, to);
                }
            }
            else if (mapping == "ByVertice")
            {
                if (reference == "Direct")
                {
                    int from = vertexIndex * infoObject.dataSize;
                    int to = (vertexIndex * infoObject.dataSize) + infoObject.dataSize;
                    return Slice(infoObject.buffer, from, to);
                }
            }
            else if (mapping == "AllSame")
            {
                if (reference == "IndexToDirect")
                {
                    int from = infoObject.indices[0] * infoObject.dataSize;
                    int to = (infoObject.indices[0] * infoObject.dataSize) + infoObject.dataSize;
                    return Slice(infoObject.buffer, from, to);
                }
            }

            return null;
        }

        private double[] Slice(double[] buffer, int from, int to)
        {
            int length = to - from;
            double[] result = new double[length];

            for (int i = from, j = 0; i < to; i++, j++)
            {
                result[j] = buffer[i];
            }

            return result;
        }

    }
}

public class FBXNode
{
    public string Name { get; }
    public Dictionary<string, object> Properties { get; }
    public List<object> PropertyList { get; }
    public Dictionary<string, object> SubNodes { get; }
    public bool IsSingleProperty { get; }
    public object Id { get; }
    public object AttrName { get; }
    public object AttrType { get; }

    public FBXNode(string name, object id, object attrName, object attrType, Dictionary<string, object> properties, List<object> propertyList, Dictionary<string, object> subNodes, bool isSingleProperty)
    {
        Name = name;
        AttrName = attrName;
        AttrType = attrType;
        Properties = properties;
        PropertyList = propertyList;
        SubNodes = subNodes;
        IsSingleProperty = isSingleProperty;
    }
}

public class FBXTree
{
    public Dictionary<string, FBXNode> KeyVal => keyValuePairs;

    // Constructor
    public FBXTree() { }

    // Add method to add key-value pairs to the tree
    public void Add(string key, FBXNode val)
    {
        this[key] = val;
    }

    // Dictionary to store key-value pairs
    private Dictionary<string, FBXNode> keyValuePairs = new Dictionary<string, FBXNode>();

    // Indexer to access key-value pairs using string keys
    public FBXNode this[string key]
    {
        get
        {
            return keyValuePairs[key];
        }
        set
        {
            keyValuePairs[key] = value;
        }
    }
}

public static class BinaryReaderExtensions
{
    public static int ReadInt32LE(this BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(4);
        Array.Reverse(bytes); // Convert from little-endian to big-endian
        return BitConverter.ToInt32(bytes, 0);
    }

    public static long ReadInt64LE(this BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(8);
        Array.Reverse(bytes); // Convert from little-endian to big-endian
        return BitConverter.ToInt64(bytes, 0);
    }

    public static float ReadSingleLE(this BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(4);
        Array.Reverse(bytes); // Convert from little-endian to big-endian
        return BitConverter.ToSingle(bytes, 0);
    }

    public static double ReadDoubleLE(this BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(8);
        Array.Reverse(bytes); // Convert from little-endian to big-endian
        return BitConverter.ToDouble(bytes, 0);
    }

    public static string ReadNullTerminatedString(this BinaryReader reader)
    {
        const int MaxStringLength = 1024; // Maximum length to prevent infinite loops

        // Read characters until a null character (0x00) is encountered or maximum length is reached
        string str = "";
        for (int i = 0; i < MaxStringLength; i++)
        {
            char c = reader.ReadChar();
            if (c == '\0')
            {
                break; // Null character found, end of string
            }
            str += c;
        }
        return str;
    }
}

public class InfoObject
{
    public string mappingType;
    public string referenceType;
    public double[] buffer;
    public int dataSize;
    public int[] indices;
}