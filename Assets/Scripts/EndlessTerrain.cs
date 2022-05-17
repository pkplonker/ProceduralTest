using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EndlessTerrain : MonoBehaviour
{
	private const float viewerMoveThreshholdForChunkUpdate = 25f;

	private const float sqrViewerMoveThreshholdForChunkUpdate =
		viewerMoveThreshholdForChunkUpdate * viewerMoveThreshholdForChunkUpdate;

	private Vector2 viewerPositionOld;
	[SerializeField] private LODInfo[] detailLevels;
	private static float maxViewDistance;
	[SerializeField] Transform viewer;
	[field: SerializeField] public static Vector2 viewerPosition { get; private set; }
	private int chunkSize;
	private int chunksVisableInViewDistance;
	private Dictionary<Vector2, TerrainChunk> terrainChunkDictionary = new Dictionary<Vector2, TerrainChunk>();
	private List<TerrainChunk> terrainChunksVisableLastUpdate = new List<TerrainChunk>();
	private static MapGenerator mapGenerator;
	public Material mapMaterial;

	private void Start()
	{
		chunkSize = MapGenerator.mapChunkSize - 1;
		maxViewDistance = detailLevels[detailLevels.Length - 1].visableDistanceThreshold;
		chunksVisableInViewDistance = Mathf.RoundToInt(maxViewDistance / chunkSize);
		mapGenerator = FindObjectOfType<MapGenerator>();
		UpdateVisableChunks();

	}

	private void Update()
	{
		viewerPosition = new Vector2(viewer.position.x, viewer.position.z);
		if ((viewerPositionOld - viewerPosition).sqrMagnitude > sqrViewerMoveThreshholdForChunkUpdate)
		{
			viewerPositionOld = viewerPosition;
			UpdateVisableChunks();
		}
	}

	private void UpdateVisableChunks()
	{
		foreach (var chunk in terrainChunksVisableLastUpdate)
		{
			chunk.SetVisable(false);
		}

		terrainChunksVisableLastUpdate.Clear();
		int currentChunkCoordX = Mathf.RoundToInt(viewer.position.x / chunkSize);
		int currentChunkCoordY = Mathf.RoundToInt(viewer.position.y / chunkSize);
		for (int yOffset = -chunksVisableInViewDistance; yOffset < chunksVisableInViewDistance; yOffset++)
		{
			for (int xOffset = -chunksVisableInViewDistance; xOffset < chunksVisableInViewDistance; xOffset++)
			{
				Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);
				if (terrainChunkDictionary.ContainsKey(viewedChunkCoord))
				{
					terrainChunkDictionary[viewedChunkCoord].UpdateTerrainChunk();
					if (terrainChunkDictionary[viewedChunkCoord].isVisable)
					{
						terrainChunksVisableLastUpdate.Add(terrainChunkDictionary[viewedChunkCoord]);
					}
				}
				else
				{
					terrainChunkDictionary.Add(viewedChunkCoord,
						new TerrainChunk(viewedChunkCoord, chunkSize, transform, mapMaterial, detailLevels));
				}
			}
		}
	}

	private class TerrainChunk
	{
		private Vector2 position;
		private GameObject meshObject;
		private Bounds bounds;
		private MeshRenderer meshRenderer;
		private MeshFilter meshFilter;
		private LODInfo[] detailLevels;
		private LODMesh[] lodMeshes;
		private MapData mapData;
		private bool mapDataReceived;
		private int previousLevelOfDetailIndex = -1;

		public TerrainChunk(Vector2 coord, int size, Transform parent, Material material, LODInfo[] detailLevels)
		{
			this.detailLevels = detailLevels;
			position = coord * size;
			bounds = new Bounds(position, Vector2.one * size);
			Vector3 positionV3 = new Vector3(position.x, 0, position.y);
			meshObject = new GameObject("TerrainChunk");
			meshRenderer = meshObject.AddComponent<MeshRenderer>();
			meshFilter = meshObject.AddComponent<MeshFilter>();
			meshRenderer.material = material;
			meshObject.transform.position = positionV3;
			meshObject.transform.parent = parent;
			SetVisable(false);
			lodMeshes = new LODMesh[detailLevels.Length];
			for (int i = 0; i < detailLevels.Length; i++)
			{
				lodMeshes[i] = new LODMesh(detailLevels[i].lod, UpdateTerrainChunk);
			}

			mapGenerator.RequestMapData(OnMapDataReceived, position);
		}

		public bool isVisable => meshObject.activeSelf;


		void OnMapDataReceived(MapData mapData)
		{
			this.mapData = mapData;
			mapDataReceived = true;
			Texture2D texture = TextureGenerator.TextureFromColorMap(mapData.colorMap, MapGenerator.mapChunkSize,
				MapGenerator.mapChunkSize);
			meshRenderer.material.mainTexture = texture;
			UpdateTerrainChunk();
		}

		public void UpdateTerrainChunk()
		{
			if (!mapDataReceived) return;
			float viewerDistanceFromNearestEdge = Mathf.Sqrt(bounds.SqrDistance(viewerPosition));
			bool visable = viewerDistanceFromNearestEdge <= maxViewDistance;
			if (visable)
			{
				int lodIndex = 0;
				for (int i = 0; i < detailLevels.Length - 1; i++)
				{
					if (viewerDistanceFromNearestEdge > detailLevels[i].visableDistanceThreshold)
					{
						lodIndex = i + 1;
					}
					else
					{
						break;
					}
				}

				if (lodIndex != previousLevelOfDetailIndex)
				{
					LODMesh lodMesh = lodMeshes[lodIndex];
					if (lodMesh.hasMesh)
					{
						previousLevelOfDetailIndex = lodIndex;
						meshFilter.mesh = lodMesh.mesh;
					}
					else if (!lodMesh.hasRequestedMesh)
					{
						lodMesh.RequestMesh(mapData);
					}
				}
			}

			SetVisable(visable);
		}

		public void SetVisable(bool visable)
		{
			meshObject.SetActive(visable);
		}
	}

	class LODMesh
	{
		public Mesh mesh;
		public bool hasRequestedMesh;
		public bool hasMesh;
		private int lod;
		private Action updateCallback;

		public LODMesh(int lod, Action updateCallback)
		{
			this.lod = lod;
			this.updateCallback = updateCallback;
		}

		private void OnMeshDataReceived(MeshData meshData)
		{
			mesh = meshData.CreateMesh();
			hasMesh = true;
			updateCallback?.Invoke();
		}

		public void RequestMesh(MapData mapData)
		{
			hasRequestedMesh = true;
			mapGenerator.RequestMeshData(mapData, OnMeshDataReceived, lod);
		}
	}

	[Serializable]
	public struct LODInfo
	{
		public int lod;
		public float visableDistanceThreshold;
	}
}