using System;
using System.Collections.Generic;
using ACE.Database.Models.Shard;
using ACE.DatLoader;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Realms;
using ACE.Server.WorldObjects;

namespace ACE.Server.InfinitideMods
{
    public class InfinitidePlayer : Player
    {
        public InfinitidePlayer(Weenie weenie, ObjectGuid guid, uint accountId, AppliedRuleset ruleset) : base(weenie, guid, accountId, ruleset)
        {

        }

        public InfinitidePlayer(ACE.Entity.Models.Biota biota, IEnumerable<ACE.Database.Models.Shard.Biota> inventory, IEnumerable<ACE.Database.Models.Shard.Biota> wieldedItems, Character character, ISession session) : base( biota, inventory, wieldedItems, character, session)
        {

        }

        protected override void UpdateXpAndLevel(long amount, XpType xpType)
        {
            if (Level < 275)
            {
                base.UpdateXpAndLevel(amount, xpType);
            }
            else
            {
                AvailableExperience += amount;
                TotalExperience += amount;
            }
            
            if (Level >= 275)
            {
                CheckForLevelup();
                var xpTotalUpdate = new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.TotalExperience, TotalExperience ?? 0);
                var xpAvailUpdate = new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.AvailableExperience, AvailableExperience ?? 0);
                var currentTotalExperiance = new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.TotalExperience, TotalExperience ?? 0);
                Session.Network.EnqueueSend(xpTotalUpdate, xpAvailUpdate, currentTotalExperiance);

                if (xpType == XpType.Quest)
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"You've earned {amount:N0} experience.", ChatMessageType.Broadcast));

                if (HasVitae && xpType != XpType.Allegiance)
                    UpdateXpVitae(amount);
            }
        }

        public override ulong GetRemainingXP()
        {
            if (Level < 275)
                return base.GetRemainingXP();
            return 191226310247 - (ulong)TotalExperience;
        }

        public override void CheckForLevelup()
        {
            if (Level < 275)
            {
                base.CheckForLevelup();
                return;
            }

            var startingLevel = Level;
            var xpSinceLastLevel = TotalExperience - (191226310247 - GetXPForLevel(Level.Value));
            while (xpSinceLastLevel >= GetXPForLevel(Level.Value))
            {
                TotalExperience = (TotalExperience ?? 0) - GetXPForLevel(Level.Value);
                xpSinceLastLevel = xpSinceLastLevel - GetXPForLevel(Level.Value);
                Level++;
            }

            if (Level > startingLevel)
            {
                var message = $"You are now level {Level}!";

                message += (AvailableSkillCredits > 0) ? $"\nYou have {AvailableExperience:#,###0} experience points and {AvailableSkillCredits} skill credits available to raise skills and attributes." : $"\nYou have {AvailableExperience:#,###0} experience points available to raise skills and attributes.";
                message += $"\nYou will earn another skill credit at level {Level + 20}.";
                var levelUp = new GameMessagePrivateUpdatePropertyInt(this, PropertyInt.Level, Level ?? 1);
                var currentCredits = new GameMessagePrivateUpdatePropertyInt(this, PropertyInt.AvailableSkillCredits, AvailableSkillCredits ?? 0);
                
                // This won't trigger if we advance more then one level in a pass
                if (Level - 275 % 20 == 0)
                {
                    AvailableSkillCredits += 1;
                    TotalSkillCredits += 1;
                    PlayParticleEffect(PlayScript.LevelUp, Guid);
                }

                if (Fellowship != null)
                    Fellowship.OnFellowLevelUp(this);

                if (AllegianceNode != null)
                    AllegianceNode.OnLevelUp();

                Session.Network.EnqueueSend(levelUp);

                SetMaxVitals();

                Session.Network.EnqueueSend(new GameMessageSystemChat(message, ChatMessageType.Advancement), currentCredits);
            }
        }

        public override long GetXPBetweenLevels(int levelA, int levelB)
        {
            if (levelB <= 275)
                return base.GetXPBetweenLevels(levelA, levelB);

            if (levelA < 275)
            {
                var xpTill275 = base.GetXPBetweenLevels(levelA, 275);
                long xpTillB = 0;
                while (levelB >= 275)
                {
                    xpTillB += GetXPForLevel(levelB);
                    levelB--;
                }
                return xpTill275 + xpTillB;
            }

            long xp = 0;
            while (levelB >= levelA)
            {
                xp += GetXPForLevel(levelB);
                levelB--;
            }
            return xp;
        }

        public long GetXPForLevel(int level)
        {
            long xp = (long)(4000000000 * Math.Pow(1.001, level - 275));
            return Math.Min(100000000000, xp);
        }
    }
}
