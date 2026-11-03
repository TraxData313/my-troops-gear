using System;
using HarmonyLib;
using MyTroopsGear.Core;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using TaleWorlds.Core;

namespace MyTroopsGear.UI
{
    /// <summary>
    /// Postfix on the private <c>PartyCharacterVM.GetCharacterCode(character, type, side)</c>: the image code of a
    /// party screen troop card and of each of its upgrade-target previews (the Character setter calls it for both).
    ///
    /// Side decision: the card's roster side picks the owner party (<c>LeftOwnerParty</c> / <c>RightOwnerParty</c>
    /// of the screen's PartyScreenLogic). Only when that party is mine (<see cref="GearScope"/>) does a member card
    /// get the edited look - so my main party (right), and on the left my own garrison or clan party, but never the
    /// other party in a trade, a loot/donation roster without an owner, or any prisoner (<see cref="PartyCardLook"/>).
    /// An upgrade preview uses the TARGET's override: that is what the troop wears after upgrading in my party.
    ///
    /// The look is a clone of the troop's first battle set (stable, no flicker) with <see cref="GearApplier.Apply"/>
    /// on top; the troop's own equipment is never written (Troop Equipment Upgrade reads it in PartyScreenLogic).
    /// No caching: every card build reads the current store, so the next party screen shows fresh edits.
    /// </summary>
    internal static class PartyScreenPatch
    {
        public static void Apply(Harmony harmony)
        {
            var target = AccessTools.Method(typeof(PartyCharacterVM), "GetCharacterCode",
                    new[] { typeof(CharacterObject), typeof(PartyScreenLogic.TroopType), typeof(PartyScreenLogic.PartyRosterSide) })
                ?? throw new MissingMethodException("PartyCharacterVM.GetCharacterCode(CharacterObject, TroopType, PartyRosterSide) not found");
            harmony.Patch(target, postfix: new HarmonyMethod(typeof(PartyScreenPatch), nameof(Postfix)));
        }

        /// <summary>The big 3D troop on the left of the party screen (PartyVM.SelectedCharacter, a HeroViewModel
        /// filled in the private RefreshCurrentCharacterInformation from the troop's own Equipment - it does NOT go
        /// through GetCharacterCode). Patched on its own so a missing method only loses this part.</summary>
        public static void ApplySelected(Harmony harmony)
        {
            var target = AccessTools.Method(typeof(PartyVM), "RefreshCurrentCharacterInformation", Type.EmptyTypes)
                ?? throw new MissingMethodException("PartyVM.RefreshCurrentCharacterInformation() not found");
            harmony.Patch(target, postfix: new HarmonyMethod(typeof(PartyScreenPatch), nameof(SelectedPostfix)));
        }

        /// <summary>The edited look of <paramref name="character"/> on this side of the screen, or null when it keeps
        /// vanilla: a clone of the first battle set with the override on top (the troop is never written).</summary>
        private static Equipment? EditedLook(CharacterObject? character, PartyScreenLogic.TroopType type,
            PartyScreenLogic.PartyRosterSide side, PartyScreenLogic? logic)
        {
            if (character == null || Campaign.Current == null) return null;
            var store = GearBehavior.Instance?.Store;
            if (store == null || store.Count == 0) return null;
            var over = store.Get(character.StringId);
            var hasOverride = over != null && GearEditing.HasEdits(over, s => GearApplier.VanillaItemId(character, s));
            if (!hasOverride) return null;

            var owner = logic == null ? null
                : side == PartyScreenLogic.PartyRosterSide.Left ? logic.LeftOwnerParty
                : side == PartyScreenLogic.PartyRosterSide.Right ? logic.RightOwnerParty
                : null;
            if (!PartyCardLook.ShowsMine(type == PartyScreenLogic.TroopType.Prisoner, character.IsHero,
                    hasOverride, GearScope.IsMine(owner)))
                return null;

            var baseSet = character.FirstBattleEquipment ?? character.Equipment;
            if (baseSet == null) return null;
            var equipment = baseSet.Clone();
            GearApplier.Apply(over!, equipment);
            return equipment;
        }

        // Parameter names match the original's; ____partyScreenLogic is the protected field _partyScreenLogic.
        private static void Postfix(CharacterObject character, PartyScreenLogic.TroopType type,
            PartyScreenLogic.PartyRosterSide side, PartyScreenLogic ____partyScreenLogic, ref CharacterCode __result)
        {
            try
            {
                if (__result == null) return;
                var equipment = EditedLook(character, type, side, ____partyScreenLogic);
                if (equipment == null) return;

                // The face/hair roll depends on the helmet's hair cover; keep vanilla's body unless that changed.
                var body = __result.BodyProperties;
                if (equipment.HairCoverType != (character.Equipment?.HairCoverType ?? equipment.HairCoverType))
                    body = character.GetBodyProperties(equipment);

                __result = CharacterCode.CreateFrom(equipment.CalculateEquipmentCode(), body, __result.IsFemale,
                    __result.IsHero, __result.Color1, __result.Color2, __result.FormationClass, __result.Race);
            }
            catch (Exception e)
            {
                ModLog.ErrorOnce("partyscreen", e);
            }
        }

        // After vanilla built SelectedCharacter from the troop's own Equipment: same side rule as the cards.
        private static void SelectedPostfix(PartyVM __instance)
        {
            try
            {
                var current = __instance?.CurrentCharacter;
                var shown = __instance?.SelectedCharacter;
                if (current == null || shown == null) return;
                var character = current.Character;
                var equipment = EditedLook(character, current.Type, current.Side, __instance!.PartyScreenLogic);
                if (equipment == null) return;
                shown.SetEquipment(equipment); // it clones; the non-hero path leaves the mount key as vanilla's
                shown.MountCreationKey = MountCreationKey.GetRandomMountKeyString(equipment[EquipmentIndex.Horse].Item,
                    TaleWorlds.Library.Common.GetDJB2(character.StringId));
            }
            catch (Exception e)
            {
                ModLog.ErrorOnce("partyscreen selected", e);
            }
        }
    }
}
