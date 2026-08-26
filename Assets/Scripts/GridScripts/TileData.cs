using UnityEngine;

public enum TileState
{
    Normal,
    Cosechado,
    Deteriorado
}

public enum TileContent
{
    Vacio,
    Cultivo,
    Obstaculo
}


[System.Serializable]
public class TileData
{
    public int x;
    public int z;
    public TileState state;
    public TileContent contentType;
    public int weight;

    public GameObject floorVisual;
    public GameObject currentObject;

    public bool IsWalkable => contentType != TileContent.Obstaculo;
    public bool HasCrop => contentType == TileContent.Cultivo;
    public bool IsObstacle => contentType == TileContent.Obstaculo;
    public bool IsEmpty => contentType == TileContent.Vacio;

    public TileData(int x, int z, GameObject floorVisual){
        this.x=x;
        this.z=z;
        this.state=TileState.Normal;
        this.contentType=TileContent.Vacio;
        this.floorVisual=floorVisual;
        this.currentObject=null;
        this.weight=1;
    }

    public void PassHarvester(){
        if(IsObstacle) return;

        if(state==TileState.Normal){
            state=TileState.Cosechado;
        }else if(state==TileState.Cosechado){
            state=TileState.Deteriorado;
        }    

        if(HasCrop){
            ClearContent();
        }
    }

    public bool SetContent(TileContent newType, GameObject prefabToSpawn=null){
        if (!IsEmpty && newType != TileContent.Vacio) return false;

        ClearContent();
        contentType = newType;

        if (prefabToSpawn != null && floorVisual != null)
        {
            Vector3 spawnPosition = floorVisual.transform.position;

            Renderer tileRenderer = floorVisual.GetComponentInChildren<Renderer>();

            if (tileRenderer != null)
            {
                float topY = tileRenderer.bounds.max.y;
                spawnPosition = new Vector3(floorVisual.transform.position.x, topY, floorVisual.transform.position.z);
            }

            
            currentObject = Object.Instantiate(prefabToSpawn, spawnPosition, Quaternion.identity);
            currentObject.transform.SetParent(floorVisual.transform);
        }

        return true;
    }

    public void ClearContent(){
        if(currentObject!=null){
            Object.Destroy(currentObject);
            currentObject=null;
        }
        contentType=TileContent.Vacio;
    }
}
