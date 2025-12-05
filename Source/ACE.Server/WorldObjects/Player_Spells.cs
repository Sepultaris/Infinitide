using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Database;
using ACE.Database.Models.Shard;
using ACE.DatLoader;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Network.Structure;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Entity.Enum.Properties;
using static System.Formats.Asn1.AsnWriter;

namespace ACE.Server.WorldObjects
{
    partial class Player
    {
        public bool SpellIsKnown(uint spellId)
        {
            return Biota.SpellIsKnown((int)spellId, BiotaDatabaseLock);
        }

        /// <summary>
        /// Will return true if the spell was added, or false if the spell already exists.
        /// </summary>
        public bool AddKnownSpell(uint spellId)
        {
            Biota.GetOrAddKnownSpell((int)spellId, BiotaDatabaseLock, out var spellAdded);

            if (spellAdded)
                ChangesDetected = true;

            return spellAdded;
        }

        /// <summary>
        /// Removes a known spell from the player's spellbook
        /// </summary>
        public bool RemoveKnownSpell(uint spellId)
        {
            return Biota.TryRemoveKnownSpell((int)spellId, BiotaDatabaseLock);
        }

        public void LearnSpellWithNetworking(uint spellId, bool uiOutput = true)
        {
            var spells = DatManager.PortalDat.SpellTable;

            if (!spells.Spells.ContainsKey(spellId))
            {
                GameMessageSystemChat errorMessage = new GameMessageSystemChat("SpellID not found in Spell Table", ChatMessageType.Broadcast);
                Session.Network.EnqueueSend(errorMessage);
                return;
            }

            if (!AddKnownSpell(spellId))
            {
                if (uiOutput)
                {
                    GameMessageSystemChat errorMessage = new GameMessageSystemChat("You already know that spell!", ChatMessageType.Broadcast);
                    Session.Network.EnqueueSend(errorMessage);
                }
                return;
            }

            GameEventMagicUpdateSpell updateSpellEvent = new GameEventMagicUpdateSpell(Session, (ushort)spellId);
            Session.Network.EnqueueSend(updateSpellEvent);

            // Check to see if we echo output to the client text area and do playscript animation
            if (uiOutput)
            {
                // Always seems to be this SkillUpPurple effect
                ApplyVisualEffects(PlayScript.SkillUpPurple);

                string message = $"You learn the {spells.Spells[spellId].Name} spell.\n";
                GameMessageSystemChat learnMessage = new GameMessageSystemChat(message, ChatMessageType.Broadcast);
                Session.Network.EnqueueSend(learnMessage);
            }
            else
            {
                Session.Network.EnqueueSend(new GameEventCommunicationTransientString(Session, "You have learned a new spell."));
            }
        }

        /// <summary>
        ///  Learns spells in bulk, without notification, filtered by school and level
        /// </summary>
        public void LearnSpellsInBulk(MagicSchool school, uint spellLevel, bool withNetworking = true)
        {
            var spellTable = DatManager.PortalDat.SpellTable;

            foreach (var spellID in PlayerSpellTable)
            {
                if (!spellTable.Spells.ContainsKey(spellID))
                {
                    Console.WriteLine($"Unknown spell ID in PlayerSpellID table: {spellID}");
                    continue;
                }
                var spell = new Spell(spellID, false);
                if (spell.School == school && spell.Formula.Level == spellLevel)
                {
                    if (withNetworking)
                        LearnSpellWithNetworking(spell.Id, false);
                    else
                        AddKnownSpell(spell.Id);
                }
            }
        }

        public void HandleActionMagicRemoveSpellId(uint spellId)
        {
            if (!Biota.TryRemoveKnownSpell((int)spellId, BiotaDatabaseLock))
            {
                log.Error("Invalid spellId passed to Player.RemoveSpellFromSpellBook");
                return;
            }

            ChangesDetected = true;

            GameEventMagicRemoveSpell removeSpellEvent = new GameEventMagicRemoveSpell(Session, (ushort)spellId);
            Session.Network.EnqueueSend(removeSpellEvent);
        }

