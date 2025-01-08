using System;
using System.Linq;
using ACE.Common;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Server.WorldObjects.Entity;

namespace ACE.Server.Command.Handlers
{
    public static class InfinitidePlayersCommands
    {
        [CommandHandler("rfel", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0, ".")]
        [CommandHandler("recall_fellowship", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 1, ".")]
        public static void HandleRecallFellowship(ISession session, params string[] parameters)
        {
            if (session.Player.Fellowship != null)
            {
                foreach (var f in session.Player.GetFellowshipTargets().Where(x => x.Name != session.Player.Name))
                {
                    string Joiner = string.Join(" ", f.Name);
                    HandleRecallFriend(session, Joiner);
                    return;
                }
            }
        }

        [CommandHandler("rf", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 1, ".")]
        [CommandHandler("recall_friend", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 1, ".")]
        public static void HandleRF(ISession session, params string[] parameters)
        {
            if (parameters[0] != null && parameters[0] is string)
            {
                string Joiner = string.Join(" ", parameters);
                HandleRecallFriend(session, Joiner);
                return;
            }
        } 

        [CommandHandler("raise", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 1, "Allows you to raise attributes past maximum. Allows you to raise Luminance Augmentation for Damage Rating (Destruction), Damage Reduction (Invulnerability), Critical Damage (Glory) and Critical Damage Reduction (Temperance) and Max Health, Stamina, and Mana (Vitality).")]
        public static void HandleRaise(ISession session, params string[] parameters)
        {
            Player player = session.Player;

            if (parameters.Length < 1)
            {
                ChatPacket.SendServerMessage(session, "Usage: /raise <str/end/coord/quick/focus/self/hp/stam/mp/mana> <destruction/invulnerability/glory/temperance/vitality>", ChatMessageType.Broadcast);
                return;
            }
            int result = 1;
            if (parameters.Length > 1 && !int.TryParse(parameters[1], out result))
            {
                ChatPacket.SendServerMessage(session, "Invalid value, values must be valid integers", ChatMessageType.Broadcast);
                return;
            }
            if (result <= 0)
            {
                ChatPacket.SendServerMessage(session, "Invalid value, values must be valid integers", ChatMessageType.Broadcast);
                return;
            }
            parameters[0] = parameters[0].ToLower();
            if (parameters[0].Equals("hp"))
            {
                parameters[0] = "health";
            }
            if (parameters[0].Equals("health"))
            {
                /*HandleRaiseHealth(player, session, result);*/

                for (int i = 0; i < result; i++)
                {
                    var costToRaise = GetXPForAttributeRaise(player.RaisedHealth);

                    if (GetXPForAttributeRaise(player.RaisedHealth) > player.AvailableExperience)
                    {
                        player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxHealth]));
                        ChatPacket.SendServerMessage(session, string.Format("Your Maximum Health has been increased by {0}.", i), ChatMessageType.Broadcast);
                        ChatPacket.SendServerMessage(session, $"Not enough experience for remaining points, you require {costToRaise:N0} XP.", ChatMessageType.Broadcast);
                        return;
                    }
                    if (!player.SpendXP(GetXPForAttributeRaise(player.RaisedHealth)))
                    {
                        player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxHealth]));
                        ChatPacket.SendServerMessage(session, string.Format("Your Maximum Health has been increased by {0}.", i), ChatMessageType.Broadcast);
                        ChatPacket.SendServerMessage(session, $"Not enough experience for remaining points, you require {costToRaise:N0} XP.", ChatMessageType.Broadcast);
                        return;
                    }
                    CreatureVital creatureVital = new CreatureVital(player, PropertyAttribute2nd.MaxHealth);
                    creatureVital.Ranks = Math.Clamp(creatureVital.Ranks + 1, 1u, uint.MaxValue);
                    player.UpdateVital(creatureVital, creatureVital.MaxValue);
                    CreatureVital creatureVital2 = new CreatureVital(player, PropertyAttribute2nd.Health);
                    creatureVital2.Ranks = Math.Clamp(creatureVital2.Ranks + 1, 1u, uint.MaxValue);
                    player.UpdateVital(creatureVital2, creatureVital2.MaxValue);
                    player.RaisedHealth++;
                }
                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxHealth]));
                ChatPacket.SendServerMessage(session, string.Format("Your Maximum Health has been increased by {0}.", result), ChatMessageType.Broadcast);
                return;
            }
            if (parameters[0].Equals("stam"))
            {
                parameters[0] = "stamina";
            }
            if (parameters[0].Equals("stamina"))
            {
                for (int j = 0; j < result; j++)
                {
                    var costToRaise = GetXPForAttributeRaise(player.RaisedStamina);

                    if (GetXPForAttributeRaise(player.RaisedStamina) > player.AvailableExperience)
                    {
                        player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxStamina]));
                        ChatPacket.SendServerMessage(session, string.Format("Your Maximum Stamina has been increased by {0}.", j), ChatMessageType.Broadcast);
                        ChatPacket.SendServerMessage(session, $"Not enough experience for remaining points, you require {costToRaise:N0} XP.", ChatMessageType.Broadcast);
                        return;
                    }
                    if (!player.SpendXP(GetXPForAttributeRaise(player.RaisedStamina)))
                    {
                        player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxStamina]));
                        ChatPacket.SendServerMessage(session, string.Format("Your Maximum Stamina has been increased by {0}.", j), ChatMessageType.Broadcast);
                        ChatPacket.SendServerMessage(session, $"Not enough experience for remaining points, you require {costToRaise:N0} XP.", ChatMessageType.Broadcast);
                        return;
                    }
                    CreatureVital creatureVital3 = new CreatureVital(player, PropertyAttribute2nd.MaxStamina);
                    creatureVital3.Ranks = Math.Clamp(creatureVital3.Ranks + 1, 1u, uint.MaxValue);
                    player.UpdateVital(creatureVital3, creatureVital3.MaxValue);
                    CreatureVital creatureVital4 = new CreatureVital(player, PropertyAttribute2nd.Stamina);
                    creatureVital4.Ranks = Math.Clamp(creatureVital4.Ranks + 1, 1u, uint.MaxValue);
                    player.UpdateVital(creatureVital4, creatureVital4.MaxValue);
                    player.RaisedStamina++;
                }
                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxStamina]));
                ChatPacket.SendServerMessage(session, string.Format("Your Maximum Stamina has been increased by {0}.", result), ChatMessageType.Broadcast);
                return;
            }

            int _luminanceRating = 0;

            // allows spending of luminance to increase luminance damage rating
            if (parameters[0].ToLowerInvariant().Equals("destruction"))
            {

                _luminanceRating = player.LumAugDamageRating;
                if (_luminanceRating == 0)
                    player.LumAugDamageRating = 1;
                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugDamageRating, _luminanceRating));

                for (int j = 0; j < result; j++)
                {
                    _luminanceRating = player.LumAugDamageRating;

                    var destruction = player.GetProperty(PropertyInt.LumAugDamageRating);
                    var destructioncost = GetLumForAttributeRaise((int)destruction);

                    // while looping through the number of increases requested - if the total available is not enough to keep looping
                    // break out of the loop and inform the player of how many increases they received
                    if (destructioncost > player.AvailableLuminance || !player.SpendLuminance((long)destructioncost))
                    {
                        player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugDamageRating, _luminanceRating));
                        ChatPacket.SendServerMessage(session, string.Format("Your Luminance Augmentation Damage Rating has been increased by {0}.", j), ChatMessageType.Broadcast);
                        ChatPacket.SendServerMessage(session, $"Not enough Luminance for remaining points, you require {destructioncost:N0} luminance.", ChatMessageType.Broadcast);
                        return;
                    }
                    player.LumAugDamageRating++;
                }
                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugDamageRating, player.LumAugDamageRating));
                ChatPacket.SendServerMessage(session, string.Format("Your Luminance Augmentation Damage Rating has been increased by {0}.", result), ChatMessageType.Broadcast);
                return;
            }


            // allows spending of luminance to increase luminance damage reduction rating (Invulnerability)
            if (parameters[0].ToLowerInvariant().Equals("invulnerability"))
            {
                for (int j = 0; j < result; j++)
                {
                    _luminanceRating = player.LumAugDamageReductionRating;
                    if (_luminanceRating == 0)
                        player.LumAugDamageReductionRating = 1;
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugDamageReductionRating, _luminanceRating));

                    var invulnerability = player.GetProperty(PropertyInt.LumAugDamageReductionRating);
                    var invulnerabilitycost = GetLumForAttributeRaise((int)invulnerability);

                    // while looping through the number of increases requested - if the total available is not enough to keep looping
                    // break out of the loop and inform the player of how many increases they received
                    if (invulnerabilitycost > player.AvailableLuminance || !player.SpendLuminance((long)invulnerabilitycost))
                    {
                        player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugDamageReductionRating, _luminanceRating));
                        ChatPacket.SendServerMessage(session, string.Format("Your Luminance Augmentation Damage Reduction Rating has been increased by {0}.", j), ChatMessageType.Broadcast);
                        ChatPacket.SendServerMessage(session, $"Not enough Luminance for remaining points, you require {invulnerabilitycost:N0} luminance.", ChatMessageType.Broadcast);
                        return;
                    }
                    player.LumAugDamageReductionRating++;
                }
                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugDamageReductionRating, player.LumAugDamageReductionRating));
                ChatPacket.SendServerMessage(session, string.Format("Your Luminance Augmentation Damage Reduction Rating has been increased by {0}.", result), ChatMessageType.Broadcast);
                return;
            }


            // allows spending of luminance to increase luminance critical damage rating (Glory)
            if (parameters[0].ToLowerInvariant().Equals("glory"))
            {
                for (int j = 0; j < result; j++)
                {
                    _luminanceRating = player.LumAugCritDamageRating;
                    if (_luminanceRating == 0)
                        player.LumAugCritDamageRating = 1;
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugCritDamageRating, _luminanceRating));

                    var glory = player.GetProperty(PropertyInt.LumAugCritDamageRating);
                    var glorycost = GetLumForAttributeRaise((int)glory);

                    // while looping through the number of increases requested - if the total available is not enough to keep looping
                    // break out of the loop and inform the player of how many increases they received
                    if (glorycost > player.AvailableLuminance || !player.SpendLuminance((long)glorycost))
                    {
                        player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugCritDamageRating, _luminanceRating));
                        ChatPacket.SendServerMessage(session, string.Format("Your Luminance Augmentation Critical Damage Rating has been increased by {0}.", j), ChatMessageType.Broadcast);
                        ChatPacket.SendServerMessage(session, $"Not enough Luminance for remaining points, you require {glorycost:N0} luminance.", ChatMessageType.Broadcast);
                        return;
                    }
                    player.LumAugCritDamageRating++;
                }
                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugCritDamageRating, player.LumAugCritDamageRating));
                ChatPacket.SendServerMessage(session, string.Format("Your Luminance Augmentation Critical Damage Rating has been increased by {0}.", result), ChatMessageType.Broadcast);
                return;
            }


            // allows spending of luminance to increase luminance critical damage reduction rating (Temperance)
            if (parameters[0].ToLowerInvariant().Equals("temperance"))
            {
                for (int j = 0; j < result; j++)
                {
                    _luminanceRating = player.LumAugCritReductionRating;
                    if (_luminanceRating == 0)
                        player.LumAugCritReductionRating = 1;
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugCritReductionRating, _luminanceRating));

                    var temperance = player.GetProperty(PropertyInt.LumAugCritReductionRating);
                    var temperancecost = GetLumForAttributeRaise((int)temperance);

                    // while looping through the number of increases requested - if the total available is not enough to keep looping
                    // break out of the loop and inform the player of how many increases they received
                    if (temperancecost > player.AvailableLuminance || !player.SpendLuminance((long)temperancecost))
                    {
                        player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugCritReductionRating, _luminanceRating));
                        ChatPacket.SendServerMessage(session, string.Format("Your Luminance Augmentation Critical Damage Reduction Rating has been increased by {0}.", j), ChatMessageType.Broadcast);
                        ChatPacket.SendServerMessage(session, $"Not enough Luminance for remaining points, you require {temperancecost:N0} luminance.", ChatMessageType.Broadcast);
                        return;
                    }
                    player.LumAugCritReductionRating++;
                }
                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.LumAugCritReductionRating, player.LumAugCritReductionRating));
                ChatPacket.SendServerMessage(session, string.Format("Your Luminance Augmentation Critical Damage Reduction Rating has been increased by {0}.", result), ChatMessageType.Broadcast);
                return;
            }

            CreatureVital maxHealth = new CreatureVital(player, PropertyAttribute2nd.MaxHealth);

            /*if (parameters[0].ToLowerInvariant().Equals("vitality"))
            {
                if (maxHealth.Ranks < 5000)
                {
                    for (int j = 0; j < result; j++)
                    {
                        if (10000000L > player.AvailableLuminance)
                        {
                            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxHealth]));
                            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxStamina]));
                            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxMana]));
                            ChatPacket.SendServerMessage(session, string.Format("Your Vitality has been increased by {0}.", j), ChatMessageType.Broadcast);
                            ChatPacket.SendServerMessage(session, "Not enough Luminance for remaining points, you require 10 million (10,000,000) Luminance per point.", ChatMessageType.Broadcast);
                            return;
                        }
                        if (!player.SpendLuminance(10000000L))
                        {
                            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxHealth]));
                            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxStamina]));
                            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxMana]));
                            ChatPacket.SendServerMessage(session, string.Format("Your Vitality has been increased by {0}.", j), ChatMessageType.Broadcast);
                            ChatPacket.SendServerMessage(session, "Not enough Luminance for remaining points, you require 10 million (10,000,000) Luminance per point.", ChatMessageType.Broadcast);
                            return;
                        }
                        CreatureVital creatureVital1 = new CreatureVital(player, PropertyAttribute2nd.MaxHealth);
                        creatureVital1.Ranks = Math.Clamp(creatureVital1.Ranks + 1, 1u, uint.MaxValue);
                        player.UpdateVital(creatureVital1, creatureVital1.MaxValue);
                        CreatureVital creatureVital2 = new CreatureVital(player, PropertyAttribute2nd.Health);
                        creatureVital2.Ranks = Math.Clamp(creatureVital2.Ranks + 1, 1u, uint.MaxValue);
                        player.UpdateVital(creatureVital2, creatureVital2.MaxValue);
                        CreatureVital creatureVital3 = new CreatureVital(player, PropertyAttribute2nd.MaxStamina);
                        creatureVital3.Ranks = Math.Clamp(creatureVital3.Ranks + 1, 1u, uint.MaxValue);
                        player.UpdateVital(creatureVital3, creatureVital3.MaxValue);
                        CreatureVital creatureVital4 = new CreatureVital(player, PropertyAttribute2nd.Stamina);
                        creatureVital4.Ranks = Math.Clamp(creatureVital4.Ranks + 1, 1u, uint.MaxValue);
                        player.UpdateVital(creatureVital4, creatureVital4.MaxValue);
                        CreatureVital creatureVital5 = new CreatureVital(player, PropertyAttribute2nd.MaxMana);
                        creatureVital5.Ranks = Math.Clamp(creatureVital5.Ranks + 1, 1u, uint.MaxValue);
                        player.UpdateVital(creatureVital5, creatureVital5.MaxValue);
                        CreatureVital creatureVital6 = new CreatureVital(player, PropertyAttribute2nd.Mana);
                        creatureVital6.Ranks = Math.Clamp(creatureVital6.Ranks + 1, 1u, uint.MaxValue);
                        player.UpdateVital(creatureVital6, creatureVital6.MaxValue);
                    }
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxHealth]));
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxStamina]));
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxMana]));
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxStamina]));
                    return;
                }

                if (maxHealth.Ranks >= 5000)
                {
                    ChatPacket.SendServerMessage(session, "You have reached Maximum Vitality", ChatMessageType.Broadcast);
                    return;
                }

            }*/
            if (parameters[0].Equals("mana"))
            {
                for (int k = 0; k < result; k++)
                {
                    var costToRaise = GetXPForAttributeRaise(player.RaisedMana);

                    if (GetXPForAttributeRaise(player.RaisedMana) > player.AvailableExperience)
                    {
                        player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxMana]));
                        ChatPacket.SendServerMessage(session, string.Format("Your Maximum Mana has been increased by {0}.", k), ChatMessageType.Broadcast);
                        ChatPacket.SendServerMessage(session, $"Not enough experience for remaining points, you require {costToRaise:N0} XP.", ChatMessageType.Broadcast);
                        return;
                    }
                    if (!player.SpendXP(GetXPForAttributeRaise(player.RaisedMana)))
                    {
                        player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxMana]));
                        ChatPacket.SendServerMessage(session, string.Format("Your Maximum Mana has been increased by {0}.", k), ChatMessageType.Broadcast);
                        ChatPacket.SendServerMessage(session, $"Not enough experience for remaining points, you require {costToRaise:N0} XP.", ChatMessageType.Broadcast);
                        return;
                    }
                    CreatureVital creatureVital5 = new CreatureVital(player, PropertyAttribute2nd.MaxMana);
                    creatureVital5.Ranks = Math.Clamp(creatureVital5.Ranks + 1, 1u, uint.MaxValue);
                    player.UpdateVital(creatureVital5, creatureVital5.MaxValue);
                    CreatureVital creatureVital6 = new CreatureVital(player, PropertyAttribute2nd.Mana);
                    creatureVital6.Ranks = Math.Clamp(creatureVital6.Ranks + 1, 1u, uint.MaxValue);
                    player.UpdateVital(creatureVital6, creatureVital6.MaxValue);
                    player.RaisedMana++;
                }
                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateVital(player, player.Vitals[PropertyAttribute2nd.MaxMana]));
                ChatPacket.SendServerMessage(session, string.Format("Your Maximum Mana has been increased by {0}.", result), ChatMessageType.Broadcast);
                return;
            }
            if (parameters[0].Equals("str") || parameters[0].Equals("strength"))
            {
                parameters[0] = "Strength";
            }
            else if (parameters[0].Equals("end") || parameters[0].Equals("endurance"))
            {
                parameters[0] = "Endurance";
            }
            else if (parameters[0].Equals("coord") || parameters[0].Equals("coordination"))
            {
                parameters[0] = "Coordination";
            }
            else if (parameters[0].Equals("quick") || parameters[0].Equals("quickness"))
            {
                parameters[0] = "Quickness";
            }
            else if (parameters[0].Equals("focus") || parameters[0].Equals("focus"))
            {
                parameters[0] = "Focus";
            }
            else if (parameters[0].Equals("self") || parameters[0].Equals("self"))
            {
                parameters[0] = "Self";
            }
            PropertyAttribute result2;
            if (!System.Enum.TryParse<PropertyAttribute>(parameters[0], out result2))
            {
                ChatPacket.SendServerMessage(session, "Invalid Attribute, valid values are: Strength,Endurance,Coordination,Quickness,Focus,Self,Health,Stamina,Mana", ChatMessageType.Broadcast);
                return;
            }
            for (int l = 0; l < result; l++)
            {
                if (GetXPForAttributeRaise((int)player.Attributes[result2].StartingValue) > player.AvailableExperience)
                {
                    var costToRaise = GetXPForAttributeRaise((int)player.Attributes[result2].StartingValue);

                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateAttribute(player, player.Attributes[result2]));
                    ChatPacket.SendServerMessage(session, string.Format("Your {0} has been increased by {1}.", result2.ToString(), l), ChatMessageType.Broadcast);
                    ChatPacket.SendServerMessage(session, $"Not enough experience for remaining points, you require {costToRaise:N0} XP.", ChatMessageType.Broadcast);
                    return;
                }
                if (!player.SpendXP(GetXPForAttributeRaise((int)player.Attributes[result2].StartingValue)))
                {
                    var costToRaise = GetXPForAttributeRaise((int)player.Attributes[result2].StartingValue);

                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateAttribute(player, player.Attributes[result2]));
                    ChatPacket.SendServerMessage(session, string.Format("Your {0} has been increased by {1}.", result2.ToString(), l), ChatMessageType.Broadcast);
                    ChatPacket.SendServerMessage(session, $"Not enough experience for remaining points, you require {costToRaise:N0} XP.", ChatMessageType.Broadcast);
                    return;
                }
                player.Attributes[result2].StartingValue++;
            }
            player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateAttribute(player, player.Attributes[result2]));
            ChatPacket.SendServerMessage(session, string.Format("Your {0} has been increased by {1}.", result2.ToString(), result), ChatMessageType.Broadcast);

            for (int l = 0; l < result; l++)
            {
                if (parameters[0].Equals("Strength"))
                {
                    player.RaisedStr++;
                }
                else if (parameters[0].Equals("Endurance"))
                {
                    player.RaisedEnd++;
                }
                else if (parameters[0].Equals("Coordination"))
                {
                    player.RaisedCoord++;
                }
                else if (parameters[0].Equals("Quickness"))
                {
                    player.RaisedQuick++;
                }
                else if (parameters[0].Equals("Focus"))
                {
                    player.RaisedFocus++;
                }
                else if (parameters[0].Equals("Self"))
                {
                    player.RaisedSelf++;
                }
            }
        }

        [CommandHandler("raisecost", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 1, "Displays the cost to raise for a given attribute or luminance augmentation rating.")]
        public static void HandleRaiseCost(ISession session, params string[] parameters)
        {
            if (parameters[0].Equals("str") || parameters[0].Equals("strength"))
            {
                parameters[0] = "Strength";
            }
            else if (parameters[0].Equals("end") || parameters[0].Equals("endurance"))
            {
                parameters[0] = "Endurance";
            }
            else if (parameters[0].Equals("coord") || parameters[0].Equals("coordination"))
            {
                parameters[0] = "Coordination";
            }
            else if (parameters[0].Equals("quick") || parameters[0].Equals("quickness"))
            {
                parameters[0] = "Quickness";
            }
            else if (parameters[0].Equals("focus") || parameters[0].Equals("focus"))
            {
                parameters[0] = "Focus";
            }
            else if (parameters[0].Equals("self") || parameters[0].Equals("self"))
            {
                parameters[0] = "Self";
            }
            else if (parameters[0].Equals("health"))
                parameters[0] = "Health";
            else if (parameters[0].Equals("stamina"))
                parameters[0] = "Stamina";
            else if (parameters[0].Equals("mana"))
                parameters[0] = "Mana";
            else if (parameters[0].Equals("destruction"))
                parameters[0] = "Destruction";

            /*PropertyAttribute result2;
            if (!System.Enum.TryParse<PropertyAttribute>(parameters[0], out result2))
            {
                ChatPacket.SendServerMessage(session, "Invalid Attribute, valid values are: Strength,Endurance,Coordination,Quickness,Focus,Self,Health,Stamina,Mana", ChatMessageType.Broadcast);
                return;
            }*/

            if (parameters[0] == "Strength")
            {
                var costToRaise = GetXPForAttributeRaise(session.Player.RaisedStr);
                ChatPacket.SendServerMessage(session, $"You require {costToRaise:N0} XP to raise Strength", ChatMessageType.Broadcast);
                return;
            }
            else if (parameters[0] == "Endurance")
            {
                var costToRaise = GetXPForAttributeRaise(session.Player.RaisedEnd);
                ChatPacket.SendServerMessage(session, $"You require {costToRaise:N0} XP to raise Endurance", ChatMessageType.Broadcast);
                return;
            }
            else if (parameters[0] == "Coordination")
            {
                var costToRaise = GetXPForAttributeRaise(session.Player.RaisedCoord);
                ChatPacket.SendServerMessage(session, $"You require {costToRaise:N0} XP to raise Coordination", ChatMessageType.Broadcast);
                return;
            }
            else if (parameters[0] == "Quickness")
            {
                var costToRaise = GetXPForAttributeRaise(session.Player.RaisedQuick);
                ChatPacket.SendServerMessage(session, $"You require {costToRaise:N0} XP to raise Quickness", ChatMessageType.Broadcast);
                return;
            }
            else if (parameters[0] == "Focus")
            {
                var costToRaise = GetXPForAttributeRaise(session.Player.RaisedFocus);
                ChatPacket.SendServerMessage(session, $"You require {costToRaise:N0} XP to raise Focus", ChatMessageType.Broadcast);
                return;
            }
            else if (parameters[0] == "Self")
            {
                var costToRaise = GetXPForAttributeRaise(session.Player.RaisedSelf);
                ChatPacket.SendServerMessage(session, $"You require {costToRaise:N0} XP to raise Self", ChatMessageType.Broadcast);
                return;
            }
            else if (parameters[0] == "Health")
            {
                var costToRaise = GetXPForAttributeRaise(session.Player.RaisedHealth);
                ChatPacket.SendServerMessage(session, $"You require {costToRaise:N0} XP to raise Health", ChatMessageType.Broadcast);
                return;
            }
            else if (parameters[0] == "Stamina")
            {
                var costToRaise = GetXPForAttributeRaise(session.Player.RaisedStamina);
                ChatPacket.SendServerMessage(session, $"You require {costToRaise:N0} XP to raise Stamina", ChatMessageType.Broadcast);
                return;
            }
            else if (parameters[0] == "Mana")
            {
                var costToRaise = GetXPForAttributeRaise(session.Player.RaisedMana);
                ChatPacket.SendServerMessage(session, $"You require {costToRaise:N0} XP to raise Mana", ChatMessageType.Broadcast);
                return;
            }
            else if (parameters[0] == "Destruction")
            {
                var costToRaise = GetLumForAttributeRaise(session.Player.LumAugDamageRating);
                ChatPacket.SendServerMessage(session, $"You require {costToRaise:N0} Luminance to raise Destruction", ChatMessageType.Broadcast);
                return;
            }
            else if (parameters[0] == "Invulnerability")
            {
                var costToRaise = GetLumForAttributeRaise(session.Player.LumAugDamageReductionRating);
                ChatPacket.SendServerMessage(session, $"You require {costToRaise:N0} Luminance to raise Invulnerability", ChatMessageType.Broadcast);
                return;
            }
            else if (parameters[0] == "Glory")
            {
                var costToRaise = GetLumForAttributeRaise(session.Player.LumAugCritDamageRating);
                ChatPacket.SendServerMessage(session, $"You require {costToRaise:N0} Luminance to raise Glory", ChatMessageType.Broadcast);
                return;
            }
            else if (parameters[0] == "Temperance")
            {
                var costToRaise = GetLumForAttributeRaise(session.Player.LumAugCritReductionRating);
                ChatPacket.SendServerMessage(session, $"You require {costToRaise:N0} Luminance to raise Temperance", ChatMessageType.Broadcast);
                return;
            }
        }

        public static long GetXPForAttributeRaise(int level)
        {
            // Set initial cost and growth factor
            long baseCost = 10000000000; // 10 billion XP
            double growthRate = 1.00035; // Growth rate chosen to approximate 7,475 raises by level 10,000

            // Calculate the cost to raise an attribute at this level
            long xpCost = (long)(baseCost * Math.Pow(growthRate, level - 1));
            return xpCost;
        }

        public static long GetLumForAttributeRaise(int level)
        {
            // Set initial cost and growth factor
            long baseCost = 1200000; 
            double growthRate = 1.035; 

            // Calculate the cost to raise an attribute at this level
            long xpCost = (long)(baseCost * Math.Pow(growthRate, level - 1));
            return xpCost;
        }

        public static void HandleRecallFriend(ISession session, string parameters)
        {
            string playerName = parameters;

            if (playerName == null)
                return;

            InvitePlayer(session.Player, playerName);
            return;
        }

        public static void InvitePlayer(Player issuer, string playerName)
        {
            var player = PlayerManager.GetOnlinePlayer(playerName);
            string issuerName = issuer.Name;

            if (player == null)
            {
                issuer.Session.Network.EnqueueSend(new GameMessageSystemChat($"Player {playerName} was not found.", ChatMessageType.Broadcast));
                return;
            }

            player.SetProperty(PropertyBool.IsInvited, true);
            player.SetProperty(PropertyString.InviterName, issuerName);
            player.SaveBiotaToDatabase();
            issuer.IsInviting = true;

            return;
        }
    }
}
