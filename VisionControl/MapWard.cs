﻿using System;
using System.Collections.Generic;
using System.Linq;
using Ensage;
using Ensage.Common.Extensions;
using SharpDX;

namespace VisionControl {
    internal class MapWard {
        public static readonly List<MapWard> MapWards = new List<MapWard>();

        public MapWard(ClassID ward, Entity hero) {
            var texture = "materials/ensage_ui/items/";

            switch (ward) {
                case ClassID.CDOTA_NPC_Observer_Ward: {
                    EndTime = Game.GameTime + 360;
                    texture += "ward_observer.vmat";
                    break;
                }
                case ClassID.CDOTA_NPC_Observer_Ward_TrueSight: {
                    EndTime = Game.GameTime + 240;
                    texture += "ward_sentry.vmat";
                    break;
                }
            }

            WardType = ward;
            Show = true;
            Texture = Drawing.GetTexture(texture);
            Position = new Vector3(hero.Position.X + 300 * (float) Math.Cos(hero.RotationRad),
                hero.Position.Y + 300 * (float) Math.Sin(hero.RotationRad),
                hero.Position.Z);
        }

        public MapWard(Entity ward) {
            var texture = "materials/ensage_ui/items/";

            switch (ward.ClassID) {
                case ClassID.CDOTA_NPC_Observer_Ward: {
                    EndTime = Game.GameTime + 360;
                    texture += "ward_observer.vmat";
                    break;
                }
                case ClassID.CDOTA_NPC_Observer_Ward_TrueSight: {
                    EndTime = Game.GameTime + 240;
                    texture += "ward_sentry.vmat";
                    break;
                }
            }

            WardType = ward.ClassID;
            IsKnown = true;
            Ward = ward;
            Show = true;
            Texture = Drawing.GetTexture(texture);
            Position = ward.Position;
        }

        public DotaTexture Texture { get; private set; }

        public bool IsKnown { get; set; }

        public Vector3 Position { get; set; }

        public Entity Ward { get; set; }

        public ClassID WardType { get; private set; }

        public float EndTime { get; private set; }

        public bool IsVisible { get; set; }

        public bool Show { get; set; }

        public static void Clear() {
            MapWards.Clear();
        }

        public static void Add(MapWard mapWard) {
            MapWards.Add(mapWard);
        }

        public static void Update() {
            if (!Program.IsEnabled)
                return;

            var enemyWards =
                ObjectManager.GetEntities<Entity>()
                    .Where(
                        x =>
                            x.IsAlive && x.IsVisible && x.Team == Program.Hero.GetEnemyTeam() &&
                            (x.ClassID == ClassID.CDOTA_NPC_Observer_Ward ||
                             x.ClassID == ClassID.CDOTA_NPC_Observer_Ward_TrueSight));

            foreach (var ward in enemyWards) {
                if (MapWards.Find(x => x.Ward != null && x.Ward.Equals(ward)) != null)
                    continue;

                var unknownWard =
                    MapWards.Where(x => !x.IsKnown && x.Position.Distance2D(ward) <= 500)
                        .OrderBy(x => x.Position.Distance2D(ward))
                        .FirstOrDefault();

                if (unknownWard == null) {
                    MapWards.Add(new MapWard(ward));
                    continue;
                }

                unknownWard.Position = ward.Position;
                unknownWard.Ward = ward;
                unknownWard.IsKnown = true;
            }

            if (Program.SmartHide)
                foreach (var ward in MapWards.Where(x => x.IsKnown))
                    ward.Show = !ward.Ward.IsVisible;

            var removeWard =
                MapWards.FirstOrDefault(
                    x => (x.IsKnown && x.Ward != null && !x.Ward.IsAlive) || Game.GameTime > x.EndTime);

            if (removeWard != null)
                MapWards.Remove(removeWard);
        }
    }
}