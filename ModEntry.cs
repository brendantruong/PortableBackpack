using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Objects;
using SObject = StardewValley.Object;

namespace PortableBackpack
{
    internal sealed class ModEntry : Mod
    {
        private const string BackpackTypeKey = "Brendan.PortableBackpack.Type";
        private const string MiningType = "Mining";
        private const string CropType = "Crops";

        private ModConfig _config = new();

        private Chest? _placedMiningBackpack;
        private Chest? _placedCropBackpack;

        private bool _carryingMiningBackpack = true;
        private bool _carryingCropBackpack = true;

        private int _appliedSpeedPenalty = 0;

        public override void Entry(IModHelper helper)
        {
            _config = helper.ReadConfig<ModConfig>();

            helper.Events.Input.ButtonPressed += OnButtonPressed;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            if (e.Button == _config.MiningBackpackButton)
                HandleBackpackButton(MiningType);

            if (e.Button == _config.CropBackpackButton)
                HandleBackpackButton(CropType);
        }

        private void HandleBackpackButton(string type)
        {
            if (type == MiningType)
            {
                if (_carryingMiningBackpack)
                    PlaceBackpack(MiningType);
                else
                    PickUpBackpack(MiningType);

                return;
            }

            if (type == CropType)
            {
                if (_carryingCropBackpack)
                    PlaceBackpack(CropType);
                else
                    PickUpBackpack(CropType);
            }
        }

        private void PlaceBackpack(string type)
        {
            GameLocation location = Game1.currentLocation;
            Vector2 tile = GetTileInFrontOfPlayer();

            if (location.objects.ContainsKey(tile))
            {
                Game1.addHUDMessage(new HUDMessage("Something is already there.", HUDMessage.error_type));
                return;
            }

            Chest chest = new Chest(true);
            chest.modData[BackpackTypeKey] = type;

            location.objects.Add(tile, chest);

            if (type == MiningType)
            {
                _placedMiningBackpack = chest;
                _carryingMiningBackpack = false;
                Game1.addHUDMessage(new HUDMessage("Mining backpack placed.", HUDMessage.newQuest_type));
            }
            else
            {
                _placedCropBackpack = chest;
                _carryingCropBackpack = false;
                Game1.addHUDMessage(new HUDMessage("Crop backpack placed.", HUDMessage.newQuest_type));
            }
        }

        private void PickUpBackpack(string type)
        {
            GameLocation location = Game1.currentLocation;
            Vector2 tile = GetTileInFrontOfPlayer();

            if (!location.objects.TryGetValue(tile, out SObject? obj))
            {
                Game1.addHUDMessage(new HUDMessage($"Face your {type.ToLower()} backpack to pick it up.", HUDMessage.error_type));
                return;
            }

            if (obj is not Chest chest || !chest.modData.TryGetValue(BackpackTypeKey, out string? foundType) || foundType != type)
            {
                Game1.addHUDMessage(new HUDMessage($"That is not your {type.ToLower()} backpack.", HUDMessage.error_type));
                return;
            }

            if (chest.Items.Count > _config.MaxSlots)
            {
                Game1.addHUDMessage(new HUDMessage($"The {type.ToLower()} backpack can only hold {_config.MaxSlots} item slots.", HUDMessage.error_type));
                return;
            }

            location.objects.Remove(tile);

            if (type == MiningType)
            {
                _placedMiningBackpack = null;
                _carryingMiningBackpack = true;
                Game1.addHUDMessage(new HUDMessage("Mining backpack picked up.", HUDMessage.newQuest_type));
            }
            else
            {
                _placedCropBackpack = null;
                _carryingCropBackpack = true;
                Game1.addHUDMessage(new HUDMessage("Crop backpack picked up.", HUDMessage.newQuest_type));
            }
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            if (e.IsMultipleOf(30))
            {
                EnforceBackpackRules(_placedMiningBackpack, MiningType);
                EnforceBackpackRules(_placedCropBackpack, CropType);
            }

            UpdateSpeedPenalty();
        }

        private void EnforceBackpackRules(Chest? chest, string type)
        {
            if (chest == null)
                return;

            for (int i = chest.Items.Count - 1; i >= 0; i--)
            {
                Item item = chest.Items[i];

                bool allowed = type == MiningType
                    ? IsMiningItem(item)
                    : IsCropItem(item);

                if (!allowed)
                {
                    chest.Items.RemoveAt(i);

                    if (!Game1.player.addItemToInventoryBool(item))
                        Game1.createItemDebris(item, Game1.player.Position, Game1.player.FacingDirection, Game1.currentLocation);

                    Game1.addHUDMessage(new HUDMessage($"That item does not belong in the {type.ToLower()} backpack.", HUDMessage.error_type));
                }
            }

            while (chest.Items.Count > _config.MaxSlots)
            {
                Item item = chest.Items[chest.Items.Count - 1];
                chest.Items.RemoveAt(chest.Items.Count - 1);

                if (!Game1.player.addItemToInventoryBool(item))
                    Game1.createItemDebris(item, Game1.player.Position, Game1.player.FacingDirection, Game1.currentLocation);

                Game1.addHUDMessage(new HUDMessage($"The {type.ToLower()} backpack can only hold {_config.MaxSlots} item slots.", HUDMessage.error_type));
            }
        }

        private bool IsMiningItem(Item item)
        {
            if (item is not SObject obj)
                return false;

            return obj.Category == SObject.mineralsCategory
                || obj.Category == SObject.GemCategory
                || obj.Category == SObject.metalResources
                || obj.Category == SObject.buildingResources
                || obj.Name.ToLower().Contains("ore")
                || obj.Name.ToLower().Contains("coal")
                || obj.Name.ToLower().Contains("bar")
                || obj.Name.ToLower().Contains("geode");
        }

        private bool IsCropItem(Item item)
        {
            if (item is not SObject obj)
                return false;

            return obj.Category == SObject.VegetableCategory
                || obj.Category == SObject.FruitsCategory
                || obj.Category == SObject.flowersCategory
                || obj.Category == SObject.SeedsCategory
                || obj.Category == SObject.GreensCategory;
        }

        private void UpdateSpeedPenalty()
        {
            Game1.player.addedSpeed -= _appliedSpeedPenalty;

            int filledSlots = 0;

            if (_carryingMiningBackpack && _placedMiningBackpack != null)
                filledSlots += _placedMiningBackpack.Items.Count;

            if (_carryingCropBackpack && _placedCropBackpack != null)
                filledSlots += _placedCropBackpack.Items.Count;

            int penalty = 0;

            if (filledSlots >= 5)
                penalty = -1;

            if (filledSlots >= 10)
                penalty = -2;

            if (filledSlots >= 20)
                penalty = -3;

            if (filledSlots >= 30)
                penalty = -4;

            _appliedSpeedPenalty = penalty;
            Game1.player.addedSpeed += _appliedSpeedPenalty;
        }

        private Vector2 GetTileInFrontOfPlayer()
        {
            Vector2 tile = Game1.player.Tile;

            if (Game1.player.FacingDirection == 0)
                return tile + new Vector2(0, -1);

            if (Game1.player.FacingDirection == 1)
                return tile + new Vector2(1, 0);

            if (Game1.player.FacingDirection == 2)
                return tile + new Vector2(0, 1);

            if (Game1.player.FacingDirection == 3)
                return tile + new Vector2(-1, 0);

            return tile;
        }
    }

    internal sealed class ModConfig
    {
        public SButton MiningBackpackButton { get; set; } = SButton.M;
        public SButton CropBackpackButton { get; set; } = SButton.C;
        public int MaxSlots { get; set; } = 15;
    }
}