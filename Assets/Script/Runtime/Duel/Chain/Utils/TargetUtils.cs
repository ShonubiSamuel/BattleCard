using YGO.Duel.Chain.YGO.Duel.Chain;

namespace YGO.Duel.Chain
{
    public static class TargetUtils
    {
        public static string SafeName(ITargetRef t) => t?.DebugName ?? "(none)";
    }
}