        public void EquipItemFromSet(WorldObject item)
        {
            if (!item.HasItemSet) return;

            var setItems = EquippedObjects.Values.Where(i => i.HasItemSet && i.EquipmentSetId == item.EquipmentSetId).ToList();

            var spells = GetSpellSet(setItems);

            // get the spells from before / without this item
            setItems.Remove(item);
            var prevSpells = GetSpellSet(setItems);

            EquipDequipItemFromSet(item, spells, prevSpells);
        }

        public void EquipDequipItemFromSet(WorldObject item, List<Spell> spells, List<Spell> prevSpells, WorldObject surrogateItem = null)
        {
            // compare these 2 spell sets -
            // see which spells are being added, and which are being removed
            var addSpells = spells.Except(prevSpells);
            var removeSpells = prevSpells.Except(spells);

            // set spells are not affected by mana
            // if it's equipped, it's active.

            foreach (var spell in removeSpells)
                EnchantmentManager.Dispel(EnchantmentManager.GetEnchantment(spell.Id, item.EquipmentSetId.Value));

            var addItem = surrogateItem ?? item;

            foreach (var spell in addSpells)
                CreateItemSpell(addItem, spell.Id);
        }

        public void DequipItemFromSet(WorldObject item)
        {
            if (!item.HasItemSet) return;

            var setItems = EquippedObjects.Values.Where(i => i.HasItemSet && i.EquipmentSetId == item.EquipmentSetId).ToList();

            // for better bookkeeping, and to avoid a rarish error with AuditItemSpells detecting -1 duration item enchantments where
            // the CasterGuid is no longer in the player's possession
            var surrogateItem = setItems.LastOrDefault();

            var spells = GetSpellSet(setItems);

            // get the spells from before / with this item
            setItems.Add(item);
            var prevSpells = GetSpellSet(setItems);

            if (surrogateItem == null)
            {
                var addSpells = spells.Except(prevSpells);

                if (addSpells.Count() != 0)
                    log.Error($"{Name}.DequipItemFromSet({item.Name}) -- last item in set dequipped, but addSpells still contains {string.Join(", ", addSpells.Select(i => i.Name))} -- this shouldn't happen!");
            }

            EquipDequipItemFromSet(item, spells, prevSpells, surrogateItem);
        }

        public void OnItemLevelUp(WorldObject item, int prevItemLevel)
        {
            if (!item.HasItemSet)
            {
                ApplyDamageAndArmorBonuses(item, item.ItemLevel.Value - prevItemLevel);

                if (item.ItemType == ItemType.MissileWeapon || item.ItemType == ItemType.MeleeWeapon || item.ItemType == ItemType.Caster)
                {
                    var monsterKillHistory = item.GetMonsterKillHistory();

                    if (monsterKillHistory.Count > 0)
                        ApplyRend(item);

                    //if (item.ItemLevel == 1)
                    //  ApplyRend(item);

                    if (item.ItemLevel == 100)
                        ApplySlayer(item);
                }
                
                return;
            }

            var setItems = EquippedObjects.Values.Where(i => i.HasItemSet && i.EquipmentSetId == item.EquipmentSetId).ToList();

            var levelDiff = prevItemLevel - (item.ItemLevel ?? 0);

            var prevSpells = GetSpellSet(setItems, levelDiff);

            var spells = GetSpellSet(setItems);

            EquipDequipItemFromSet(item, spells, prevSpells);

            ApplyDamageAndArmorBonuses(item, (item.ItemLevel.Value - prevItemLevel));
        }

