using ACE.DatLoader.FileTypes;
using ACE.Entity.Enum;
using ACE.Server.Command.Handlers.Processors;
using ACE.Server.Network;
using System;
using System.IO;
using System.Linq;

namespace ACE.Server.Command.Handlers
{
    internal class InfinitideContentRepoCommands
    {
        public enum FileType
        {
            Undefined,
            Encounter,
            LandblockInstance,
            Quest,
            Recipe,
            Spell,
            Weenie,
            Realm
        }

        [CommandHandler("pc", AccessLevel.Admin, CommandHandlerFlag.RequiresWorld, 0, ".")]
        [CommandHandler("pull_content", AccessLevel.Admin, CommandHandlerFlag.RequiresWorld, 1, ".")]

        public static void HandlePullContentCommand(ISession session, params string[] parameters)
        {
            if (parameters.Length > 0)
            {
                if (parameters[0].Equals("true"))
                {
                    UpdateRepo(session, true);
                }
                else if (parameters[0].Equals("false"))
                {
                    UpdateRepo(session, false);
                }
                else
                    ChatPacket.SendServerMessage(session, "Invalid parameters, values must be true or false", ChatMessageType.Broadcast);
            }
            else
                ChatPacket.SendServerMessage(session, "Invalid parameters, values must be true or false", ChatMessageType.Broadcast);
        }

        public static void UpdateRepo(ISession session, bool import)
        {
            var updater = new GitRepoUpdater(repoPath: @"C:\Infinitide\Server\Content", privateKeyPath: @"C:\Windows\System32\infinitide-content-updater");

            if (!updater.ValidateRepository(out var error))
            {
                ChatPacket.SendServerMessage(session, $"Repo validation failed: {error}", ChatMessageType.Broadcast);
                return;
            }

            if (updater.HasRemoteUpdates())
            {
                string[] updatedFiles = updater.GetUpdatedFiles();

                if (updatedFiles.Length < 0)
                {
                    ChatPacket.SendServerMessage(session, "Files that would be updated:", ChatMessageType.Broadcast);
                    foreach (var f in updatedFiles)
                    {
                        ChatPacket.SendServerMessage(session, f, ChatMessageType.Broadcast);
                    }
                }

                var pullResult = updater.Pull();
                if (pullResult.Success)
                {
                    ChatPacket.SendServerMessage(session, "Content updated successfully!", ChatMessageType.Broadcast);

                    if (import)
                    {
                        foreach (var file in updatedFiles)
                        {
                            if (file.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
                            {
                                var contentType = GetContentTypeFromPath(file);

                                if (contentType == FileType.Realm)
                                {
                                    CommandHandlerHelper.WriteOutputInfo(session, $"Realms may not be imported individually. Use /import-realms instead to fully reload the ruleset and realm files.");
                                    return;
                                }
                                if (contentType == FileType.Undefined)
                                {
                                    CommandHandlerHelper.WriteOutputInfo(session, $"Unknown content type.");
                                    return;
                                }

                                string fileName = Path.GetFileName(file); // "10000 ExampleWeenie.sql"
                                
                                string fileId = fileName.Split('.')[0];

                                switch (contentType)
                                {
                                    case FileType.LandblockInstance:
                                        {
                                            if (fileId != null)
                                            {
                                                DeveloperContentCommands.ImportSQLLandblock(session, fileId);
                                                break;
                                            }
                                            else
                                            {
                                                ChatPacket.SendServerMessage(session, "Failed to parse LandblockId from file name.", ChatMessageType.Broadcast);
                                                break;
                                            }
                                        }

                                    case FileType.Quest:
                                        {
                                            if (fileId != null)
                                            {
                                                DeveloperContentCommands.ImportSQLQuest(session, fileId);
                                                break;
                                            }
                                            else
                                            {
                                                ChatPacket.SendServerMessage(session, "Failed to parse QuestId from file name.", ChatMessageType.Broadcast);
                                                break;
                                            }
                                        }

                                    case FileType.Recipe:
                                        {
                                            string numberPart = fileName.Split(' ')[0];

                                            if (int.TryParse(numberPart, out int recipeId))
                                            {
                                                DeveloperContentCommands.ImportSQLRecipe(session, $"{recipeId}");
                                                break;
                                            }
                                            else
                                            {
                                                ChatPacket.SendServerMessage(session, "Failed to parse RecipeId from file name.", ChatMessageType.Broadcast);
                                                break;
                                            }
                                        }

                                    case FileType.Spell:
                                        {
                                            string numberPart = fileName.Split(' ')[0];

                                            if (int.TryParse(numberPart, out int spellId))
                                            {
                                                DeveloperContentCommands.ImportSQLSpell(session, $"{spellId}");
                                                break;
                                            }
                                            else
                                            {
                                                ChatPacket.SendServerMessage(session, "Failed to parse SpellId from file name.", ChatMessageType.Broadcast);
                                                break;
                                            }
                                        }

                                    case FileType.Weenie:
                                    {
                                        string numberPart = fileName.Split(' ')[0];

                                        if (int.TryParse(numberPart, out int weenieId))
                                        {
                                            DeveloperContentCommands.ImportSQLWeenieWrapped(session, $"{weenieId}", "C:\\Infinitide\\Server\\Content\\sql\\weenies");
                                            break;
                                        }
                                        else
                                        {
                                            ChatPacket.SendServerMessage(session, "Failed to parse WeenieId from file name.", ChatMessageType.Broadcast);
                                            break;
                                        }
                                    } 
                                }
                            }
                        }
                    }

                    var pullOutput = pullResult.Output;
                }
                else
                {
                    ChatPacket.SendServerMessage(session, $"Pull failed: {pullResult.Error}", ChatMessageType.Broadcast);
                }
            }
            else
            {
                ChatPacket.SendServerMessage(session, "Repo is already up to date.", ChatMessageType.Broadcast);
            }
        }

        public static FileType GetContentTypeFromPath(string fullFilePath)
        {
            if (string.IsNullOrWhiteSpace(fullFilePath))
                return FileType.Undefined;

            // Normalize path for safety
            string normalizedPath = fullFilePath.Replace('/', Path.DirectorySeparatorChar).ToLowerInvariant();

            if (normalizedPath.Contains($"{Path.DirectorySeparatorChar}weenies{Path.DirectorySeparatorChar}"))
                return FileType.Weenie;

            if (normalizedPath.Contains($"{Path.DirectorySeparatorChar}landblocks{Path.DirectorySeparatorChar}"))
                return FileType.LandblockInstance;

            if (normalizedPath.Contains($"{Path.DirectorySeparatorChar}quests{Path.DirectorySeparatorChar}"))
                return FileType.Quest;

            if (normalizedPath.Contains($"{Path.DirectorySeparatorChar}recipes{Path.DirectorySeparatorChar}"))
                return FileType.Recipe;

            if (normalizedPath.Contains($"{Path.DirectorySeparatorChar}spells{Path.DirectorySeparatorChar}"))
                return FileType.Spell;

            return FileType.Undefined;
        }
    }
}
