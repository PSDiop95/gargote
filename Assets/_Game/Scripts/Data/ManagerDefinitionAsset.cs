using Gargote.Core;
using UnityEngine;

namespace Gargote.Data
{
    /// <summary>Gérant (§5). Textes (nom, réplique) dans la table « Gargote » : manager.&lt;id&gt;.name / .line.</summary>
    [CreateAssetMenu(menuName = "Gargote/Meta/Manager", fileName = "Manager")]
    public sealed class ManagerDefinitionAsset : ScriptableObject
    {
        public ManagerDefinition definition = new ManagerDefinition();
    }
}