        public void CreateSentinelBuffPlayers(IEnumerable<Player> players, bool self = false, ulong maxLevel = 8)
        {
            var SelfOrOther = self ? "Self" : "Other";

            // ensure level 8s are installed
            var maxSpellLevel = Math.Clamp(maxLevel, 1, 8);
            if (maxSpellLevel == 8 && DatabaseManager.World.GetCachedSpell((uint)SpellId.ArmorOther8) == null)
                maxSpellLevel = 7;

            var tySpell = typeof(SpellId);
            List<BuffMessage> buffMessages = new List<BuffMessage>();
            // prepare messages
            List<string> buffsNotImplementedYet = new List<string>();
            foreach (var spell in Buffs)
            {
                var spellNamPrefix = spell;
                bool isBane = false;
                if (spellNamPrefix.StartsWith("@"))
                {
                    isBane = true;
                    spellNamPrefix = spellNamPrefix.Substring(1);
                }
                string fullSpellEnumName = spellNamPrefix + ((isBane) ? string.Empty : SelfOrOther) + maxSpellLevel;
                string fullSpellEnumNameAlt = spellNamPrefix + ((isBane) ? string.Empty : ((SelfOrOther == "Self") ? "Other" : "Self")) + maxSpellLevel;
                uint spellID = (uint)Enum.Parse(tySpell, fullSpellEnumName);
                var buffMsg = BuildBuffMessage(spellID);

                if (buffMsg == null)
                {
                    spellID = (uint)Enum.Parse(tySpell, fullSpellEnumNameAlt);
                    buffMsg = BuildBuffMessage(spellID);
                }

                if (buffMsg != null)
                {
                    buffMsg.Bane = isBane;
                    buffMessages.Add(buffMsg);
                }
                else
                {
                    buffsNotImplementedYet.Add(fullSpellEnumName);
                }
            }
            // buff each player
            players.ToList().ForEach(targetPlayer =>
            {
                if (buffMessages.Any(k => !k.Bane))
                {
                    // bake player into the messages
                    buffMessages.Where(k => !k.Bane).ToList().ForEach(k => k.SetTargetPlayer(targetPlayer));
                    // update client-side enchantments
                    targetPlayer.Session.Network.EnqueueSend(buffMessages.Where(k => !k.Bane).Select(k => k.SessionMessage).ToArray());
                    // run client-side effect scripts, omitting duplicates
                    targetPlayer.EnqueueBroadcast(buffMessages.Where(k => !k.Bane).ToList().GroupBy(m => m.Spell.TargetEffect).Select(a => a.First().LandblockMessage).ToArray());
                    // update server-side enchantments

                    var buffsForPlayer = buffMessages.Where(k => !k.Bane).ToList().Select(k => k.Enchantment);

                    var lifeBuffsForPlayer = buffsForPlayer.Where(k => k.Spell.School == MagicSchool.LifeMagic).ToList();
                    var critterBuffsForPlayer = buffsForPlayer.Where(k => k.Spell.School == MagicSchool.CreatureEnchantment).ToList();
                    var itemBuffsForPlayer = buffsForPlayer.Where(k => k.Spell.School == MagicSchool.ItemEnchantment).ToList();

                    lifeBuffsForPlayer.ForEach(spl =>
                    {
                        CreateEnchantmentSilent(spl.Spell, targetPlayer);
                    });
                    critterBuffsForPlayer.ForEach(spl =>
                    {
                        CreateEnchantmentSilent(spl.Spell, targetPlayer);
                    });
                    itemBuffsForPlayer.ForEach(spl =>
                    {
                        CreateEnchantmentSilent(spl.Spell, targetPlayer);
                    });
                }
                if (buffMessages.Any(k => k.Bane))
                {
                    // Impen/bane
                    var items = targetPlayer.EquippedObjects.Values.ToList();
                    var itembuffs = buffMessages.Where(k => k.Bane).ToList();
                    foreach (var itemBuff in itembuffs)
                    {
                        foreach (var item in items)
                        {
                            if ((item.WeenieType == WeenieType.Clothing || item.IsShield) && item.IsEnchantable)
                                CreateEnchantmentSilent(itemBuff.Spell, item);
                        }
                    }
                }
            });
        }

