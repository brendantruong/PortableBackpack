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
        private const string BackpackKey = "YourProjectName.Backpack";

        private ModConfig _config = new ModConfig();

        private bool _isCarryingBackpack = true;
        private Chest? _placedBackpack = null;
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

            if (e.Button != _config.BackpackButton)
                return;

            if (_isCarryingBackpack)
                PlaceBackpack();
            else
                PickUpBackpack();
        }

        private void PlaceBackpack()
        {
            GameLocation location = Game1.currentLocation;
            Vector2 tile = GetTileInFrontOfPlayer();

            if (location.objects.ContainsKey(tile))
            {
                Game1.addHUDMessage(new HUDMessage("Something is already there.", HUDMessage.error_type));
                return;
            }

            Chest chest = new Chest(true);
            chest.modData[BackpackKey] = "true";

            location.objects.Add(tile, chest);

            _placedBackpack = chest;
            _isCarryingBackpack = false;

            Game1.addHUDMessage(new HUDMessage("Backpack placed.", HUDMessage.newQuest_type));
        }

        private void PickUpBackpack()
        {
            GameLocation location = Game1.currentLocation;
            Vector2 tile = GetTileInFrontOfPlayer();

            if (!location.objects.TryGetValue(tile, out SObject? obj))
            {
                Game1.addHUDMessage(new HUDMessage("Face your backpack to pick it up.", HUDMessage.error_type));
                return;
            }

            if (obj is not Chest chest || !chest.modData.ContainsKey(BackpackKey))
            {
                Game1.addHUDMessage(new HUDMessage("That is not your backpack.", HUDMessage.error_type));
                return;
            }

            if (chest.Items.Count > _config.MaxSlots)
            {
                Game1.addHUDMessage(new HUDMessage("Backpack is too full.", HUDMessage.error_type));
                return;
            }

            location.objects.Remove(tile);

            _placedBackpack = null;
            _isCarryingBackpack = true;

            Game1.addHUDMessage(new HUDMessage("Backpack picked up.", HUDMessage.newQuest_type));
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            EnforceBackpackLimit();
            UpdateSpeedPenalty();
        }

        private void EnforceBackpackLimit()
        {
            if (_placedBackpack == null)
                return;

            while (_placedBackpack.Items.Count > _config.MaxSlots)
            {
                _placedBackpack.Items.RemoveAt(_placedBackpack.Items.Count - 1);
                Game1.addHUDMessage(new HUDMessage("Backpack can only hold 15 item slots.", HUDMessage.error_type));
            }
        }

        private void UpdateSpeedPenalty()
        {
            Game1.player.addedSpeed -= _appliedSpeedPenalty;

            int filledSlots = 0;

            if (_placedBackpack != null)
                filledSlots = _placedBackpack.Items.Count;

            int penalty = 0;

            if (filledSlots >= 5)
                penalty = -1;
            if (filledSlots >= 10)
                penalty = -2;
            if (filledSlots >= 15)
                penalty = -3;

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
        public SButton BackpackButton { get; set; } = SButton.B;
        public int MaxSlots { get; set; } = 15;
    }
}