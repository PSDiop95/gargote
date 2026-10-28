using Gargote.Simulation;
using UnityEngine;

namespace Gargote.EditorTools
{
    /// <summary>Profils de joueurs et cibles de la simulation (§4.5). Asset éditeur uniquement.</summary>
    [CreateAssetMenu(menuName = "Gargote/Simulation Config", fileName = "SimulationConfig")]
    public sealed class SimulationConfigAsset : ScriptableObject
    {
        public SimulationSeed settings = new SimulationSeed();
    }
}