        private void CreateEnchantmentSilent(Spell spell, WorldObject target)
        {
            var addResult = target.EnchantmentManager.Add(spell, this, null);

            if (target is Player targetPlayer)
            {
                targetPlayer.Session.Network.EnqueueSend(new GameEventMagicUpdateEnchantment(targetPlayer.Session, new Enchantment(targetPlayer, addResult.Enchantment)));

                targetPlayer.HandleSpellHooks(spell);
            }
        }

        // TODO: switch this over to SpellProgressionTables
        private static string[] Buffs = new string[] {
#region spells
            // @ indicates impenetrability or a bane
            "Strength",
            "Invulnerability",
            "FireProtection",
            "Armor",
            "Rejuvenation",
            "Regeneration",
            "ManaRenewal",
            "Impregnability",
            "MagicResistance",
            //"AxeMastery",    // light weapons
            "LightWeaponsMastery",
            //"DaggerMastery", // finesse weapons
            "FinesseWeaponsMastery",
            //"MaceMastery",
            //"SpearMastery",
            //"StaffMastery",
            //"SwordMastery",  // heavy weapons
            "HeavyWeaponsMastery",
            //"UnarmedCombatMastery",
            //"BowMastery",    // missile weapons
            "MissileWeaponsMastery",
            //"CrossbowMastery",
            //"ThrownWeaponMastery",
            "AcidProtection",
            "CreatureEnchantmentMastery",
            "ItemEnchantmentMastery",
            "LifeMagicMastery",
            "WarMagicMastery",
            "ManaMastery",
            "ArcaneEnlightenment",
            "ArcanumSalvaging",
            "ArmorExpertise",
            "ItemExpertise",
            "MagicItemExpertise",
            "WeaponExpertise",
            "MonsterAttunement",
            "PersonAttunement",
            "DeceptionMastery",
            "HealingMastery",
            "LeadershipMastery",
            "LockpickMastery",
            "Fealty",
            "JumpingMastery",
            "Sprint",
            "BludgeonProtection",
            "ColdProtection",
            "LightningProtection",
            "BladeProtection",
            "PiercingProtection",
            "Endurance",
            "Coordination",
            "Quickness",
            "Focus",
            "Willpower",
            "CookingMastery",
            "FletchingMastery",
            "AlchemyMastery",
            "VoidMagicMastery",
            "SummoningMastery",
            "SwiftKiller",
            "Defender",
            "BloodDrinker",
            "HeartSeeker",
            "HermeticLink",
            "SpiritDrinker",
            "DualWieldMastery",
            "TwoHandedMastery",
            "DirtyFightingMastery",
            "RecklessnessMastery",
            "SneakAttackMastery",
            "ShieldMastery",
            "@Impenetrability",
            "@PiercingBane",
            "@BludgeonBane",
            "@BladeBane",
            "@AcidBane",
            "@FlameBane",
            "@FrostBane",
            "@LightningBane",
#endregion
            };

        private class BuffMessage
        {
            public bool Bane { get; set; } = false;
            public GameEventMagicUpdateEnchantment SessionMessage { get; set; } = null;
            public GameMessageScript LandblockMessage { get; set; } = null;
            public Spell Spell { get; set; } = null;
            public Enchantment Enchantment { get; set; } = null;
            public void SetTargetPlayer(Player p)
            {
                Enchantment.Target = p;
                SessionMessage = new GameEventMagicUpdateEnchantment(p.Session, Enchantment);
                SetLandblockMessage(p.Guid);
            }
            public void SetLandblockMessage(ObjectGuid target)
            {
                LandblockMessage = new GameMessageScript(target, Spell.TargetEffect, 1f);
            }
        }

        private static BuffMessage BuildBuffMessage(uint spellID)
        {
            BuffMessage buff = new BuffMessage();
            buff.Spell = new Spell(spellID);
            if (buff.Spell.NotFound) return null;
            buff.Enchantment = new Enchantment(null, 0, spellID, 1, (EnchantmentMask)buff.Spell.StatModType, buff.Spell.StatModVal);
            return buff;
        }

