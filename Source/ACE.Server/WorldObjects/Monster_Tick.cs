using System;
using ACE.Database;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Managers;
using ACE.Server.Realms.Peripherals;

namespace ACE.Server.WorldObjects
{
    partial class Creature
    {
        protected const double monsterTickInterval = 0.2;

        public double NextMonsterTickTime;

        private bool firstUpdate = true;

        /// <summary>
        /// Primary dispatch for monster think
        /// </summary>
        public void Monster_Tick(double currentUnixTime)
        {
            if (IsChessPiece && this is GamePiece gamePiece)
            {
                // faster than vtable?
                gamePiece.Tick(currentUnixTime);
                return;
            }

            var thisMonster = this;

            if (!IsPassivePet && this is CombatPet combatPet && IsMoving)
            {
                combatPet.CombatPetTick(currentUnixTime);
                return;
            }

            if (IsPassivePet && this is Pet pet)
            {
                pet.Tick(currentUnixTime);
                return;
            }

            var position = Location.AsLocalPosition();
            bool inSetLandblock = RealmManager.Peripherals.DungeonSets.IncludedInSet(position, "ShatteredDawn");
            var mobStrengthScaleFactor = PropertyManager.GetDouble("MobStrengthScaleFactor").Item;
            var mobEnduranceScaleFactor = PropertyManager.GetDouble("MobEnduranceScaleFactor").Item;
            var mobCoordinationScaleFactor = PropertyManager.GetDouble("MobCoordinationScaleFactor").Item;
            var mobQuicknessScaleFactor = PropertyManager.GetDouble("MobQuicknessScaleFactor").Item;
            var mobFocusScaleFactor = PropertyManager.GetDouble("MobFocusScaleFactor").Item;
            var mobSelfScaleFactor = PropertyManager.GetDouble("MobSelfScaleFactor").Item;
            var mobHealthScaleFactor = PropertyManager.GetDouble("MobHealthScaleFactor").Item;
            var mobStaminaScaleFactor = PropertyManager.GetDouble("MobStaminaScaleFactor").Item;
            var mobManaScaleFactor = PropertyManager.GetDouble("MobManaScaleFactor").Item;

            if (inSetLandblock)
            {
                if (this is not CombatPet && !CreatureStatsUpdated)
                {
                    var instanceLevel = Location.CalculateInstanceLevel(CurrentLandblock.Id);
                    var weenie = DatabaseManager.World.GetCachedWeenie(WeenieClassId);
                    var baseMaxHealth = weenie.GetPropertyAttribute2nd(PropertyAttribute2nd.MaxHealth).Value;
                    var baseMaxStamina = weenie.GetPropertyAttribute2nd(PropertyAttribute2nd.MaxStamina).Value;
                    var baseMaxMana = weenie.GetPropertyAttribute2nd(PropertyAttribute2nd.MaxMana).Value;

                    uint strength = (uint)((uint)Math.Max(instanceLevel - 275, 0) * mobStrengthScaleFactor);
                    uint endurance = (uint)((uint)Math.Max(instanceLevel - 275, 0) * mobEnduranceScaleFactor);
                    uint coordination = (uint)((uint)Math.Max(instanceLevel - 275, 0) * mobCoordinationScaleFactor);
                    uint quickness = (uint)((uint)Math.Max(instanceLevel - 275, 0) * mobQuicknessScaleFactor);
                    uint self = (uint)((uint)Math.Max(instanceLevel - 275, 0) * mobFocusScaleFactor);
                    uint focus = (uint)((uint)Math.Max(instanceLevel - 275, 0) * mobSelfScaleFactor);

                    uint newHealthValue = (uint)(baseMaxHealth + (uint)Math.Max(instanceLevel - 275, 0));
                    uint newStaminaValue = (uint)(baseMaxStamina + (uint)Math.Max(instanceLevel - 275, 0));
                    uint newManaValue = (uint)(baseMaxMana + (uint)Math.Max(instanceLevel - 275, 0));

                    if (Vitals.ContainsKey(PropertyAttribute2nd.MaxHealth))
                        Vitals[PropertyAttribute2nd.MaxHealth].StartingValue = (uint)(newHealthValue * mobHealthScaleFactor);

                    if (Vitals.ContainsKey(PropertyAttribute2nd.MaxStamina))
                        Vitals[PropertyAttribute2nd.MaxStamina].StartingValue = (uint)(newStaminaValue * mobStaminaScaleFactor);

                    if (Vitals.ContainsKey(PropertyAttribute2nd.MaxMana))
                        Vitals[PropertyAttribute2nd.MaxMana].StartingValue = (uint)(newManaValue * mobManaScaleFactor);

                    if (Attributes.ContainsKey(PropertyAttribute.Strength))
                        Attributes[PropertyAttribute.Strength].Ranks = strength;

                    if (Attributes.ContainsKey(PropertyAttribute.Endurance))
                        Attributes[PropertyAttribute.Endurance].Ranks = endurance;

                    if (Attributes.ContainsKey(PropertyAttribute.Coordination))
                        Attributes[PropertyAttribute.Coordination].Ranks = coordination;

                    if (Attributes.ContainsKey(PropertyAttribute.Quickness))
                        Attributes[PropertyAttribute.Quickness].Ranks = quickness;

                    if (Attributes.ContainsKey(PropertyAttribute.Focus))
                        Attributes[PropertyAttribute.Focus].Ranks = self;

                    if (Attributes.ContainsKey(PropertyAttribute.Self))
                        Attributes[PropertyAttribute.Self].Ranks = focus;

                    Health.Current = Health.MaxValue;
                    Stamina.Current = Stamina.MaxValue;
                    Mana.Current = Mana.MaxValue;

                    Level = Level + Math.Max(instanceLevel - 275, 0); 
                    CreatureStatsUpdated = true;
                }

                if (this is CombatPet && !CreatureStatsUpdated)
                {
                    var instanceLevel = Location.CalculateInstanceLevel(CurrentLandblock.Id);

                    Level = instanceLevel;

                    if (PetOwner != null)
                    {
                        var owner = PlayerManager.GetOnlinePlayer(PetOwner.Value);

                        if (owner != null)
                            instanceLevel = (int)owner.Level;

                        Level = (int)owner.Level;
                    }
                    
                    var weenie = DatabaseManager.World.GetCachedWeenie(WeenieClassId);
                    var baseMaxHealth = weenie.GetPropertyAttribute2nd(PropertyAttribute2nd.MaxHealth).Value;
                    var baseMaxStamina = weenie.GetPropertyAttribute2nd(PropertyAttribute2nd.MaxStamina).Value;
                    var baseMaxMana = weenie.GetPropertyAttribute2nd(PropertyAttribute2nd.MaxMana).Value;

                    uint strength = (uint)((uint)Math.Max(instanceLevel - 275, 0) * mobStrengthScaleFactor);
                    uint endurance = (uint)((uint)Math.Max(instanceLevel - 275, 0) * mobEnduranceScaleFactor);
                    uint coordination = (uint)((uint)Math.Max(instanceLevel - 275, 0) * mobCoordinationScaleFactor);
                    uint quickness = (uint)((uint)Math.Max(instanceLevel - 275, 0) * mobQuicknessScaleFactor);
                    uint self = (uint)((uint)Math.Max(instanceLevel - 275, 0) * mobFocusScaleFactor);
                    uint focus = (uint)((uint)Math.Max(instanceLevel - 275, 0) * mobSelfScaleFactor);

                    uint newHealthValue = baseMaxHealth + (uint)Math.Max(instanceLevel - 275, 0);
                    uint newStaminaValue = baseMaxStamina + (uint)Math.Max(instanceLevel - 275, 0);
                    uint newManaValue = baseMaxMana + (uint)Math.Max(instanceLevel - 275, 0);

                    if (Vitals.ContainsKey(PropertyAttribute2nd.MaxHealth))
                        Vitals[PropertyAttribute2nd.MaxHealth].StartingValue = (uint)(newHealthValue * mobHealthScaleFactor);

                    if (Vitals.ContainsKey(PropertyAttribute2nd.MaxStamina))
                        Vitals[PropertyAttribute2nd.MaxStamina].StartingValue = (uint)(newStaminaValue * mobStaminaScaleFactor);

                    if (Vitals.ContainsKey(PropertyAttribute2nd.MaxMana))
                        Vitals[PropertyAttribute2nd.MaxMana].StartingValue = (uint)(newManaValue * mobManaScaleFactor);

                    if (Attributes.ContainsKey(PropertyAttribute.Strength))
                        Attributes[PropertyAttribute.Strength].Ranks = strength;

                    if (Attributes.ContainsKey(PropertyAttribute.Endurance))
                        Attributes[PropertyAttribute.Endurance].Ranks = endurance;

                    if (Attributes.ContainsKey(PropertyAttribute.Coordination))
                        Attributes[PropertyAttribute.Coordination].Ranks = coordination;

                    if (Attributes.ContainsKey(PropertyAttribute.Quickness))
                        Attributes[PropertyAttribute.Quickness].Ranks = quickness;

                    if (Attributes.ContainsKey(PropertyAttribute.Focus))
                        Attributes[PropertyAttribute.Focus].Ranks = self;

                    if (Attributes.ContainsKey(PropertyAttribute.Self))
                        Attributes[PropertyAttribute.Self].Ranks = focus;

                    Health.Current = Health.MaxValue;
                    Stamina.Current = Stamina.MaxValue;
                    Mana.Current = Mana.MaxValue;
                    
                    CreatureStatsUpdated = true;
                }
            }

            NextMonsterTickTime = currentUnixTime + monsterTickInterval;

            if (!IsAwake)
            {
                if (MonsterState == State.Return)
                    MonsterState = State.Idle;

                if (IsFactionMob || HasFoeType)
                    FactionMob_CheckMonsters();

                return;
            }

            if (IsDead) return;

            if (EmoteManager.IsBusy) return;

            HandleFindTarget();

            CheckMissHome();    // tickrate?

            if (AttackTarget == null && MonsterState != State.Return)
            {
                Sleep();
                return;
            }

            if (MonsterState == State.Return)
            {
                Movement();
                return;
            }

            var combatPet1 = this as CombatPet;

            var creatureTarget = AttackTarget as Creature;

            if (creatureTarget != null && (creatureTarget.IsDead || (combatPet1 == null && !IsVisibleTarget(creatureTarget))))
            {
                FindNextTarget();
                return;
            }

            if (firstUpdate)
            {
                if (CurrentMotionState.Stance == MotionStance.NonCombat)
                    DoAttackStance();

                if (IsAnimating)
                {
                    //PhysicsObj.ShowPendingMotions();
                    PhysicsObj.update_object(Location.Instance);
                    return;
                }

                firstUpdate = false;
            }

            // select a new weapon if missile launcher is out of ammo
            var weapon = GetEquippedWeapon();
            /*if (weapon != null && weapon.IsAmmoLauncher)
            {
                var ammo = GetEquippedAmmo();
                if (ammo == null)
                    SwitchToMeleeAttack();
            }*/

            if (weapon == null && CurrentAttack != null && CurrentAttack == CombatType.Missile)
            {
                EquipInventoryItems(true);
                DoAttackStance();
                CurrentAttack = null;
            }

            // decide current type of attack
            if (CurrentAttack == null)
            {
                CurrentAttack = GetNextAttackType();
                MaxRange = GetMaxRange();

                //if (CurrentAttack == AttackType.Magic)
                //MaxRange = MaxMeleeRange;   // FIXME: server position sync
            }

            if (PhysicsObj.IsSticky)
                UpdatePosition(false);

            // get distance to target
            var targetDist = GetDistanceToTarget();
            //Console.WriteLine($"{Name} ({Guid}) - Dist: {targetDist}");

            if (CurrentAttack != CombatType.Missile)
            {
                if (targetDist > MaxRange || (!IsFacing(AttackTarget) && !IsSelfCast()))
                {
                    // turn / move towards
                    if (!IsTurning && !IsMoving)
                        StartTurn();
                    else
                        Movement();
                }
                else
                {
                    // perform attack
                    if (AttackReady())
                        Attack();
                }
            }
            else
            {
                if (IsTurning || IsMoving)
                {
                    Movement();
                    return;
                }

                if (!IsFacing(AttackTarget))
                {
                    StartTurn();
                }
                else if (targetDist <= MaxRange)
                {
                    // perform attack
                    if (AttackReady())
                        Attack();
                }
                else
                {
                    // monster switches to melee combat immediately,
                    // if target is beyond max range?

                    // should ranged mobs only get CurrentTargets within MaxRange?
                    //Console.WriteLine($"{Name}.MissileAttack({AttackTarget.Name}): targetDist={targetDist}, MaxRange={MaxRange}, switching to melee");
                    TrySwitchToMeleeAttack();
                }
            }

            // pets drawing aggro
            if (combatPet1 != null)
                combatPet1.PetCheckMonsters();
        }
    }
}
