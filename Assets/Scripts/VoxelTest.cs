using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Model2VoxelConverter;
using System.IO;
using System;
using System.Linq;
using System.IO.Compression;
using UnityEngine.Networking;
using System.Threading.Tasks;
using UnityEngine.Rendering;

public class VoxelTest : MonoBehaviour, IVoxel
{
    [SerializeField] private Voxel _voxel;
    [SerializeField] private Button _button;
    [SerializeField] private Button _exportButton;
    [SerializeField] private MeshRenderer _targetObj;

    private UnityFBXLoader _loader;
    private VoxelAnimationTest _voxelAnimationTest = null;

    // Start is called before the first frame update
    void Start()
    {
        _voxel.listener = this;
        _button.onClick.AddListener(GenerateVoxel);
        _exportButton.onClick.AddListener(Export);

        _voxelAnimationTest = GetComponent<VoxelAnimationTest>();
    }

    private void GenerateVoxel()
    {
        _voxel.GenerateVoxel(60, _targetObj);
    }

    private async Task<Texture2D> LoadTextureAsync(string path)
    {
        if (path.Contains("://") || path.Contains("://"))
        {
            UnityWebRequest www = UnityWebRequestTexture.GetTexture(path);
            await www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                return DownloadHandlerTexture.GetContent(www);
            }
            else
            {
                Debug.LogError("Failed to load texture: " + www.error);
                return null;
            }
        }
        else
        {
            byte[] fileData = File.ReadAllBytes(path);
            Texture2D texture = new Texture2D(2, 2);
            texture.LoadImage(fileData);
            return texture;
        }
    }

    [ContextMenu(nameof(Import))]
    private async void Import()
    {
        var filePath = System.IO.Path.Combine(Application.streamingAssetsPath, "Puma_right.fbx");
        _loader = new UnityFBXLoader();
        _loader.OnLoaded += OnLoaded;

        var texPath = System.IO.Path.Combine(Application.streamingAssetsPath, "puma_rider_play_right_albedo_1k.png");
        var tex = await LoadTextureAsync(texPath);
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.mainTexture = tex;

        _loader.Load(filePath, mat);
    }

    private void OnLoaded(GameObject obj)
    {
        _loader.OnLoaded -= OnLoaded;
        var scale = obj.transform.localScale;
        scale *= 3f;
        obj.transform.localScale = scale;
        obj.AddComponent<MeshCollider>();
        _voxel.GenerateVoxel(60, obj.GetComponent<MeshRenderer>());
    }

    private void Export()
    {
        string filePath = Application.dataPath + "/ExportedModel.ply";
        _voxel.ExportPly(filePath);
    }

    [ContextMenu(nameof(TestGenerate))]
    private void TestGenerate()
    {
        _voxel.GenerateVoxel(30, _targetObj);
    }

    public void OnGenerated()
    {
        Debug.Log($">>>>>>[{nameof(VoxelTest)}] OnGenerated");
        if(_voxelAnimationTest != null)
        {
            _voxelAnimationTest.StartAnimation(_voxel.voxelObj);
        }
    }

    public void OnExported()
    {
        Debug.Log($">>>>>>[{nameof(VoxelTest)}] OnExported");
    }
}