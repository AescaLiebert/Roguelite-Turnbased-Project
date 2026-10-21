using FightingAllstar.Rendering;
using UnityEngine;
namespace FightingAllstar.Presentation.Combat
{
    // Keeps existing scene references; new maps use the shared MapRenderContext.
    [DefaultExecutionOrder(-11000), DisallowMultipleComponent]
    public sealed class TeyvatDungeonArena : MapRenderContext
    {
        public new static TeyvatDungeonArena Active => MapRenderContext.Active as TeyvatDungeonArena;
    }
}
