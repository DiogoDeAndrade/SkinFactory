using UnityEngine;
using UnityEngine.UIElements;

[CreateAssetMenu(fileName = "ConceptSO", menuName = "Skin Factory/ConceptSO")]
public class ConceptSO : ScriptableObject
{
    [SerializeField] private VectorImage    baseSVG;   
    [SerializeField] private Sprite         sketch;
    [SerializeField] private Sprite         conceptArt;

    public VectorImage  BaseSVG => baseSVG;
    public Sprite       Sketch => sketch;
    public Sprite       ConceptArt => conceptArt;
}
