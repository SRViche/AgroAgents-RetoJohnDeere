using UnityEngine;
using System.Text;
using System.IO;
using UnityEngine.InputSystem;

namespace AgroAgents.Presentation
{
    
    public class VRGridBrush : MonoBehaviour
    {
        public enum BrushMode{Terrain, Tree, Rock, Crop, Eraser}

        [Header("Referencias al Grid")]
        [SerializeField] private Transform gridOrigin;
        [SerializeField] private float tileSize=8f;

        private const int MAX_GRID_SIZE=30;
        [Range(1, MAX_GRID_SIZE)]
        [SerializeField] private int gridWidth=30;
        [Range(1,MAX_GRID_SIZE)]
        [SerializeField] private int gridHeight=30;

        public LayerMask gridLayer;

        [Header("Configuracion Brush")]
        [SerializeField] private Transform vrController;
        public BrushMode currentMode=BrushMode.Terrain;

        [Header("Prefabs")]
        [SerializeField] private GameObject terrainPrefab;
        [SerializeField] private GameObject treePrefab;
        [SerializeField] private GameObject rockPrefab;
        [SerializeField] private GameObject cropPrefab;

        [Header("Acciones")]
        [SerializeField] private InputActionReference gripAction;


        private class PaintedCell
        {
            public BrushMode Mode;
            public GameObject FloorInstance;
            public GameObject ContentInstance;
        }

        private PaintedCell[,] _gridCells;

        private void Start()
        {
            InitializeGridData();
        }

        void Update()
        {
            if(gripAction.action.IsPressed()){
                TryPaintCell();
            }
        }

        public void SetGridDimensions(int newWidth, int newHeight)
        {
            gridWidth=Mathf.Clamp(newWidth, 1, MAX_GRID_SIZE);
            gridHeight=Mathf.Clamp(newHeight, 1, MAX_GRID_SIZE);

            ClearEntireGrid();
            InitializeGridData();
        }

        private void InitializeGridData()
        {
            _gridCells= new PaintedCell[gridWidth, gridHeight];
        }

        public void ClearEntireGrid()
        {
            if (_gridCells != null)
            {
                for(int y=0; y<_gridCells.GetLength(1); y++)
                {
                    for(int x=0; x<_gridCells.GetLength(0); x++)
                    {
                        if(_gridCells[x, y] != null)
                        {
                            if (_gridCells[x, y].FloorInstance != null)
                            {
                                Destroy(_gridCells[x, y].FloorInstance);
                            }
                            if (_gridCells[x, y].ContentInstance != null)
                            {
                                Destroy(_gridCells[x,y].ContentInstance);
                            }
                        }
                    }
                }
            }
        }

        private void TryPaintCell()
        {
            if (vrController == null)
                return;

            if(Physics.Raycast(vrController.position, vrController.forward, out RaycastHit hit, 100f, gridLayer))
            {
                Vector3 originPos=gridOrigin !=null ? gridOrigin.position : transform.position;
                Vector3 localPos=hit.point-originPos;

                int x=Mathf.FloorToInt(localPos.x/tileSize);
                int y=Mathf.FloorToInt(localPos.z/tileSize);

                if(x>=0 && x<gridWidth && y>=0 && y < gridHeight)
                {
                    ApplyBrushLogic(x, y, originPos);
                }
            }
        }

        private void ApplyBrushLogic(int x, int y, Vector3 originPos)
        {
            if(currentMode==BrushMode.Eraser)
            {
                if(_gridCells[x, y] != null)
                {
                    if(_gridCells[x, y].FloorInstance!=null) Destroy(_gridCells[x,y].FloorInstance);
                    if(_gridCells[x, y].ContentInstance!=null) Destroy(_gridCells[x,y].ContentInstance);

                    _gridCells[x, y]=null;
                }
                return;
            }

            if (_gridCells[x, y] == null)
            {
                _gridCells[x, y]= new PaintedCell {Mode = BrushMode.Terrain};
            }

            PaintedCell cell=_gridCells[x,y];
            Vector3 cellCenter=originPos+new Vector3(x*tileSize+(tileSize/2f), 0, y*tileSize+(tileSize/2f));

            switch (currentMode)
            {
                case BrushMode.Terrain:
                    ClearCellContent(cell);
                    if (cell.FloorInstance == null)
                    {
                        cell.FloorInstance=Instantiate(terrainPrefab, cellCenter,Quaternion.identity);
                    }
                    cell.Mode=BrushMode.Terrain;
                    break;
                case BrushMode.Tree:
                case BrushMode.Rock:
                    ClearCellContent(cell);
                    GameObject prefabToUse=currentMode==BrushMode.Tree ? treePrefab : rockPrefab;
                    cell.ContentInstance=Instantiate(prefabToUse,cellCenter,Quaternion.identity);
                    cell.Mode=currentMode;
                    break;
                case BrushMode.Crop:
                    if(cell.Mode==BrushMode.Terrain && cell.ContentInstance == null)
                    {
                        cell.ContentInstance=Instantiate(cropPrefab, cellCenter, Quaternion.identity);
                        cell.Mode=BrushMode.Crop;
                    }
                    break;
            }
        }

        private void ClearCellContent(PaintedCell cell)
        {
            if (cell.ContentInstance != null)
            {
                Destroy(cell.ContentInstance);
                cell.ContentInstance=null;
            }
        }

        public void ExportAndSaveGrid()
        {
            StringBuilder sb=new StringBuilder();
            for(int y=0; y<gridHeight; y++)
            {
                for(int x=0; x<gridWidth; x++)
                {
                    PaintedCell cell=_gridCells[x, y];
                    if(cell==null || cell.Mode==BrushMode.Terrain) sb.Append('.');
                    else if(cell.Mode==BrushMode.Crop) sb.Append('W');
                    else if(cell.Mode==BrushMode.Tree || cell.Mode==BrushMode.Rock) sb.Append('#');
                }
                sb.AppendLine();
            }
            string path=Application.dataPath + "/AuthoredVRGrid.txt";
            File.WriteAllText(path, sb.ToString());
            Debug.Log("Se exporto exitosamente");

        }

        public void SetBrushModeTerrain()
        {
            currentMode = BrushMode.Terrain;
            Debug.Log("Modo Pincel: Terrain");
        }

        public void SetBrushModeTree()
        {
            currentMode = BrushMode.Tree;
            Debug.Log("Modo Pincel: Tree");
        }

        public void SetBrushModeRock()
        {
            currentMode = BrushMode.Rock;
            Debug.Log("Modo Pincel: Rock");
        }

        public void SetBrushModeCrop()
        {
            currentMode = BrushMode.Crop;
            Debug.Log("Modo Pincel: Crop");
        }

        public void SetBrushModeEraser()
        {
            currentMode = BrushMode.Eraser;
            Debug.Log("Modo Pincel: Eraser");
        }
    }
}