        public void HandleSpellbookFilters(SpellBookFilterOptions filters)
        {
            Character.SpellbookFilters = (uint)filters;
        }

        public void HandleSetDesiredComponentLevel(uint component_wcid, uint amount)
        {
            // ensure wcid is spell component
            if (!SpellComponent.IsValid(component_wcid))
            {
                log.Warn($"{Name}.HandleSetDesiredComponentLevel({component_wcid}, {amount}): invalid spell component wcid");
                return;
            }
            if (amount > 0)
            {
                var existing = Character.GetFillComponent(component_wcid, CharacterDatabaseLock);

                if (existing == null)
                    Character.AddFillComponent(component_wcid, amount, CharacterDatabaseLock, out bool exists);
                else
                    existing.QuantityToRebuy = (int)amount;
            }
            else
                Character.TryRemoveFillComponent(component_wcid, out var _, CharacterDatabaseLock);

            CharacterChangesDetected = true;
        }

        public static Dictionary<MagicSchool, uint> FociWCIDs = new Dictionary<MagicSchool, uint>()
        {
            { MagicSchool.CreatureEnchantment, 15268 },   // Foci of Enchantment
            { MagicSchool.ItemEnchantment,     15269 },   // Foci of Artifice
            { MagicSchool.LifeMagic,           15270 },   // Foci of Verdancy
            { MagicSchool.WarMagic,            15271 },   // Foci of Strife
            { MagicSchool.VoidMagic,           43173 },   // Foci of Shadow
        };

        public bool HasFoci(MagicSchool school)
        {
            switch (school)
            {
                case MagicSchool.CreatureEnchantment:
                    if (AugmentationInfusedCreatureMagic > 0)
                        return true;
                    break;
                case MagicSchool.ItemEnchantment:
                    if (AugmentationInfusedItemMagic > 0)
                        return true;
                    break;
                case MagicSchool.LifeMagic:
                    if (AugmentationInfusedLifeMagic > 0)
                        return true;
                    break;
                case MagicSchool.VoidMagic:
                    if (AugmentationInfusedVoidMagic > 0)
                        return true;
                    break;
                case MagicSchool.WarMagic:
                    if (AugmentationInfusedWarMagic > 0)
                        return true;
                    break;
            }

            var wcid = FociWCIDs[school];
            return Inventory.Values.FirstOrDefault(i => i.WeenieClassId == wcid) != null;
        }

        public void HandleSpellHooks(Spell spell)
        {
            HandleMaxVitalUpdate(spell);

            // unsure if spell hook was here in retail,
            // but this has the potential to take the client out of autorun mode
            // which causes them to stop if they hit a turn key afterwards
            if (PropertyManager.GetBool("runrate_add_hooks").Item)
                HandleRunRateUpdate(spell);
        }

        /// <summary>
        /// Called when an enchantment is added or removed,
        /// checks if the spell affects the max vitals,
        /// and if so, updates the client immediately
        /// </summary>
        public void HandleMaxVitalUpdate(Spell spell)
        {
            var maxVitals = spell.UpdatesMaxVitals;

            if (maxVitals.Count == 0)
                return;

            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(1.0f);      // client needs time for primary attribute updates
            actionChain.AddAction(this, () =>
            {
                foreach (var maxVital in maxVitals)
                {
                    var playerVital = Vitals[maxVital];

                    Session.Network.EnqueueSend(new GameMessagePrivateUpdateAttribute2ndLevel(this, playerVital.ToEnum(), playerVital.Current));
                }
            });
            actionChain.EnqueueChain();
        }

        public bool HandleRunRateUpdate(Spell spell)
        {
            if (!spell.UpdatesRunRate)
                return false;

            return HandleRunRateUpdate();
        }

