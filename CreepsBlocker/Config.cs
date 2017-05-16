﻿namespace CreepsBlocker
{
    using System;

    using Ensage.Common.Menu;
    using Ensage.SDK.Menu;

    internal class Config : IDisposable
    {
        private readonly MenuFactory factory;

        public Config()
        {
            factory = MenuFactory.Create("Creeps Blocker");
            Key = factory.Item("Hotkey", new KeyBind('M'));
            BlockRangedCreep = factory.Item("Block ranged creep", true);
        }

        public MenuItem<bool> BlockRangedCreep { get; }

        public MenuItem<KeyBind> Key { get; }

        public void Dispose()
        {
            factory?.Dispose();
        }
    }
}