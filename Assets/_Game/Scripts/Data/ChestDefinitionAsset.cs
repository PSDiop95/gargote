using Gargote.Core;
using UnityEngine;

namespace Gargote.Data
{
    /// <summary>Coffre (Bois, Bronze, Or, Teranga) : probabilités affichées au joueur (bouton « i »).</summary>
    [CreateAssetMenu(menuName = "Gargote/Meta/Chest", fileName = "Chest")]
    public sealed class ChestDefinitionAsset : ScriptableObject
    {
        public ChestDefinition definition = new ChestDefinition();
    }
}
