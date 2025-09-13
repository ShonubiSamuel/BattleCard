// // EffectLibrary.cs
// using System;
// using System.Collections.Generic;
// using YGO.Duel.Board;
// using YGO.Duel.Chain;
// using YGO.Duel.Data;
// using YGO.Duel.Rules;
// using Card = YGO.Duel.Cards.Card;
//
// namespace YGO.Duel.Effects
// {
//     /// <summary>Factory type that produces an IEffectHandle bound to a specific source card.</summary>
//     public delegate IEffectHandle EffectFactory(Card source);
//
//     /// <summary>Central map: (cardId, effectKey) -> factory.</summary>
//     public sealed class EffectLibrary
//     {
//         // Key: (Card Definition asset id, optional effectId) -> effect handle
//         private readonly Dictionary<(string cardDefId, string effectId), IEffectHandle> _map
//             = new(StringTupleComparer.Instance);
//
//         public void Register(CardDefinition def, IEffectHandle handle, string effectId = "")
//         {
//             if (def == null || handle == null) return;
//             _map[(def.DefinitionId, effectId ?? "")] = handle;
//         }
//
//         public IEffectHandle GetHandle(CardDefinition def, string effectId = "")
//         {
//             if (def == null) return null;
//             _map.TryGetValue((def.DefinitionId, effectId ?? ""), out var h);
//             return h;
//         }
//
//         // simple tuple comparer (avoids allocs)
//         private sealed class StringTupleComparer : IEqualityComparer<(string, string)>
//         {
//             public static readonly StringTupleComparer Instance = new();
//             public bool Equals((string, string) x, (string, string) y)
//                 => string.Equals(x.Item1, y.Item1, StringComparison.Ordinal) &&
//                    string.Equals(x.Item2, y.Item2, StringComparison.Ordinal);
//             public int GetHashCode((string, string) obj)
//                 => HashCode.Combine(obj.Item1 ?? "", obj.Item2 ?? "");
//         }
//     }
//
//     // ---------------- Helpers: Scripted effect handle ----------------
//
//     /// <summary>Composable IEffectHandle you can build from lambdas.</summary>
//     public sealed class ScriptedEffectHandle : IEffectHandle
//     {
//         public string EffectName { get; }
//         public RuleSet.SpellSpeed Speed { get; }
//         private readonly Func<ConditionContext, (bool ok, string why)> _cond;
//         private readonly Func<CostContext, IEnumerable<ICost>> _costs;
//         private readonly Func<ResolveContext, IResolverAction> _resolver;
//
//         public ScriptedEffectHandle(
//             string name,
//             RuleSet.SpellSpeed speed,
//             Func<ConditionContext, (bool, string)> condition,
//             Func<CostContext, IEnumerable<ICost>> costs,
//             Func<ResolveContext, IResolverAction> resolver)
//         {
//             EffectName = name ?? "Effect";
//             Speed = speed;
//             _cond = condition ?? (_ => (true, ""));
//             _costs = costs ?? (_ => Array.Empty<ICost>());
//             _resolver = resolver ?? (_ => null);
//         }
//
//         public bool CheckAdditionalConditions(ConditionContext ctx, out string reason)
//         {
//             var (ok, why) = _cond(ctx);
//             reason = why ?? "";
//             return ok;
//         }
//
//         public IEnumerable<ICost> GetCosts(CostContext ctx) => _costs(ctx);
//
//         public IResolverAction BuildResolveAction(ResolveContext ctx) => _resolver(ctx);
//     }
//
//   
// }