        public void AuditItemSpells()
        {
            // cleans up bugged chars with dangling item set spells
            // from previous bugs

            var allPossessions = GetAllPossessions().ToDictionary(i => i.Guid, i => i);

            // this is a legacy method, but is still a decent failsafe to catch any existing issues

            // get active item enchantments
            var enchantments = Biota.PropertiesEnchantmentRegistry.Clone(BiotaDatabaseLock).Where(i => i.Duration == -1 && i.SpellId != (int)SpellId.Vitae).ToList();

            foreach (var enchantment in enchantments)
            {
                var table = enchantment.HasSpellSetId ? allPossessions : EquippedObjects;

                // if this item is not equipped, remove enchantment
                if (!table.TryGetValue(new ObjectGuid(enchantment.CasterObjectId), out var item))
                {
                    var spell = new Spell(enchantment.SpellId, false);
                    log.Error($"{Name}.AuditItemSpells(): removing spell {spell.Name} from {(enchantment.HasSpellSetId ? "non-possessed" : "non-equipped")} item");

                    EnchantmentManager.Dispel(enchantment);
                    continue;
                }

                // is this item part of a set?
                if (!item.HasItemSet)
                    continue;

                // get all of the equipped items in this set
                var setItems = EquippedObjects.Values.Where(i => i.HasItemSet && i.EquipmentSetId == item.EquipmentSetId).ToList();

                // get all of the spells currently active from this set
                var currentSpells = GetSpellSet(setItems);

                // get all of the spells possible for this item set
                var possibleSpells = GetSpellSetAll((EquipmentSet)item.EquipmentSetId);

                // get the difference between them
                var inactiveSpells = possibleSpells.Except(currentSpells).ToList();

                // remove any item set spells that shouldn't be active
                foreach (var inactiveSpell in inactiveSpells)
                {
                    var removeSpells = enchantments.Where(i => i.SpellSetId == item.EquipmentSetId && i.SpellId == inactiveSpell.Id).ToList();

                    foreach (var removeSpell in removeSpells)
                    {
                        log.Error($"{Name}.AuditItemSpells(): removing spell {inactiveSpell.Name} from {item.EquipmentSetId}");

                        EnchantmentManager.Dispel(removeSpell);
                    }
                }
            }
        }

