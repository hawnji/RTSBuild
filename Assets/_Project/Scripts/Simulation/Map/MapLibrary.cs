using System;
using System.Collections.Generic;

namespace RTS.Sim
{
    /// <summary>Original skirmish battlefields. 1v1 maps are point-symmetric; 2v2 maps are four-way mirrored.</summary>
    public static class MapLibrary
    {
        static List<MapSpec> _skirmish;

        static Vec2 V(float x, float y) => new Vec2(x, y);

        public static List<MapSpec> Skirmish
        {
            get
            {
                if (_skirmish == null) Build();
                return _skirmish;
            }
        }

        public static MapSpec Find(string id)
        {
            foreach (var m in Skirmish) if (m.Id == id) return m;
            return null;
        }

        static MapFeature F(FeatureKind k, float r, params Vec2[] pts) => MapFeature.Make(k, r, pts);

        static void Build()
        {
            _skirmish = new List<MapSpec>();

            // ---------------------------------------------------------- 1v1: Twin Rivers
            {
                var m = new MapSpec
                {
                    Id = "twin_rivers", Name = "Twin Rivers", MaxPlayers = 2, Width = 96, Height = 96, Theme = MapTheme.Temperate, Seed = 101,
                    Symmetry = Symmetry.Rotational2, ForestDensity = 0.09f, Roughness = 0.03f,
                    Description = "Two rivers split the valley. Three bridges and a shallow ford decide the flow of battle; a rich crystal field sits in no-man's-land."
                };
                var river = F(FeatureKind.River, 0, V(0, 30), V(20, 36), V(40, 44), V(56, 52), V(76, 60), V(96, 66));
                river.Width = 4; river.BridgeAt.AddRange(new[] { 2, 4 });
                m.Add(river);
                m.Add(new MapFeature { Kind = FeatureKind.Ford, Width = 3, Points = { V(66, 54), V(68, 60) } });
                m.Add(F(FeatureKind.OreField, 4.5f, V(22, 78)));
                m.Add(F(FeatureKind.OreField, 4f, V(30, 62)));
                m.Add(F(FeatureKind.OreField, 3.5f, V(8, 54)));
                m.Add(F(FeatureKind.CrystalField, 3.5f, V(48, 47)));
                m.Add(F(FeatureKind.Forest, 5, V(40, 70), V(14, 40)));
                m.Add(F(FeatureKind.Mountain, 3, V(36, 30)));
                m.Add(new MapFeature { Kind = FeatureKind.Road, Width = 1.5f, Points = { V(12, 84), V(30, 66), V(40, 44) } });
                m.Starts.Add(new Int2(12, 84)); m.Starts.Add(new Int2(83, 11));
                m.Neutrals.Add(new NeutralPlacement("tech_derrick", 30, 47));
                m.Neutrals.Add(new NeutralPlacement("tech_derrick", 64, 47));
                m.Neutrals.Add(new NeutralPlacement("civ_house", 44, 58)); m.Neutrals.Add(new NeutralPlacement("civ_barn", 50, 36));
                m.Neutrals.Add(new NeutralPlacement("civ_chapel", 20, 50)); m.Neutrals.Add(new NeutralPlacement("civ_chapel", 74, 44));
                _skirmish.Add(m);
            }
            // ---------------------------------------------------------- 1v1: Dust Basin
            {
                var m = new MapSpec
                {
                    Id = "dust_basin", Name = "Dust Basin", MaxPlayers = 2, Width = 104, Height = 84, Theme = MapTheme.Desert, Seed = 202,
                    Symmetry = Symmetry.Rotational2, ForestDensity = 0.0f, Roughness = 0.07f,
                    Description = "Elevated plateaus guard each base. The central basin holds the richest ore but is overlooked from every side."
                };
                var plat = F(FeatureKind.Plateau, 11, V(14, 20)); plat.Value = 1; plat.Ramps.Add(V(26, 22)); plat.Ramps.Add(V(14, 31));
                m.Add(plat);
                var plat2 = F(FeatureKind.Plateau, 7, V(52, 24)); plat2.Value = 1; plat2.Ramps.Add(V(52, 31)); plat2.Ramps.Add(V(45, 24));
                m.Add(plat2);
                m.Add(F(FeatureKind.OreField, 4f, V(30, 12)));
                m.Add(F(FeatureKind.OreField, 4.5f, V(40, 36)));
                m.Add(F(FeatureKind.CrystalField, 4f, V(52, 41)));
                m.Add(F(FeatureKind.Mountain, 3.5f, V(70, 30)));
                m.Add(F(FeatureKind.Patch, 6, V(36, 22)));
                m.Add(new MapFeature { Kind = FeatureKind.Road, Width = 1.5f, Points = { V(14, 20), V(34, 30), V(52, 41) } });
                m.Starts.Add(new Int2(14, 20)); m.Starts.Add(new Int2(89, 63));
                m.Neutrals.Add(new NeutralPlacement("tech_derrick", 28, 40));
                m.Neutrals.Add(new NeutralPlacement("tech_derrick", 74, 42));
                m.Neutrals.Add(new NeutralPlacement("civ_watertower", 52, 30)); m.Neutrals.Add(new NeutralPlacement("civ_house", 40, 18));
                _skirmish.Add(m);
            }
            // ---------------------------------------------------------- 1v1: Frostbite Pass
            {
                var m = new MapSpec
                {
                    Id = "frostbite_pass", Name = "Frostbite Pass", MaxPlayers = 2, Width = 88, Height = 112, Theme = MapTheme.Winter, Seed = 303,
                    Symmetry = Symmetry.Rotational2, ForestDensity = 0.07f, Roughness = 0.05f,
                    Description = "Mountain ridges funnel armies through three icy passes around a frozen lake. Flank routes reward the bold."
                };
                var r1 = new MapFeature { Kind = FeatureKind.Ridge, Width = 4, Points = { V(0, 40), V(28, 46), V(36, 48) } }; m.Add(r1);
                var r2 = new MapFeature { Kind = FeatureKind.Ridge, Width = 4, Points = { V(52, 50), V(72, 44), V(88, 42) } }; m.Add(r2);
                m.Add(F(FeatureKind.Lake, 6.5f, V(44, 56)));
                m.Add(F(FeatureKind.OreField, 4.5f, V(28, 18)));
                m.Add(F(FeatureKind.OreField, 4f, V(66, 30)));
                m.Add(F(FeatureKind.OreField, 3.5f, V(10, 54)));
                m.Add(F(FeatureKind.CrystalField, 3f, V(44, 40)));
                m.Add(F(FeatureKind.Forest, 5, V(16, 32), V(56, 22)));
                m.Starts.Add(new Int2(16, 12)); m.Starts.Add(new Int2(71, 99));
                m.Neutrals.Add(new NeutralPlacement("tech_hospital", 10, 64));
                m.Neutrals.Add(new NeutralPlacement("tech_derrick", 76, 46));
                m.Neutrals.Add(new NeutralPlacement("civ_house", 34, 30));
                _skirmish.Add(m);
            }
            // ---------------------------------------------------------- 1v1: Iron Strait (naval)
            {
                var m = new MapSpec
                {
                    Id = "iron_strait", Name = "Iron Strait", MaxPlayers = 2, Width = 112, Height = 96, Theme = MapTheme.Temperate, Seed = 404,
                    Symmetry = Symmetry.Rotational2, ForestDensity = 0.08f, Roughness = 0.03f,
                    Description = "A wide sea channel separates the coasts. A causeway and a ford offer land routes; navies rule the strait."
                };
                var sea = new MapFeature { Kind = FeatureKind.River, Width = 18, Points = { V(-4, 54), V(30, 50), V(56, 48), V(82, 46), V(116, 42) } };
                sea.BridgeAt.Add(2);
                m.Add(sea);
                m.Add(new MapFeature { Kind = FeatureKind.Ford, Width = 4, Points = { V(92, 38), V(94, 56) } });
                m.Add(F(FeatureKind.OreField, 4.5f, V(28, 14)));
                m.Add(F(FeatureKind.OreField, 4f, V(48, 28)));
                m.Add(F(FeatureKind.OreField, 3.5f, V(84, 20)));
                m.Add(F(FeatureKind.Forest, 5, V(64, 16), V(10, 30)));
                m.Starts.Add(new Int2(14, 16)); m.Starts.Add(new Int2(97, 79));
                m.Neutrals.Add(new NeutralPlacement("tech_relay", 70, 30));
                m.Neutrals.Add(new NeutralPlacement("civ_house", 40, 36)); m.Neutrals.Add(new NeutralPlacement("civ_watertower", 54, 36));
                _skirmish.Add(m);
            }
            // ---------------------------------------------------------- 2v2: Crossroads
            {
                var m = new MapSpec
                {
                    Id = "crossroads", Name = "Crossroads", MaxPlayers = 4, Width = 128, Height = 128, Theme = MapTheme.Temperate, Seed = 505,
                    Symmetry = Symmetry.Mirror4, ForestDensity = 0.08f, Roughness = 0.03f,
                    Description = "Four bases at the corners of a farming province. Highways meet at a ruined town in the center; flank roads wind through the woods."
                };
                m.Add(new MapFeature { Kind = FeatureKind.Road, Width = 2, Points = { V(16, 16), V(40, 40), V(64, 64) } });
                m.Add(new MapFeature { Kind = FeatureKind.Road, Width = 1.5f, Points = { V(16, 16), V(16, 64) } });
                m.Add(new MapFeature { Kind = FeatureKind.Road, Width = 1.5f, Points = { V(16, 16), V(64, 16) } });
                m.Add(F(FeatureKind.OreField, 4.5f, V(32, 12)));
                m.Add(F(FeatureKind.OreField, 4.5f, V(12, 34)));
                m.Add(F(FeatureKind.OreField, 4f, V(44, 52)));
                m.Add(F(FeatureKind.CrystalField, 3.5f, V(60, 60)));
                m.Add(F(FeatureKind.Lake, 5, V(36, 30)));
                m.Add(F(FeatureKind.Forest, 6, V(54, 30), V(30, 54)));
                m.Starts.Add(new Int2(16, 16)); m.Starts.Add(new Int2(16, 111)); m.Starts.Add(new Int2(111, 16)); m.Starts.Add(new Int2(111, 111));
                m.TeamA.AddRange(new[] { 0, 1 });
                m.Neutrals.Add(new NeutralPlacement("tech_derrick", 56, 62)); m.Neutrals.Add(new NeutralPlacement("tech_derrick", 70, 62));
                m.Neutrals.Add(new NeutralPlacement("civ_chapel", 62, 54)); m.Neutrals.Add(new NeutralPlacement("civ_house", 58, 70)); m.Neutrals.Add(new NeutralPlacement("civ_house", 68, 70));
                m.Neutrals.Add(new NeutralPlacement("tech_hospital", 62, 72));
                _skirmish.Add(m);
            }
            // ---------------------------------------------------------- 2v2: Scorched Plateau
            {
                var m = new MapSpec
                {
                    Id = "scorched_plateau", Name = "Scorched Plateau", MaxPlayers = 4, Width = 128, Height = 112, Theme = MapTheme.Desert, Seed = 606,
                    Symmetry = Symmetry.Mirror4, ForestDensity = 0.0f, Roughness = 0.06f,
                    Description = "Teams share a fortified plateau on each flank. Three corridors cross the scorched basin between them."
                };
                var plat = F(FeatureKind.Plateau, 13, V(20, 26)); plat.Value = 1; plat.Ramps.Add(V(34, 24)); plat.Ramps.Add(V(20, 40)); plat.Ramps.Add(V(30, 36));
                m.Add(plat);
                m.Add(F(FeatureKind.Mountain, 4, V(46, 40), V(60, 20)));
                m.Add(F(FeatureKind.OreField, 4f, V(12, 14)));
                m.Add(F(FeatureKind.OreField, 4.5f, V(44, 14)));
                m.Add(F(FeatureKind.OreField, 4f, V(34, 50)));
                m.Add(F(FeatureKind.CrystalField, 4f, V(62, 54)));
                m.Add(new MapFeature { Kind = FeatureKind.Road, Width = 1.5f, Points = { V(20, 26), V(40, 30), V(64, 30) } });
                m.Starts.Add(new Int2(20, 24)); m.Starts.Add(new Int2(20, 87)); m.Starts.Add(new Int2(107, 24)); m.Starts.Add(new Int2(107, 87));
                m.TeamA.AddRange(new[] { 0, 1 });
                m.Neutrals.Add(new NeutralPlacement("tech_derrick", 56, 44)); m.Neutrals.Add(new NeutralPlacement("tech_derrick", 70, 66));
                m.Neutrals.Add(new NeutralPlacement("tech_relay", 62, 30));
                _skirmish.Add(m);
            }
            // ---------------------------------------------------------- 2v2: Glacier Bay
            {
                var m = new MapSpec
                {
                    Id = "glacier_bay", Name = "Glacier Bay", MaxPlayers = 4, Width = 136, Height = 120, Theme = MapTheme.Winter, Seed = 707,
                    Symmetry = Symmetry.Mirror4, ForestDensity = 0.06f, Roughness = 0.04f,
                    Description = "A frozen bay opens to the sea at the heart of the map. Teams hold opposing shores linked by ice bridges and snowy ridgelines."
                };
                m.Add(F(FeatureKind.Lake, 16, V(68, 60)));
                m.Add(F(FeatureKind.Lake, 9, V(68, 34)));
                var ridge = new MapFeature { Kind = FeatureKind.Ridge, Width = 3, Points = { V(30, 44), V(44, 52) } }; m.Add(ridge);
                m.Add(new MapFeature { Kind = FeatureKind.Road, Width = 2, Points = { V(22, 20), V(46, 40), V(68, 40) } });
                m.Add(F(FeatureKind.OreField, 4.5f, V(10, 30)));
                m.Add(F(FeatureKind.OreField, 4.5f, V(36, 12)));
                m.Add(F(FeatureKind.OreField, 4f, V(48, 30)));
                m.Add(F(FeatureKind.CrystalField, 3.5f, V(22, 56)));
                m.Add(F(FeatureKind.Forest, 5, V(16, 46), V(52, 12)));
                m.Starts.Add(new Int2(18, 16)); m.Starts.Add(new Int2(18, 103)); m.Starts.Add(new Int2(117, 16)); m.Starts.Add(new Int2(117, 103));
                m.TeamA.AddRange(new[] { 0, 1 });
                m.Neutrals.Add(new NeutralPlacement("tech_derrick", 30, 58)); m.Neutrals.Add(new NeutralPlacement("tech_derrick", 104, 58));
                m.Neutrals.Add(new NeutralPlacement("civ_house", 40, 20)); m.Neutrals.Add(new NeutralPlacement("civ_house", 94, 20));
                _skirmish.Add(m);
            }
        }
    }
}
