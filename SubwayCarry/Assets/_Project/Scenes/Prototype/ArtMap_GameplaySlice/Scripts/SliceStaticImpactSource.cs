using UnityEngine;
using SubwayCarry.TeamReview.KimJun.Core.Contracts;
namespace SubwayCarry.Prototype.ArtMapSlice
{
    public sealed class SliceStaticImpactSource : MonoBehaviour, IPackageImpactSource
    {
        public Vector2 ImpactVelocity => Vector2.zero;
    }
}
