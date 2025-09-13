// using System;
// using UnityEngine;
// using YGO.Duel.Chain;
// using YGO.Duel.Data;
// using YGO.Duel.Effects;
// using YGO.Duel.Foundation;
// using YGO.Duel.Rules;
//
// public sealed class EffectLibraryBootstrap : MonoBehaviour
// {
//     [Header("Register card-specific effects here")]
//     public CardDefinition[] cardDefs;
//
//     private void Awake()
//     {
//         // Get or make the library
//         if (!ServiceLocator.TryGet(out EffectLibrary lib))
//         {
//             lib = new EffectLibrary();
//             ServiceLocator.Register(lib, overwrite:true);
//         }
//
//         // Example: map a simple “destroy 1 S/T” effect for testing
//         foreach (var def in cardDefs)
//         {
//             if (!def) continue;
//
//             // EXAMPLE handle (replace with your real ones)
//             var handle = new ScriptedEffectHandle(
//                 name: def.cardName,
//                 speed: def.IsTrap
//                     ? (def.trapSubtype == TrapSubtype.Counter ? RuleSet.SpellSpeed.Three : RuleSet.SpellSpeed.Two)
//                     : (def.spellSubtype == SpellSubtype.QuickPlay ? RuleSet.SpellSpeed.Two : RuleSet.SpellSpeed.One),
//                 condition: ctx => (true, ""),
//                 costs: ctx => Array.Empty<ICost>(),
//                 resolver: resCtx => new DestroyFirstTargetResolver()
//             );
//
//             lib.Register(def, handle); // effectId "" default
//         }
//     }
//
//     // demo resolver: destroy target if still legal
//     private sealed class DestroyFirstTargetResolver : IResolverAction
//     {
//         public void Resolve(ResolveContext ctx)
//         {
//             if (ctx.Targets == null || ctx.Targets.Count == 0) return;
//             var tr = ctx.Targets[0];
//
//             if (!tr.TryResolveCard(ServiceLocator.Get<YGO.Duel.Board.BoardManager>(), out var card) || card == null) return;
//
//             // Put your destruction system call here
//             if (ServiceLocator.TryGet<YGO.Duel.Systems.DestructionSystem>(out var destr) && destr != null)
//             {
//                 destr.TryDestroy(card, DestroyReason.Effect,card.Controller, out string reason);
//             }
//         }
//     }
// }