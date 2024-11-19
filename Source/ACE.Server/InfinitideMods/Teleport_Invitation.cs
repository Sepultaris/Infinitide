using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ACE.Entity.Enum;
using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.WorldObjects;

namespace ACE.Server.InfinitideMods
{
    internal class Teleport_Invitation
    {
        public static void HandleTeleportInvitation(string issuer, Player player)
        {
            var issuerActual = PlayerManager.GetOnlinePlayer(issuer);

            if (player.IsInvited == false)
            {
                return;
            }
            if (issuerActual == null)
            {
                player.IsInvited = false;
                return;
            }

            var location = issuerActual.Location;
            var msg = $"{issuerActual.Name} has invited you to be teleported to their location. Would you like to be teleported?";

            if (!player.Session.Player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(player.Session.Player.Guid, () => player.Teleport(issuerActual.Location, false)), msg))
            {
                player.Session.Player.SendWeenieError(WeenieError.ConfirmationInProgress);
                issuerActual.SaveBiotaToDatabase();
            }
            player.IsInvited = false;

            return;
        }
    }
}