        public void ApplyDamageAndArmorBonuses(WorldObject item, int levelsGained)
        {
            var itemLevel = item.ItemLevel;

            if (itemLevel == null)
                itemLevel = 0;

            var bonusMultiplier = (float)(0.0025f * itemLevel);

            if (item.ItemTotalXp > 0 && item.Bonded != BondedStatus.Bonded && item.Attuned != AttunedStatus.Attuned)
            {
                item.Bonded = BondedStatus.Bonded;
                item.Attuned = AttunedStatus.Attuned;
            }

            for (int i = 0; i < levelsGained; i++)
            {
                if (item is MeleeWeapon meleeWeapon)
                {
                    int baseWeaponDamage = (int)meleeWeapon.WeaponBaseDamage.Value;

                    int maxBonus = (int)(baseWeaponDamage * 0.15);

                    float bonusPerLevel = maxBonus / 100f;

                    int bonusForCurrentItemLevel = (int)(bonusPerLevel * meleeWeapon.ItemLevel);

                    int numberOfIronTinks = 0;

                    if (meleeWeapon.TinkerLog != null)
                    {
                        int targetNumber = 61;

                        // Split the string by commas and count occurrences of targetNumber
                        numberOfIronTinks = meleeWeapon.TinkerLog.Split(',').Count(num => int.TryParse(num, out int n) && n == targetNumber);
                    }

                    int damageFromIronTinks = numberOfIronTinks * 1;

                    for (int j = 0; j < levelsGained; j++)
                    {
                        if (bonusForCurrentItemLevel > maxBonus)
                            bonusForCurrentItemLevel = maxBonus;

                        meleeWeapon.Damage = meleeWeapon.WeaponBaseDamage + bonusForCurrentItemLevel + damageFromIronTinks;
                    }
                }
                else if (item is MissileLauncher missileLauncher)
                {
                    float baseWeaponDamage = (float)missileLauncher.WeaponBaseDamageMod.Value;

                    float maxBonus = (float)(baseWeaponDamage * 0.15);

                    float bonusPerLevel = maxBonus / 100f;

                    float bonusForCurrentItemLevel = (float)(bonusPerLevel * missileLauncher.ItemLevel);

                    int numberOfMahoganyTinks = 0;

                    if (missileLauncher.TinkerLog != null)
                    {
                        int targetNumber = 74;

                        // Split the string by commas and count occurrences of targetNumber
                        numberOfMahoganyTinks = missileLauncher.TinkerLog.Split(',').Count(num => int.TryParse(num, out int n) && n == targetNumber);
                    }

                    float damageFromMahoganyTinks = 0.04f * numberOfMahoganyTinks;

                    for (int j = 0; j < levelsGained; j++)
                    {
                        if (bonusForCurrentItemLevel > maxBonus)
                            bonusForCurrentItemLevel = maxBonus;

                        missileLauncher.DamageMod = missileLauncher.WeaponBaseDamageMod + bonusForCurrentItemLevel + damageFromMahoganyTinks;
                    }
                }
                else if (item is Caster caster)
                {
                    float baseWeaponDamage = (float)caster.WeaponBaseDamageMod.Value;

                    float maxBonus = (float)(baseWeaponDamage * 0.015);

                    float bonusPerLevel = maxBonus / 100f;

                    float bonusForCurrentItemLevel = (float)(bonusPerLevel * caster.ItemLevel);

                    int numberOfGreenGarnetTinks = 0;

                    if (caster.TinkerLog != null)
                    {
                        int targetNumber = 23;

                        // Split the string by commas and count occurrences of targetNumber
                        numberOfGreenGarnetTinks = caster.TinkerLog.Split(',').Count(num => int.TryParse(num, out int n) && n == targetNumber);
                    }

                    float damageFromGreenGarnetTinks = 0.01f * numberOfGreenGarnetTinks;

                    for (int j = 0; j < levelsGained; j++)
                    {
                        if (bonusForCurrentItemLevel > maxBonus)
                            bonusForCurrentItemLevel = maxBonus;

                        caster.ElementalDamageMod = caster.WeaponBaseDamageMod + bonusForCurrentItemLevel + damageFromGreenGarnetTinks;
                    }
                }
                else if (item is Clothing clothing)
                {
                    if (clothing.BaseArmorLevel != null)
                    {
                        int baseArmorLevel = (int)clothing.BaseArmorLevel;

                        int maxBonus = (int)(baseArmorLevel * 0.15);

                        float bonusPerLevel = maxBonus / 100f;

                        int bonusForCurrentItemLevel = (int)(bonusPerLevel * clothing.ItemLevel);

                        int numberOfSteelTinks = 0;

                        if (clothing.TinkerLog != null)
                        {
                            if (clothing.TinkerLog.Count() > 0)
                            {
                                int targetNumber = 64;

                                numberOfSteelTinks = clothing.TinkerLog.Split(',').Count(num => int.TryParse(num, out int n) && n == targetNumber);
                            }
                        }

                        int armorFromSteelTinks = numberOfSteelTinks * 20;

                        for (int j = 0; j < levelsGained; j++)
                        {
                            if (bonusForCurrentItemLevel > maxBonus)
                                bonusForCurrentItemLevel = maxBonus;

                            clothing.ArmorLevel = clothing.BaseArmorLevel + bonusForCurrentItemLevel + armorFromSteelTinks;
                        }
                    }
                }
            }
        }

