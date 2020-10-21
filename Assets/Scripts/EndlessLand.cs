using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EndlessLand : MonoBehaviour
{
    const float scale = 2f;

    const float viewerMoveTreshholdForChunkUpdate = 25f;
    const float sqrViewerMoveTreshholdForChunkUpdate = viewerMoveTreshholdForChunkUpdate * viewerMoveTreshholdForChunkUpdate;

    public Transform viewer;

    public static Vector2 viewerPosition;
    Vector2 oldPositionViewer;

    int chunkSize;
    int chunksVisiblesInViewDistance;

    public Material mapMaterial;

    public static MapGenerator mapGenerator;

    Dictionary<Vector2, TerrainChunk> terrainChunkDictionary = new Dictionary<Vector2, TerrainChunk>();
    static List<TerrainChunk> terrainChunkVisibleLastFrame = new List<TerrainChunk>();

    public LODinfo[] detailLevels;
    public static float maxViewDistance;

    private void Start()
    {
        maxViewDistance = detailLevels[detailLevels.Length - 1].visibleDistanceThreshold;
        mapGenerator = FindObjectOfType<MapGenerator>();
        chunkSize = MapGenerator.mapChunkSize - 1;
        chunksVisiblesInViewDistance = Mathf.RoundToInt(maxViewDistance / chunkSize);

        UpdateVisibleChunks();
    }
    private void Update()
    {
        viewerPosition = new Vector2(viewer.position.x, viewer.position.z) / scale;
        if((oldPositionViewer - viewerPosition).sqrMagnitude > sqrViewerMoveTreshholdForChunkUpdate)
        {
            UpdateVisibleChunks();
            oldPositionViewer = viewerPosition;
        }
    }
    void UpdateVisibleChunks()
    {
        for (int i = 0; i < terrainChunkVisibleLastFrame.Count; i++)
        {
            terrainChunkVisibleLastFrame[i].SetVisible(false);
        }
        terrainChunkVisibleLastFrame.Clear();

        int currentChunkCoordX = Mathf.RoundToInt(viewerPosition.x / chunkSize);
        int currentChunkCoordY = Mathf.RoundToInt(viewerPosition.y / chunkSize);

        for (int yOffset = -chunksVisiblesInViewDistance; yOffset <= chunksVisiblesInViewDistance; yOffset++)
        {
            for (int xOffset = -chunksVisiblesInViewDistance; xOffset <= chunksVisiblesInViewDistance; xOffset++)
            {
                Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);

                if (terrainChunkDictionary.ContainsKey(viewedChunkCoord))
                {
                    terrainChunkDictionary[viewedChunkCoord].UpdateTerrainChunk();
                }
                else
                {
                    terrainChunkDictionary.Add(viewedChunkCoord, new TerrainChunk(viewedChunkCoord, chunkSize, detailLevels, transform, mapMaterial));
                }
            }
        }
    }
    public class TerrainChunk
    {
        Vector2 position;
        GameObject meshObject;
        Bounds bounds;

        MapData mapData;
        MeshRenderer meshRenderer;
        MeshFilter meshFilter;
        MeshCollider meshCollider;

        LODinfo[] detailsLevel;
        LODMesh[] lODMeshes;
        LODMesh collisionLODMesh;

        bool mapDataReceived;
        int previousLODIndex = -1;

        public TerrainChunk(Vector2 coord, int size, LODinfo[] detailsLevel, Transform parent, Material material)
        {
            this.detailsLevel = detailsLevel;

            position = coord * size;
            Vector3 positionV3 = new Vector3(position.x, 0, position.y);

            bounds = new Bounds(position, Vector2.one * size);

            meshObject = new GameObject("Terrain Chunk");
            meshRenderer = meshObject.AddComponent<MeshRenderer>();
            meshFilter = meshObject.AddComponent<MeshFilter>();
            meshCollider = meshObject.AddComponent<MeshCollider>();

            meshRenderer.material = material;

            meshObject.transform.position = positionV3 * scale;
            meshObject.transform.parent = parent;
            meshObject.transform.localScale = Vector3.one * scale;

            SetVisible(false);

            lODMeshes = new LODMesh[detailsLevel.Length];
            for (int i = 0; i < detailsLevel.Length; i++)
            {
                lODMeshes[i] = new LODMesh(detailsLevel[i].lod, UpdateTerrainChunk);
                if (detailsLevel[i].useForCollider)
                {
                    collisionLODMesh = lODMeshes[i];
                }
            }

            mapGenerator.RequestMapData(position, OnMapDataReceived);
        }
        void OnMapDataReceived(MapData mapData)
        {
            this.mapData = mapData;
            mapDataReceived = true;

            Texture2D texture = TextureGenerator.TextureFromColourMap(mapData.colourMap, MapGenerator.mapChunkSize, MapGenerator.mapChunkSize);
            meshRenderer.material.mainTexture = texture;

            UpdateTerrainChunk();
        }
        void OnMeshDataReceived(MeshData meshdata)
        {
            meshFilter.mesh = meshdata.CreateMesh();
        }
        public void UpdateTerrainChunk()
        {
            if (mapDataReceived)
            {
                float viewDistanceFromNearestEdge = Mathf.Sqrt(bounds.SqrDistance(viewerPosition));
                bool visible = viewDistanceFromNearestEdge <= maxViewDistance;

                if (visible)
                {
                    int lODIndex = 0;

                    for (int i = 0; i < detailsLevel.Length - 1; i++)
                    {
                        if (viewDistanceFromNearestEdge > detailsLevel[i].visibleDistanceThreshold)
                        {
                            lODIndex = i + 1;
                        }
                        else
                        {
                            break;
                        }
                    }
                    if (lODIndex != previousLODIndex)
                    {
                        LODMesh lODMesh = lODMeshes[lODIndex];
                        if (lODMesh.hasMesh)
                        {
                            previousLODIndex = lODIndex;
                            meshFilter.mesh = lODMesh.mesh;
                            meshCollider.sharedMesh = lODMesh.mesh;
                        }
                        else if (!lODMesh.hasRequestedMesh)
                        {
                            lODMesh.RequestMesh(mapData);
                        }
                    }
                    if(lODIndex == 0)
                    {
                        if (collisionLODMesh.hasMesh)
                        {
                            meshCollider.sharedMesh = collisionLODMesh.mesh;
                        }
                        else if(!collisionLODMesh.hasRequestedMesh)
                        {
                            collisionLODMesh.RequestMesh(mapData);
                        }
                    }
                    terrainChunkVisibleLastFrame.Add(this);
                }
                SetVisible(visible);
            }
        }
        public void SetVisible(bool visible)
        {
            meshObject.SetActive(visible);
        }
        public bool IsVisible()
        {
            return meshObject.activeSelf;
        }
    }

    class LODMesh
    {
        public Mesh mesh;
        public bool hasRequestedMesh;
        public bool hasMesh;
        public int lod;
        System.Action updateCallback;

        public LODMesh(int lod, System.Action updateCallback)
        {
            this.lod = lod;
            this.updateCallback = updateCallback;
        }
        public void RequestMesh(MapData mapData)
        {
            mapGenerator.RequestMeshData(mapData, lod, OnMeshDataReceived);
            hasRequestedMesh = true;
        }
        void OnMeshDataReceived(MeshData meshData)
        {
            mesh = meshData.CreateMesh();
            hasMesh = true;
        }
    }
}
[System.Serializable]
public struct LODinfo
{
    public bool useForCollider;
    public int lod;
    public float visibleDistanceThreshold;
}

