﻿namespace Debugger.Tools.Information
{
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.ComponentModel.Composition;
    using System.Linq;
    using System.Text;

    using Ensage;
    using Ensage.SDK.Extensions;
    using Ensage.SDK.Menu;

    using Logger;

    using Menu;

    using SharpDX;

    internal class Items : IDebuggerTool
    {
        private readonly Player player;

        private MenuItem<bool> autoUpdate;

        private MenuItem<bool> information;

        private uint lastUnitInfo;

        [Import]
        private ILog log;

        [Import]
        private IMenu mainMenu;

        private MenuFactory menu;

        private MenuItem<bool> showBackpack;

        private MenuItem<bool> showBehavior;

        private MenuItem<bool> showCastRange;

        private MenuItem<bool> showInventory;

        private MenuItem<bool> showManaCost;

        private MenuItem<bool> showSpecialData;

        private MenuItem<bool> showStash;

        private MenuItem<bool> showTargetType;

        [ImportingConstructor]
        public Items()
        {
            this.player = ObjectManager.LocalPlayer;
        }

        public int LoadPriority { get; } = 80;

        public void Activate()
        {
            this.menu = this.mainMenu.InformationMenu.Menu("Items");

            this.information = this.menu.Item("Get", false);
            this.information.PropertyChanged += this.InformationOnPropertyChanged;

            this.autoUpdate = this.menu.Item("Auto update", false);
            this.autoUpdate.PropertyChanged += this.AutoUpdateOnPropertyChanged;

            this.showInventory = this.menu.Item("Show inventory items", true);
            this.showBackpack = this.menu.Item("Show backpack items", false);
            this.showStash = this.menu.Item("Show stash items", false);
            this.showManaCost = this.menu.Item("Show mana cost", false);
            this.showCastRange = this.menu.Item("Show cast range", false);
            this.showBehavior = this.menu.Item("Show behavior", false);
            this.showTargetType = this.menu.Item("Show target type", false);
            this.showSpecialData = this.menu.Item("Show all special data", false);

            this.AutoUpdateOnPropertyChanged(null, null);
        }

        public void Dispose()
        {
            this.information.PropertyChanged -= this.InformationOnPropertyChanged;
            this.autoUpdate.PropertyChanged -= this.AutoUpdateOnPropertyChanged;
            Game.OnFireEvent -= this.GameOnFireEvent;
        }

        private void AutoUpdateOnPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            if (this.autoUpdate)
            {
                this.menu.AddAsterisk();
                Game.OnFireEvent += this.GameOnFireEvent;
                this.InformationOnPropertyChanged(null, null);
            }
            else
            {
                this.menu.RemoveAsterisk();
                Game.OnFireEvent -= this.GameOnFireEvent;
            }
        }

        private void GameOnFireEvent(FireEventEventArgs args)
        {
            if (args.GameEvent.Name != "dota_player_update_selected_unit" && args.GameEvent.Name != "dota_player_update_query_unit")
            {
                return;
            }

            var unit = (this.player.QueryUnit ?? this.player.Selection.FirstOrDefault()) as Unit;
            if (unit?.IsValid != true)
            {
                return;
            }

            if (unit.Handle == this.lastUnitInfo)
            {
                return;
            }

            this.InformationOnPropertyChanged(null, null);
        }

        private void InformationOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var unit = (this.player.QueryUnit ?? this.player.Selection.FirstOrDefault()) as Unit;
            if (unit?.IsValid != true || !unit.HasInventory)
            {
                return;
            }

            this.lastUnitInfo = unit.Handle;

            var item = new LogItem(LogType.Item, Color.PaleGreen, "Items information");

            item.AddLine("Unit name: " + unit.Name, unit.Name);
            item.AddLine("Unit network name: " + unit.NetworkName, unit.NetworkName);
            item.AddLine("Unit classID: " + unit.ClassId, unit.ClassId);

            var items = new List<Item>();

            if (this.showInventory)
            {
                items.AddRange(unit.Inventory.Items);
            }

            if (this.showBackpack)
            {
                items.AddRange(unit.Inventory.Backpack);
            }

            if (this.showStash)
            {
                items.AddRange(unit.Inventory.Stash);
            }

            foreach (var ability in items.Reverse<Item>())
            {
                var abilityItem = new LogItem(LogType.Spell, Color.PaleGreen);

                abilityItem.AddLine("Name: " + ability.Name, ability.Name);
                abilityItem.AddLine("Network name: " + ability.NetworkName, ability.NetworkName);
                abilityItem.AddLine("ClassID: " + ability.ClassId, ability.ClassId);

                if (this.showManaCost)
                {
                    abilityItem.AddLine("Mana cost: " + ability.ManaCost, ability.ManaCost);
                }

                if (this.showCastRange)
                {
                    abilityItem.AddLine("Cast range: " + ability.GetCastRange(), ability.GetCastRange());
                }

                if (this.showBehavior)
                {
                    abilityItem.AddLine("Behavior: " + ability.AbilityBehavior, ability.AbilityBehavior);
                }

                if (this.showTargetType)
                {
                    abilityItem.AddLine("Target type: " + ability.TargetType, ability.TargetType);
                    abilityItem.AddLine("Target team type: " + ability.TargetTeamType, ability.TargetTeamType);
                }

                if (this.showSpecialData)
                {
                    abilityItem.AddLine("Special data =>");
                    foreach (var abilitySpecialData in ability.AbilitySpecialData.Where(x => !x.Name.StartsWith("#")))
                    {
                        var values = new StringBuilder();
                        var count = abilitySpecialData.Count;

                        for (uint i = 0; i < count; i++)
                        {
                            values.Append(abilitySpecialData.GetValue(i));
                            if (i < count - 1)
                            {
                                values.Append(", ");
                            }
                        }

                        abilityItem.AddLine("  " + abilitySpecialData.Name + ": " + values, abilitySpecialData.Name);
                    }
                }

                this.log.Display(abilityItem);
            }

            this.log.Display(item);
        }
    }
}