        public void ApplyRend(WorldObject item)
        {
            var damageType = item.GetProperty(PropertyInt.DamageType);

            if (item.ImbuedEffect == 0)
            {
                if (damageType == 1)
                {
                    item.SetProperty(PropertyInt.ImbuedEffect, 8);
                    item.SetProperty(PropertyDataId.IconUnderlay, 0x600335C);
                    var underlayUpdate = new GameMessagePrivateUpdateDataID(item, PropertyDataId.IconUnderlay, IconUnderlayId ?? 100676444);
                    Session.Network.EnqueueSend(underlayUpdate);
                }
                if (damageType == 2)
                {
                    item.SetProperty(PropertyInt.ImbuedEffect, 16);
                    item.SetProperty(PropertyDataId.IconUnderlay, 0x600335B);
                    var underlayUpdate = new GameMessagePrivateUpdateDataID(item, PropertyDataId.IconUnderlay, IconUnderlayId ?? 100676443);
                    Session.Network.EnqueueSend(underlayUpdate);
                }
                if (damageType == 4)
                {
                    item.SetProperty(PropertyInt.ImbuedEffect, 32);
                    item.SetProperty(PropertyDataId.IconUnderlay, 0x600335A);
                    var underlayUpdate = new GameMessagePrivateUpdateDataID(item, PropertyDataId.IconUnderlay, IconUnderlayId ?? 100676442);
                    Session.Network.EnqueueSend(underlayUpdate);
                }
                if (damageType == 8)
                {
                    item.SetProperty(PropertyInt.ImbuedEffect, 128);
                    item.SetProperty(PropertyDataId.IconUnderlay, 0x6003353);
                    var underlayUpdate = new GameMessagePrivateUpdateDataID(item, PropertyDataId.IconUnderlay, IconUnderlayId ?? 100676435);
                    Session.Network.EnqueueSend(underlayUpdate);
                }
                if (damageType == 16)
                {
                    item.SetProperty(PropertyInt.ImbuedEffect, 512);
                    item.SetProperty(PropertyDataId.IconUnderlay, 0x6003359);
                    var underlayUpdate = new GameMessagePrivateUpdateDataID(item, PropertyDataId.IconUnderlay, IconUnderlayId ?? 100676440);
                    Session.Network.EnqueueSend(underlayUpdate);
                }
                if (damageType == 32)
                {
                    item.SetProperty(PropertyInt.ImbuedEffect, 64);
                    item.SetProperty(PropertyDataId.IconUnderlay, 0x6003355);
                    var underlayUpdate = new GameMessagePrivateUpdateDataID(item, PropertyDataId.IconUnderlay, IconUnderlayId ?? 100676437);
                    Session.Network.EnqueueSend(underlayUpdate);
                }
                if (damageType == 64)
                {
                    item.SetProperty(PropertyInt.ImbuedEffect, 256);
                    item.SetProperty(PropertyDataId.IconUnderlay, 0x6003354);
                    var underlayUpdate = new GameMessagePrivateUpdateDataID(item, PropertyDataId.IconUnderlay, IconUnderlayId ?? 100676436);
                    Session.Network.EnqueueSend(underlayUpdate);
                }
                if (damageType == 1024)
                {
                    item.SetProperty(PropertyInt.ImbuedEffect, 16384);
                }
            }
        }

        public void ApplySlayer(WorldObject item)
        {
            if (item.ItemLevel == 100)
            {
                var monsterKillHistory = item.GetMonsterKillHistory();

                if (monsterKillHistory.Count > 0)
                {
                    int maxValue = monsterKillHistory.Values.Max();

                    var maxEntries = monsterKillHistory.Where(pair => pair.Value == maxValue);

                    foreach (var entry in maxEntries)
                    {
                        item.SlayerCreatureType = (CreatureType?)entry.Key;
                        item.SlayerDamageBonus = 1.25f;

                        var updateSlayerBonus = new GameMessagePrivateUpdatePropertyFloat(item, PropertyFloat.SlayerDamageBonus, 1.25f);
                        Session.Network.EnqueueSend(updateSlayerBonus);
                    }
                }
            }
        }
    }
}
