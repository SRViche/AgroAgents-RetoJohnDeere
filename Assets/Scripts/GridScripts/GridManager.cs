using UnityEngine;
using System.Collections.Generic;
public class GridManager : MonoBehaviour
{
    [SerializeField] private Transform gridOrigin;

    [Header("Configuración del grid")]
    [SerializeField] private int width=10;
    [SerializeField] private int height=10;
    [SerializeField] private float tileSize=1f;

    [Header("Generacion de terreno")]
    [SerializeField] private bool useRandomSeed=true;
    [SerializeField] private int customSeed=12345;
    [SerializeField] private float obstacleChance=0.15f;
    [SerializeField] private float cropChance=0.35f;



    [SerializeField] private GameObject floorPrefab;

    [SerializeField] private List<GameObject> obstaclePrefabs;
    [SerializeField] private List<GameObject> cropPrefabs;


    [Header("Materiales suelo")]
    [SerializeField] private Material normalMaterial;
    [SerializeField] private Material cosechadoMaterial;
    [SerializeField] private Material deterioradoMaterial;

    public int Width => width;
    public int Height => height;
    public float TileSize => tileSize;


    private TileData[,] grid;

    void Start()
    {
        GenerateGrid();
    }

    void GenerateGrid(){
        if(useRandomSeed){
            customSeed=Random.Range(0,999999);
        }
        Random.InitState(customSeed);

        grid=new TileData[width, height];

        Vector3 originPos=(gridOrigin!=null) ? gridOrigin.position : transform.position;


        for(int x=0; x<width; x++){
            for(int z=0; z<height; z++){
                Vector3 worldPos=originPos + new Vector3(x*tileSize, 0, z*tileSize);

                GameObject floorVisual=Instantiate(floorPrefab, worldPos, Quaternion.identity, transform);
                floorVisual.name=$"Tile_{x}_{z}";

                TileData tile=new TileData(x,z,floorVisual);

                if(x==0 && z==0){
                    tile.SetContent(TileContent.Vacio);
                }else{
                    float roll=Random.value;
                    if(roll<obstacleChance){
                        GameObject selectedObstacle=GetRandomPrefabFromList(obstaclePrefabs);
                        tile.SetContent(TileContent.Obstaculo, selectedObstacle);
                    }else if(roll < obstacleChance+cropChance){
                        GameObject selectedCrop=GetRandomPrefabFromList(cropPrefabs);
                        tile.SetContent(TileContent.Cultivo, selectedCrop);
                        tile.weight=1;
                    }else{
                        tile.SetContent(TileContent.Vacio);
                        tile.weight=2;
                    }
                }
                grid[x,z]=tile;
            }
        }
    }

    private GameObject GetRandomPrefabFromList(List<GameObject> prefabs){
        if(prefabs==null || prefabs.Count==0){
            return null;
        }
        int index=Random.Range(0, prefabs.Count);
        return prefabs[index];
    }

    public void ProcessHarvesterPass(Vector3 worldPosition){
        TileData tile=GetTileAtPosition(worldPosition);

        if(tile!=null){
            tile.PassHarvester();

            UpdateTileVisualState(tile);
        }
    }

    public void UpdateTileVisualState(TileData tile){
        if(tile==null || tile.floorVisual==null) return;

        MeshRenderer renderer=tile.floorVisual.GetComponentInChildren<MeshRenderer>();
        if(renderer==null) return;

        switch(tile.state){
            case TileState.Normal:
                if(normalMaterial!=null) renderer.material=normalMaterial;
                break;
            case TileState.Cosechado:
                if(cosechadoMaterial!=null) renderer.material=cosechadoMaterial;
                break;
            case TileState.Deteriorado:
                if(deterioradoMaterial!=null) renderer.material=deterioradoMaterial;
                break;
        }
    }
    

    
    public TileData GetTileAtPosition(Vector3 worldPosition){
        Vector3 originPos=(gridOrigin!=null) ? gridOrigin.position : transform.position;

        Vector3 localPos=worldPosition-originPos;

        int x=Mathf.RoundToInt(localPos.x/tileSize);
        int z=Mathf.RoundToInt(localPos.z/tileSize);

        if(x>=0 && x<width && z>=0 && z<height){
            return grid[x,z];
        }
        return null;
    }

    public TileData GetTile(int x, int z){
        if(x<0 || x>=width || z<0 || z>=height){
            return null;
        }
        return grid[x,z];
    }

    public Vector3 GridToWorld(Vector2Int gridPos){
        Vector3 originPos=(gridOrigin!=null) ? gridOrigin.position : transform.position;
        return originPos + new Vector3(gridPos.x*tileSize, 0, gridPos.y*tileSize);
    }

    public Vector2Int NearestWalkableTile(Vector3 worldTarget){
        Vector2Int best=Vector2Int.zero;
        float bestDist=float.MaxValue;

        for(int x=0; x<width; x++){
            for(int z=0; z<height; z++){
                TileData tile=grid[x,z];
                if(tile==null || !tile.IsWalkable){
                    continue;
                }
                float dist=Vector3.Distance(GridToWorld(new Vector2Int(x,z)),worldTarget);
                if(dist<bestDist){
                    bestDist=dist;
                    best= new Vector2Int(x,z);
                }
            }
        }
        return best;
    }

    
}
