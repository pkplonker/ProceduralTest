using System;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;

public class MapGenerator : MonoBehaviour
{
	public enum DrawMode
	{
		NoiseMap,
		ColorMap,
		DrawMesh
	}
	
	public DrawMode drawMode;
	[SerializeField] private float noiseScale;
	[Min(0)] [SerializeField] private int octaves;
	[Range(0, 1)] [SerializeField] private float persistance;
	[Min(1)] [SerializeField] private float lacunarity;
	[SerializeField] private int seed;
	[SerializeField] private Vector2 offset;
	[SerializeField] private TerrainType[] regions;
	[SerializeField] private float meshHeightMultiplier;
	[SerializeField] private AnimationCurve meshHeightCurve;
	public const int mapChunkSize = 241;
	private Queue<MapThreadInfo<MapData>> mapDataThreadInfoQueue = new Queue<MapThreadInfo<MapData>>();
	private Queue<MapThreadInfo<MeshData>> meshDataThreadInfoQueue = new Queue<MapThreadInfo<MeshData>>();

	[Range(0,6)] [SerializeField] private int levelOfDetailPreview=1;
	[field: SerializeField] public bool autoUpdate { get; private set; }
	private MapDisplay mapDisplay;
	private MapData GenerateMapData(Vector2 centre)
	{
		float[,] noiseMap = Noise.GenerateNoise(mapChunkSize, mapChunkSize, seed, noiseScale, octaves, persistance, lacunarity,
			 centre+offset);
		Color[] colorMap = new Color[mapChunkSize * mapChunkSize];
		for (int y = 0; y < mapChunkSize; y++)
		{
			for (int x = 0; x < mapChunkSize; x++)
			{
				float currentHeight = noiseMap[x, y];
				for (int i = 0; i < regions.Length; i++)
				{
					if (currentHeight <= regions[i].height)
					{
						colorMap[y * mapChunkSize + x] = regions[i].color;
						break;
					}
				}
			}
		}

		return new MapData(noiseMap, colorMap);
	}

	public void DrawMapInEditor()
	{
		MapData mapData = GenerateMapData(Vector2.zero);
		if (mapDisplay == null)
		{
			mapDisplay = FindObjectOfType<MapDisplay>();
		}

		if (mapDisplay == null)
		{
			Debug.LogWarning("No Map Display found");
			return;
		}

		switch (drawMode)
		{
			case DrawMode.NoiseMap:
				mapDisplay.DrawTexture(TextureGenerator.TextureFromHeightMap(mapData.heightMap));

				break;
			case DrawMode.ColorMap:
				mapDisplay.DrawTexture(TextureGenerator.TextureFromColorMap(mapData.colorMap, mapChunkSize, mapChunkSize));

				break;
			case DrawMode.DrawMesh:
				mapDisplay.DrawMesh(
					MeshGenerator.GenerateTerrainMesh(mapData.heightMap, meshHeightMultiplier, meshHeightCurve, levelOfDetailPreview),
					TextureGenerator.TextureFromColorMap(mapData.colorMap, mapChunkSize, mapChunkSize));
				break;
			default:
				throw new ArgumentOutOfRangeException();
		}
	}

	public void RequestMapData(Action<MapData> callBack,Vector2 centre)
	{
		ThreadStart threadStart = delegate { MapDataThread(callBack,centre); };
		new Thread(threadStart).Start();
	}

	void MapDataThread(Action<MapData> callback,Vector2 centre)
	{
		MapData mapData = GenerateMapData(centre);
		lock (mapDataThreadInfoQueue)
		{
			mapDataThreadInfoQueue.Enqueue(new MapThreadInfo<MapData>(callback,mapData));
		}
	}

	public void RequestMeshData(MapData mapData,Action<MeshData> callback, int levelOfDetail)
	{
		ThreadStart threadStart = delegate { MeshDataThread(mapData,callback,levelOfDetail); };
		new Thread(threadStart).Start();
	}

	private void MeshDataThread(MapData mapData, Action<MeshData> callback, int levelOfDetail)
	{
		MeshData meshData =
			MeshGenerator.GenerateTerrainMesh(mapData.heightMap, meshHeightMultiplier, meshHeightCurve, levelOfDetail);
		lock (meshDataThreadInfoQueue)
		{
			meshDataThreadInfoQueue.Enqueue(new MapThreadInfo<MeshData>(callback,meshData));
		}
	}
	private void Update()
	{
		if (mapDataThreadInfoQueue.Count > 0)
		{
			
				for (int i = 0; i < mapDataThreadInfoQueue.Count; i++)
				{
					MapThreadInfo<MapData> threadInfo = mapDataThreadInfoQueue.Dequeue();
					threadInfo.callback(threadInfo.paramater);
				}
			
			
			
		}
		if (meshDataThreadInfoQueue.Count > 0)
		{
			for (int i = 0; i < meshDataThreadInfoQueue.Count; i++)
			{
				MapThreadInfo<MeshData> threadInfo = meshDataThreadInfoQueue.Dequeue();
				threadInfo.callback(threadInfo.paramater);
			}
			
		}
	}

	readonly struct MapThreadInfo<T>
	{
		public readonly Action<T> callback;
		public readonly T paramater;

		public MapThreadInfo(Action<T> callback, T paramater)
		{
			this.callback = callback;
			this.paramater = paramater;
		}
	}
}

[Serializable]
public struct TerrainType
{
	public string terrainTypeName;
	[Range(-1f, 1f)] public float height;
	public Color color;
}

public struct MapData
{
	public readonly float[,] heightMap;
	public readonly Color[] colorMap;

	public MapData(float[,] heightMap, Color[] colorMap)
	{
		this.heightMap = heightMap;
		this.colorMap = colorMap;
	}
}
