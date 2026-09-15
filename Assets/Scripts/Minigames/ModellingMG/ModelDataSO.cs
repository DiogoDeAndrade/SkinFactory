using UnityEngine;

// Snapshot of what the player carries after the modelling station: the concept, the drawing and the polygons.
// Saved by the modelling station in the editor (debug) and assignable to Player.debugModel to start from that stage.
[CreateAssetMenu(fileName = "ModelData", menuName = "Skin Factory/Model Data")]
public class ModelDataSO : ScriptableObject
{
    [System.Serializable]
    public class Polygon
    {
        public Vector2[] vertices;  // Normalized [0,1] over the drawing, origin bottom-left
    }

    [SerializeField] private ConceptSO  concept;
    [SerializeField] private Texture2D  drawing;
    [SerializeField] private Polygon[]  polygons;

    public ConceptSO    Concept => concept;
    public Texture2D    Drawing => drawing;

    public Vector2[][] GetPolygons()
    {
        if (polygons == null) return new Vector2[0][];

        var result = new Vector2[polygons.Length][];
        for (int i = 0; i < polygons.Length; i++)
        {
            result[i] = (polygons[i]?.vertices != null) ? (Vector2[])polygons[i].vertices.Clone() : new Vector2[0];
        }
        return result;
    }

    public void Set(ConceptSO concept, Texture2D drawing, Vector2[][] polygons)
    {
        this.concept = concept;
        this.drawing = drawing;
        this.polygons = new Polygon[polygons?.Length ?? 0];
        for (int i = 0; i < this.polygons.Length; i++)
        {
            this.polygons[i] = new Polygon { vertices = (Vector2[])polygons[i].Clone() };
        }
    }
}
