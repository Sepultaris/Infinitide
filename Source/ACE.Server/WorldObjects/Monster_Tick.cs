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

            if (IsPassivePet && this is Pet pet)
            {
                pet.Tick(currentUnixTime);
                return;
            }
            var position = Location.AsLocalPosition();
            bool inSetLandblock = RealmManager.Peripherals.DungeonSets.IncludedInSet(position, "default");

            if (inSetLandblock)
            {
                if (Level != Location.CalculateInstanceLevel(CurrentLandblock.Id))
                    CreatureStatsUpdated = false;

                if (this is not CombatPet && !CreatureStatsUpdated)
                {
                    var instanceLevel = Location.CalculateInstanceLevel(CurrentLandblock.Id);
                    var weenie = DatabaseManager.World.GetCachedWeenie(WeenieClassId);
                    var baseMaxHealth = weenie.GetPropertyAttribute2nd(PropertyAttribute2nd.MaxHealth).Value;
                    var baseMaxStamina = weenie.GetPropertyAttribute2nd(PropertyAttribute2nd.MaxStamina).Value;
                    var baseMaxMana = weenie.GetPropertyAttribute2nd(PropertyAttribute2nd.MaxMana).Value;

                    uint strength = (uint)(Strength.StartingValue * (uint)Math.Pow(1.005, instanceLevel / 10) / 10 * 0.3f);
                    uint endurance = (uint)(Endurance.StartingValue * (uint)Math.Pow(1.0063, instanceLevel / 10) / 10 * 0.3f);
                    uint coordination = (uint)(Coordination.StartingValue * (uint)Math.Pow(1.00767, instanceLevel / 10) / 10 * 0.3f);
                    uint quickness = (uint)(Quickness.StartingValue * (uint)Math.Pow(1.00767, instanceLevel / 10) / 10 * 0.3f);
                    uint self = (uint)(Self.StartingValue * (uint)Math.Pow(1.0062, instanceLevel / 10) / 10 * 0.3f);
                    uint focus = (uint)(Focus.StartingValue * (uint)Math.Pow(1.0062, instanceLevel / 10) / 10 * 0.3f);

                    uint newHealthValue = (uint)((baseMaxHealth * Math.Pow(1.007, instanceLevel / 10) / 10) * 0.3f);
                    uint newStaminaValue = (uint)((baseMaxStamina * Math.Pow(1.006, instanceLevel / 10) / 10) * 0.3f);
                    uint newManaValue = (uint)((baseMaxMana * Math.Pow(1.006, instanceLevel / 10) / 10) * 0.3f);

                    if (Vitals.ContainsKey(PropertyAttribute2nd.MaxHealth))
                        Vitals[PropertyAttribute2nd.MaxHealth].Ranks = newHealthValue;

                    if (Vitals.ContainsKey(PropertyAttribute2nd.MaxStamina))
                        Vitals[PropertyAttribute2nd.MaxStamina].Ranks = newStaminaValue;

                    if (Vitals.ContainsKey(PropertyAttribute2nd.MaxMana))
                        Vitals[PropertyAttribute2nd.MaxMana].Ranks = newManaValue;

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

                    Level = instanceLevel;
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

            var combatPet = this as CombatPet;

            var creatureTarget = AttackTarget as Creature;

            if (creatureTarget != null && (creatureTarget.IsDead || (combatPet == null && !IsVisibleTarget(creatureTarget))))
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
            if (combatPet != null)
                combatPet.PetCheckMonsters();
        }
    }
}
