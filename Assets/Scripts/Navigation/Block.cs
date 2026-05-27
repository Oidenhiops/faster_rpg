using System;
using UnityEngine;

[Serializable]
public class Block
{
    // Posición discreta en la grilla (clave en el Dictionary del GridMap).
    public Vector3Int gridPos;

    // Caras por las que se PUEDE salir. Bloque |_| con techo abierto -> openFaces = Up.
    // Una escalera con pasamanos al este -> openFaces = All & ~East.
    public BlockFace openFaces = BlockFace.All;

    // ¿Hay un objeto sólido sobre la superficie caminable de este bloque? (cofre, NPC, item dropeado)
    // Si es true, el bloque NO es transitable aunque isWalkable sea true.
    public bool isOccupiedOnTop;

    // ¿Este bloque es base caminable? false para muros, decoración no transitable, agua profunda, etc.
    public bool isWalkable = true;

    // Costo para A*. 1f = caminar normal. >1 = terreno lento (barro, agua), <1 = atajo (escalera prioritaria).
    public float moveCost = 1f;

    // Si este bloque es una escalera/rampa diagonal, indica HACIA DÓNDE sube (norte, sur, este u oeste).
    // None = bloque plano normal. Un solo valor cardinal horizontal (no Up/Down, no combinación).
    // Una escalera con stairUpDirection = East conecta diagonalmente:
    //   - hacia arriba: (pos + East + Up)
    //   - hacia abajo:  (pos + West + Down)
    public BlockFace stairUpDirection = BlockFace.None;

    // Referencia al GameObject de la escena. Útil para debug, interacción y para que objetos dinámicos puedan
    // marcar el bloque como ocupado en runtime.
    [NonSerialized] public GameObject sourceObject;

    // Helper: ¿puedo pararme/transitar este bloque ahora mismo?
    public bool IsTraversable => isWalkable && !isOccupiedOnTop;

    public Block() {}

    public Block(Vector3Int gridPos, BlockFace openFaces, bool isWalkable = true, float moveCost = 1f)
    {
        this.gridPos = gridPos;
        this.openFaces = openFaces;
        this.isWalkable = isWalkable;
        this.moveCost = moveCost;
    }
}
