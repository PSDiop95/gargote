using Gargote.Core;
using UnityEngine;

namespace Gargote.Data
{
    /// <summary>Recette de l'album. Textes : recipe.&lt;id&gt;.name / .anecdote / .step.N (relecture CULTURE_REVIEW).</summary>
    [CreateAssetMenu(menuName = "Gargote/Meta/Recipe", fileName = "Recipe")]
    public sealed class RecipeDefinitionAsset : ScriptableObject
    {
        public RecipeDefinition definition = new RecipeDefinition();
        [Tooltip("Nombre d'étapes de la recette dans la table de localisation")]
        public int steps = 3;
    }
}
