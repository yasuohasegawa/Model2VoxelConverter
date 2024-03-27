using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VoxelAnimationTest : MonoBehaviour
{
    [SerializeField] private ComputeShader _computeShader;

    private MeshFilter _meshFilter;
    private ComputeBuffer _vertexBuffer;
    private ComputeBuffer _outVertexBuffer;
    private Vector3[] _modifiedVertices;
    private Material _material;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    public void StartAnimation(GameObject voxel)
    {
        _meshFilter = voxel.GetComponent<MeshFilter>();
        _material = voxel.GetComponent<MeshRenderer>().material;

        int vertexCount = _meshFilter.mesh.vertexCount;
        _vertexBuffer = new ComputeBuffer(vertexCount, sizeof(float) * 3);
        _vertexBuffer.SetData(_meshFilter.mesh.vertices);

        _outVertexBuffer = new ComputeBuffer(vertexCount, sizeof(float) * 3);
        _outVertexBuffer.SetData(_meshFilter.mesh.vertices);

        _modifiedVertices = new Vector3[vertexCount];

        _computeShader.SetBuffer(0, "vertices", _vertexBuffer);
        _computeShader.SetBuffer(0, "outputVertices", _outVertexBuffer);
        _computeShader.SetInt("vertexCount", vertexCount);
    }

    // Update is called once per frame
    void Update()
    {
        if(_modifiedVertices != null)
        {
            _computeShader.SetFloat("time", Time.time);
            _computeShader.Dispatch(0, Mathf.CeilToInt(_meshFilter.mesh.vertexCount / 24.0f), 1, 1);

            _material.SetBuffer("_modifiedVertices", _outVertexBuffer);

            // CPU version to update the vertices. TODO: pass the following buffer to the vertex buffer
            //_vertexBuffer.GetData(_modifiedVertices);
            //_meshFilter.mesh.vertices = _modifiedVertices;
        }
    }

    void OnDestroy()
    {
        if (_vertexBuffer != null)
            _vertexBuffer.Release();
        if (_outVertexBuffer != null)
            _outVertexBuffer.Release();
    }
}
