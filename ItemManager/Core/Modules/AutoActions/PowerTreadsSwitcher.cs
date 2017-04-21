﻿namespace ItemManager.Core.Modules.AutoActions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Abilities;

    using Attributes;

    using Ensage;
    using Ensage.Common.AbilityInfo;
    using Ensage.Common.Extensions;
    using Ensage.Common.Objects;
    using Ensage.Common.Objects.UtilityObjects;

    using EventArgs;

    using Interfaces;

    using Menus;
    using Menus.Modules.AutoActions.Actions;
    using Menus.Modules.Recovery;

    using Utils;

    using Attribute = Ensage.Attribute;

    [AbilityBasedModule(AbilityId.item_power_treads)]
    internal class PowerTreadsSwitcher : IAbilityBasedModule
    {
        private readonly List<string> activeDelaySwitchModifiers = new List<string>();

        private readonly Manager manager;

        private readonly PowerTreadsMenu menu;

        private readonly Order order;

        private readonly RecoveryMenu recoveryMenu;

        private readonly MultiSleeper sleeper = new MultiSleeper();

        private PowerTreads powerTreads;

        public PowerTreadsSwitcher(Manager manager, MenuManager menu)
        {
            this.manager = manager;
            this.menu = menu.AutoActionsMenu.PowerTreadsMenu;
            recoveryMenu = menu.RecoveryMenu;
            order = new Order();

            Refresh();

            foreach (var ability in manager.MyHero.Abilities.Where(x => x.GetManaCost(0) > 0))
            {
                this.menu.AddAbility(ability.StoredName(), true);
            }

            manager.OnAbilityAdd += OnAbilityAdd;
            manager.OnAbilityRemove += OnAbilityRemove;
            Player.OnExecuteOrder += OnExecuteOrder;
            Game.OnUpdate += OnUpdate;
            Unit.OnModifierAdded += OnModifierAdded;
            Unit.OnModifierRemoved += OnModifierRemoved;
        }

        public List<AbilityId> AbilityIds { get; } = new List<AbilityId>
        {
            AbilityId.item_power_treads
        };

        public void Dispose()
        {
            Player.OnExecuteOrder -= OnExecuteOrder;
            Game.OnUpdate -= OnUpdate;
            Unit.OnModifierAdded -= OnModifierAdded;
            Unit.OnModifierRemoved -= OnModifierRemoved;
            manager.OnAbilityAdd -= OnAbilityAdd;
            manager.OnAbilityRemove -= OnAbilityRemove;

            activeDelaySwitchModifiers.Clear();
        }

        public void Refresh()
        {
            powerTreads = manager.MyHero.UsableAbilities.FirstOrDefault(x => x.Id == AbilityIds.First()) as PowerTreads;
        }

        private Attribute GetAttribute(int menuIndex)
        {
            Attribute switchAttribute;
            switch (menuIndex)
            {
                case 0:
                {
                    switchAttribute = Attribute.Invalid;
                    break;
                }
                case 1:
                {
                    switchAttribute = manager.MyHero.Hero.PrimaryAttribute;
                    break;
                }
                default:
                {
                    switchAttribute = (Attribute)menuIndex - 2;
                    break;
                }
            }

            return switchAttribute;
        }

        private void OnAbilityAdd(object sender, AbilityEventArgs abilityEventArgs)
        {
            if (!abilityEventArgs.IsMine)
            {
                return;
            }

            if (abilityEventArgs.Ability.GetManaCost(0) > 0)
            {
                menu.AddAbility(abilityEventArgs.Ability.StoredName(), true);
            }
        }

        private void OnAbilityRemove(object sender, AbilityEventArgs abilityEventArgs)
        {
            if (!abilityEventArgs.IsMine)
            {
                return;
            }

            if (abilityEventArgs.Ability.GetManaCost(0) > 0)
            {
                menu.RemoveAbility(abilityEventArgs.Ability.StoredName());
            }
        }

        private void OnExecuteOrder(Player sender, ExecuteOrderEventArgs args)
        {
            if (!menu.IsEnabled || !args.Process || args.IsQueued || !args.Entities.Contains(manager.MyHero.Hero)
                || manager.MyHero.IsInvisible())
            {
                return;
            }

            var ability = args.Ability;

            if (!args.IsPlayerInput)
            {
                if (ability?.Id == AbilityIds.First())
                {
                    powerTreads.SetSleep(300);
                    return;
                }

                if (!menu.UniversalUseEnabled)
                {
                    return;
                }
            }
            else if (ability?.Id == AbilityIds.First())
            {
                powerTreads.ChangeDefaultAttribute();
                return;
            }

            var sleep = Math.Max(ability.FindCastPoint() * 1000, 100) + 200;

            if (!powerTreads.CanBeCasted())
            {
                if (ability == null)
                {
                    return;
                }

                sleeper.Sleep((float)sleep, AbilityIds.First());

                if (powerTreads.ActiveAttribute == Attribute.Intelligence)
                {
                    powerTreads.SetSleep((float)sleep);
                }

                return;
            }

            if (ability != null && (ability.ManaCost <= menu.MpAbilityThreshold
                                    || !menu.IsAbilityEnabled(ability.StoredName())))
            {
                return;
            }

            switch (args.OrderId)
            {
                case OrderId.MoveLocation:
                case OrderId.MoveTarget:
                {
                    var switchAttribute = GetAttribute(menu.SwitchOnMoveAttribute);

                    if (switchAttribute == Attribute.Invalid || powerTreads.ActiveAttribute == switchAttribute
                        || activeDelaySwitchModifiers.Any())
                    {
                        return;
                    }

                    powerTreads.SwitchTo(switchAttribute);
                    powerTreads.ChangeDefaultAttribute(switchAttribute);
                    sleeper.Sleep(300, AbilityIds.First());

                    return;
                }
                case OrderId.AttackLocation:
                case OrderId.AttackTarget:
                {
                    var switchAttribute = GetAttribute(menu.SwitchOnAttackAttribute);

                    if (switchAttribute == Attribute.Invalid || powerTreads.ActiveAttribute == switchAttribute
                        || activeDelaySwitchModifiers.Any())
                    {
                        return;
                    }

                    powerTreads.SwitchTo(switchAttribute);
                    powerTreads.ChangeDefaultAttribute(switchAttribute);
                    sleeper.Sleep(300, AbilityIds.First());

                    return;
                }
                case OrderId.AbilityTarget:
                {
                    var target = args.Target as Unit;
                    if (target != null && target.IsValid && target.IsAlive)
                    {
                        if (manager.MyHero.Distance2D(target) <= ability.GetCastRange() + 300)
                        {
                            powerTreads.SwitchTo(Attribute.Intelligence);
                            order.UseAbility(ability, target);
                            sleep += manager.MyHero.Hero.GetTurnTime(target) * 1000;
                            args.Process = false;
                        }
                    }
                    break;
                }
                case OrderId.AbilityLocation:
                {
                    var targetLocation = args.TargetPosition;
                    if (manager.MyHero.Distance2D(targetLocation) <= ability.GetCastRange() + 300
                        || !AbilityDatabase.Find(ability.StoredName()).FakeCastRange)
                    {
                        powerTreads.SwitchTo(Attribute.Intelligence);
                        order.UseAbility(ability, targetLocation);
                        sleep += manager.MyHero.Hero.GetTurnTime(targetLocation) * 1000;
                        args.Process = false;
                    }
                    break;
                }
                case OrderId.Ability:
                {
                    powerTreads.SwitchTo(Attribute.Intelligence);
                    order.UseAbility(ability);
                    args.Process = false;
                    break;
                }
                case OrderId.ToggleAbility:
                {
                    powerTreads.SwitchTo(Attribute.Intelligence);
                    order.ToggleAbility(ability);
                    args.Process = false;
                    break;
                }
                default:
                {
                    return;
                }
            }

            if (!args.Process)
            {
                sleeper.Sleep((float)sleep, AbilityIds.First());
            }
        }

        private void OnModifierAdded(Unit sender, ModifierChangedEventArgs args)
        {
            if (sender.Handle != manager.MyHero.Handle)
            {
                return;
            }

            var modifier = ModifierUtils.DelayPowerTreadsSwitchModifiers.FirstOrDefault(x => x == args.Modifier.Name);
            if (!string.IsNullOrEmpty(modifier))
            {
                activeDelaySwitchModifiers.Add(modifier);

                if (sleeper.Sleeping(AbilityIds.First()) || recoveryMenu.IsActive)
                {
                    return;
                }

                switch (modifier)
                {
                    case "modifier_item_urn_heal":
                    case "modifier_flask_healing":
                    case "modifier_filler_heal":
                    case "modifier_bottle_regeneration":
                    {
                        powerTreads.SwitchTo(Attribute.Agility);
                        break;
                    }
                }
            }
        }

        private void OnModifierRemoved(Unit sender, ModifierChangedEventArgs args)
        {
            if (sender.Handle != manager.MyHero.Handle)
            {
                return;
            }

            var modifier = ModifierUtils.DelayPowerTreadsSwitchModifiers.FirstOrDefault(x => x == args.Modifier.Name);
            if (!string.IsNullOrEmpty(modifier))
            {
                activeDelaySwitchModifiers.Remove(modifier);
            }
        }

        private void OnUpdate(EventArgs args)
        {
            if (sleeper.Sleeping(this) || Game.IsPaused)
            {
                return;
            }

            sleeper.Sleep(200, this);

            if (!menu.IsEnabled || sleeper.Sleeping(AbilityIds.First()) || !powerTreads.CanBeCasted()
                || !manager.MyHero.CanUseItems() || powerTreads.ActiveAttribute == powerTreads.DefaultAttribute
                || activeDelaySwitchModifiers.Any())
            {
                return;
            }

            powerTreads.SwitchTo(powerTreads.DefaultAttribute);
            sleeper.Sleep(300, this);
        }
    }
}