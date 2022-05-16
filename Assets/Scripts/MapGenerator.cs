using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{
	public enum DrawMode
	{
		NoiseMap,
		ColorMap
	}


	public DrawMode drawMode;
	[Min(1)] [SerializeField] private int mapWidth;
	[Min(1)] [SerializeField] private int mapHeight;
	[SerializeField] private float noiseScale;
	[Min(0)] [SerializeField] private int octaves;
	[Range(0, 1)] [SerializeField] private float persistance;
	[Min(1)] [SerializeField] private float lacunarity;
	[SerializeField] private int seed;
	[SerializeField] private Vector2 offset;
	[SerializeField] private TerrainType[] regions;
	[field: SerializeField] public bool autoUpdate { get; private set; }

	public void GenerateMap()
	{
		float[,] noiseMap = Noise.GenerateNoise(mapWidth, mapHeight, seed, noiseScale, octaves, persistance, lacunarity,
			offset);
		Color[] colorMap = new Color[mapHeight * mapWidth];
		for (int y = 0; y < mapHeight; y++)
		{
			for (int x = 0; x < mapWidth; x++)
			{
				float currentHeight = noiseMap[x, y];
				for (int i = 0; i < regions.Length; i++)
				{
					if (currentHeight <= regions[i].height)
					{
						colorMap[y * mapWidth + x] = regions[i].color;
						break;
					}
				}
			}
		}

		MapDisplay mapDisplay = FindObjectOfType<MapDisplay>();
		switch (drawMode)
		{
			case DrawMode.NoiseMap:
				mapDisplay.DrawTexture(TextureGenerator.TextureFromHeightMap(noiseMap));

				break;
			case DrawMode.ColorMap:
				mapDisplay.DrawTexture(TextureGenerator.TextureFromColorMap(colorMap, mapWidth, mapHeight));

				break;
		}
	}
}

[System.Serializable]
public struct TerrainType
{
	public string terrainTypeName;
	[Range(-1f, 1f)] public float height;
	public Color color;
}