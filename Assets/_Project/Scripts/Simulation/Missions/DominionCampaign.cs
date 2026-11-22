using System;
using System.Collections.Generic;
using static RTS.Sim.MissionKit;

namespace RTS.Sim
{
    /// <summary>The Iron Dominion campaign: twelve operations from the opening offensive to the siege of Meridian.</summary>
    public static class DominionCampaign
    {
        static Vec2 V(float x, float y) => new Vec2(x, y);
        const FactionId CA = FactionId.Alliance, ID = FactionId.Dominion;

        public static List<MissionDef> Build()
        {
            var list = new List<MissionDef>();

            // ============================================================ 1. Steel Dawn — opening offensive
            {
                var map = Map("id01", "Kesh Border", 96, 88, MapTheme.Temperate, 3101, 0.08f);
                map.Add(Road(V(48, 8), V(48, 44), V(40, 80))).Add(Feat(FeatureKind.OreField, 4.5f, V(30, 20))).Add(Feat(FeatureKind.OreField, 4, V(62, 70)));
                map.Add(River(3, new[] { 1, 3 }, V(0, 40), V(30, 42), V(60, 38), V(80, 44), V(96, 42)));
                map.Add(Feat(FeatureKind.Forest, 6, V(14, 64), V(80, 18)));
                map.Starts.Add(new Int2(48, 10)); map.Starts.Add(new Int2(44, 76));
                list.Add(new MissionDef
                {
                    Id = "id01", Faction = ID, Index = 1, Title = "Steel Dawn", Location = "Kesh border, Halvorsen sector", Date = "9 March 1994",
                    Map = map, StartingCredits = 4000, EnemyFaction = CA, Music = "dominion",
                    Briefing = "Comrade Commander. History begins today.\n\nThe southern republics hoard the aurite that belongs to the industry of all Veldmark. At dawn, our artillery opens the way. " +
                               "Lead the 7th Shock Group across the border and annihilate the Alliance border garrison.\n\nA Foundry Crawler will follow once the river crossing is secure. Then build, and finish them.",
                    VictoryText = "The border garrison is ash. The road south lies open. The Marshal-Chancellor is pleased.",
                    Tags = { "large-scale offensive", "establish base" },
                    Players = m => { m.AddHuman(); m.AddAI("enemy", "Alliance Border Garrison", CA, 1, builds: false, attacks: false); m.Human.MaxTechTier = 2; },
                    Populate = m =>
                    {
                        var e = m.P("enemy");
                        m.SpawnGroup(m.Human, 46, 12, null, "id_ravager", "id_ravager", "id_ravager", "id_jackal", "id_levy", "id_levy", "id_levy", "id_levy", "id_levy", "id_levy", "id_breacher", "id_breacher", "id_hailstorm");
                        m.BuildBase(e, 44, 74, BasePreset.Small, "garrison");
                        m.Build(e, "ca_mgbunker", 44, 46, "bridgeguard"); m.Build(e, "ca_mgbunker", 52, 46, "bridgeguard");
                        m.SpawnGroup(e, 48, 48, "bridgeguard", "ca_rifleman", "ca_rifleman", "ca_rifleman", "ca_lancer");
                        m.SpawnGroup(e, 44, 66, "garrison_troops", "ca_rifleman", "ca_rifleman", "ca_rifleman", "ca_lancer", "ca_sabre", "ca_bulwark");
                    },
                    Script = m =>
                    {
                        var e = m.P("enemy");
                        m.Zone("crossing", 48, 42, 6);
                        m.AddObjective("cross", "Secure the river crossing.");
                        m.AddObjective("garrison", "Destroy the Alliance border garrison.");
                        m.At(2, "intro", () => m.Say("Marshal Strand", "For the foundries. For the Dominion. Advance!"));
                        m.When("crossed", () => m.CountInZone("crossing", m.Human) >= 3 && m.AllDead("bridgeguard"), () =>
                        {
                            m.Complete("cross");
                            m.Say("Commissar Volk", "Crossing secure. The Foundry Crawler is coming forward. Build, Commander.");
                            m.Reinforce(m.Human, 48, 3, 48, 30, null, "id_foundry", "id_levy", "id_levy", "id_sapper");
                            m.GiveCredits(m.Human, 4000);
                        });
                        m.When("garrison", () => m.CountBuildings(e) == 0, () => m.Complete("garrison"));
                        m.At(360, "counter", () => m.Attack(e, 44, 84, 48, 30, "counter", Scale(m, "ca_sabre", "ca_sabre", "ca_rifleman", "ca_rifleman", "ca_lancer")));
                    }
                });
            }

            // ============================================================ 2. The Foundry — establish base and economy
            {
                var map = Map("id02", "Ostrava Fields", 104, 96, MapTheme.Wasteland, 3202, 0.05f, 0.04f);
                map.Add(Feat(FeatureKind.OreField, 5, V(24, 22))).Add(Feat(FeatureKind.OreField, 4.5f, V(52, 36))).Add(Feat(FeatureKind.CrystalField, 4, V(80, 30))).Add(Feat(FeatureKind.OreField, 4.5f, V(30, 64)));
                map.Add(Feat(FeatureKind.Lake, 6, V(66, 62))).Add(Road(V(16, 14), V(52, 36), V(88, 84)));
                map.Starts.Add(new Int2(16, 14)); map.Starts.Add(new Int2(88, 84));
                map.Neutrals.Add(new NeutralPlacement("tech_derrick", 50, 56, "derrick"));
                list.Add(new MissionDef
                {
                    Id = "id02", Faction = ID, Index = 2, Title = "The Foundry", Location = "Ostrava ore fields", Date = "18 March 1994",
                    Map = map, StartingCredits = 5000, EnemyFaction = CA, Music = "dominion",
                    Briefing = "The offensive needs steel, and steel needs ore. The Ostrava fields are rich, and the Alliance knows it.\n\n" +
                               "Build a working base and harvest 10,000 credits of ore to fund the southern front. Alliance raiding parties will try to stop you. Protect your harvesters.",
                    VictoryText = "The ore flows north. The foundries roar. Well done, Commander.",
                    Tags = { "establish base", "economy" },
                    Players = m => { m.AddHuman(); m.AddAI("enemy", "Alliance 5th Rangers", CA, 1, builds: true, attacks: false); m.AddNeutral(); m.Human.MaxTechTier = 2; },
                    Populate = m =>
                    {
                        m.Spawn(m.Human, "id_foundry", 16, 14);
                        m.SpawnGroup(m.Human, 20, 18, null, "id_ravager", "id_jackal", "id_levy", "id_levy", "id_levy", "id_breacher", "id_sapper");
                        m.BuildBase(m.P("enemy"), 88, 84, BasePreset.Small, "ebase");
                    },
                    Script = m =>
                    {
                        var e = m.P("enemy");
                        m.AddObjective("harvest", "Harvest 10,000 credits of ore.");
                        m.AddObjective("derrick", "Capture the ore derrick.", primary: false);
                        m.AddObjective("ebase", "Destroy the Alliance raider camp.", primary: false);
                        m.Every(5, "progress", () => true, () => { if (m.Human.Stats.CreditsHarvested >= 10000) m.Complete("harvest"); });
                        m.When("derrick", () => m.OwnedBy("derrick", m.Human), () => m.Complete("derrick"));
                        m.When("ebase", () => m.CountBuildings(e) == 0, () => m.Complete("ebase"));
                        Waves(m, "raid", e, 200, 110, 8, new[] { V(96, 40), V(60, 92), V(96, 70) }, V(30, 24),
                            i => i < 3 ? new[] { "ca_wasp", "ca_wasp", "ca_rifleman", "ca_lancer" } : new[] { "ca_sabre", "ca_wasp", "ca_rifleman", "ca_rifleman", "ca_lancer" });
                        m.At(5, "hint", () => m.Hint("Ore Smelters come with a free Ore Hauler. Build Ore Bunkers if your credits reach storage capacity."));
                    }
                });
            }

            // ============================================================ 3. Crossing the Varn — capture strategic structures
            {
                var map = Map("id03", "Varn Lowlands", 112, 96, MapTheme.Temperate, 3303, 0.09f);
                map.Add(River(5, new[] { 1, 3 }, V(0, 48), V(28, 50), V(56, 46), V(84, 50), V(112, 48)));
                map.Add(Feat(FeatureKind.OreField, 4.5f, V(26, 20))).Add(Feat(FeatureKind.OreField, 4, V(80, 22))).Add(Feat(FeatureKind.OreField, 4, V(54, 74)));
                map.Add(Feat(FeatureKind.Forest, 6, V(14, 78), V(96, 76)));
                map.Starts.Add(new Int2(54, 12)); map.Starts.Add(new Int2(54, 86));
                map.Neutrals.Add(new NeutralPlacement("tech_relay", 20, 62, "relay")); map.Neutrals.Add(new NeutralPlacement("tech_relay", 54, 60, "relay")); map.Neutrals.Add(new NeutralPlacement("tech_relay", 90, 62, "relay"));
                list.Add(new MissionDef
                {
                    Id = "id03", Faction = ID, Index = 3, Title = "Crossing the Varn", Location = "Varn lowlands", Date = "30 March 1994",
                    Map = map, StartingCredits = 8000, EnemyFaction = CA, Music = "battle",
                    Briefing = "Three communications relays on the north bank of the Varn control the Alliance's entire signals network in the region.\n\n" +
                               "Capture all three with Combat Sappers. Once we hold them, the Alliance in this sector will be deaf and blind.",
                    VictoryText = "The relays are ours. Alliance commanders shout into dead radios.",
                    Tags = { "capture strategic structures" },
                    Players = m => { m.AddHuman(); m.AddAI("enemy", "Alliance Varn Command", CA, 1, builds: true, attacks: true, aggression: 0.7f); m.AddNeutral(); m.Human.MaxTechTier = 2; },
                    Populate = m =>
                    {
                        var e = m.P("enemy");
                        m.BuildBase(m.Human, 54, 12, BasePreset.Small, "base");
                        m.SpawnGroup(m.Human, 56, 22, null, "id_ravager", "id_ravager", "id_levy", "id_levy", "id_levy", "id_breacher", "id_sapper", "id_sapper", "id_sapper");
                        m.BuildBase(e, 54, 86, BasePreset.Medium, "ebase");
                        m.SpawnGroup(e, 20, 58, "relayguard", "ca_rifleman", "ca_rifleman", "ca_lancer");
                        m.SpawnGroup(e, 54, 56, "relayguard", "ca_rifleman", "ca_bulwark", "ca_sabre");
                        m.SpawnGroup(e, 90, 58, "relayguard", "ca_rifleman", "ca_rifleman", "ca_lancer");
                        m.Build(e, "ca_mgbunker", 52, 58);
                    },
                    Script = m =>
                    {
                        var e = m.P("enemy");
                        m.AddObjective("relays", "Capture all three communications relays.");
                        m.AddObjective("ebase", "Destroy Alliance Varn Command.", primary: false);
                        m.When("relays", () => m.OwnedBy("relay", m.Human), () => m.Complete("relays"));
                        m.When("relay_lost", () => m.CountTagged("relay") < 3, () => { m.Fail("relays"); });
                        m.When("ebase", () => m.CountBuildings(e) == 0, () => m.Complete("ebase"));
                        m.At(3, "intro", () => m.Say("Commissar Volk", "Do not destroy the relays. Capture them. Sappers walk in; the buildings are ours."));
                    }
                });
            }

            // ============================================================ 4. Night Raid — infiltrate enemy territory
            {
                var map = Map("id04", "Corvin Heights", 96, 104, MapTheme.Winter, 3404, 0.12f, 0.04f);
                map.Add(Feat(FeatureKind.Clearing, 5, V(12, 10))).Add(Road(V(60, 50), V(60, 96))).Add(Feat(FeatureKind.Lake, 5, V(30, 60)));
                map.Add(Ridge(3, V(0, 36), V(40, 40))).Add(Feat(FeatureKind.OreField, 4, V(70, 70)));
                map.Starts.Add(new Int2(12, 10)); map.Starts.Add(new Int2(62, 82));
                list.Add(new MissionDef
                {
                    Id = "id04", Faction = ID, Index = 4, Title = "Night Raid", Location = "Corvin Heights", Date = "12 April 1994", NoBase = true,
                    Map = map, StartingCredits = 0, EnemyFaction = CA, Music = "tension",
                    Briefing = "The Alliance radar array on Corvin Heights watches our bomber routes. Tonight it goes dark.\n\n" +
                               "A Black Guard Commando and a sapper team will infiltrate the heights. Destroy the Radar Array and all four Fusion Generators. The commando's demolition charges level any structure; your sappers can capture an enemy barracks for reinforcements.\n\n" +
                               "Sentry Guns detect intruders. Avoid them where you can.",
                    VictoryText = "Corvin Heights is dark. Our bombers fly unseen.",
                    Tags = { "infiltration", "commando" },
                    Players = m => { m.AddHuman(); m.AddAI("enemy", "Corvin Garrison", CA, 1, builds: false, attacks: false); },
                    Populate = m =>
                    {
                        var e = m.P("enemy");
                        m.SpawnGroup(m.Human, 12, 10, null, "id_blackguard", "id_sapper", "id_sapper", "id_hunter", "id_levy", "id_levy", "id_surgeon");
                        m.Build(e, "ca_radar", 62, 84, "radar");
                        m.Build(e, "ca_power", 54, 78, "power"); m.Build(e, "ca_power", 70, 78, "power"); m.Build(e, "ca_power", 54, 90, "power"); m.Build(e, "ca_power", 70, 90, "power");
                        m.Build(e, "ca_barracks", 40, 70, "barracks");
                        m.Build(e, "ca_aiturret", 60, 74); m.Build(e, "ca_mgbunker", 66, 74); m.Build(e, "ca_aiturret", 48, 84); m.Build(e, "ca_mgbunker", 76, 84);
                        m.SpawnGroup(e, 60, 70, "guard", "ca_rifleman", "ca_rifleman", "ca_rifleman", "ca_bulwark");
                        m.SpawnGroup(e, 36, 44, "patrol", "ca_rifleman", "ca_rifleman", "ca_wasp");
                        m.SpawnGroup(e, 80, 60, "patrol2", "ca_rifleman", "ca_lancer", "ca_pathfinder");
                    },
                    Script = m =>
                    {
                        var e = m.P("enemy");
                        m.AddObjective("radar", "Destroy the Radar Array.");
                        m.AddObjective("power", "Destroy all four Fusion Generators.");
                        m.AddObjective("barracks", "Capture the Alliance Training Camp.", primary: false);
                        foreach (var u in m.Tagged("patrol")) m.W.Commands.Patrol(new[] { u.Id }, u.Owner, new Vec2(10, 50));
                        foreach (var u in m.Tagged("patrol2")) m.W.Commands.Patrol(new[] { u.Id }, u.Owner, new Vec2(84, 30));
                        m.At(2, "intro", () => m.Say("Black Guard", "In position. Going quiet."));
                        m.When("radar", () => m.AllDead("radar"), () => m.Complete("radar"));
                        m.When("power", () => m.AllDead("power"), () => m.Complete("power"));
                        m.When("barracks", () => m.OwnedBy("barracks", m.Human), () =>
                        {
                            m.Complete("barracks"); m.GiveCredits(m.Human, 3000);
                            m.Say("Commissar Volk", "The camp is ours. Train what you need — 3,000 credits have been made available.");
                        });
                        m.When("alarm", () => m.AllDead("radar") || m.CountTagged("power") < 4, () =>
                        {
                            m.Say("Radio Intercept", "(Alliance) ...Corvin under attack! All units respond!");
                            m.Attack(e, 90, 100, 60, 80, "response", Scale(m, "ca_sabre", "ca_rifleman", "ca_rifleman", "ca_lancer", "ca_wasp"));
                        });
                    }
                });
            }

            // ============================================================ 5. Kettle Pass — survive assault waves
            {
                var map = Map("id05", "Kettle Pass", 96, 112, MapTheme.Winter, 3505, 0.07f, 0.05f);
                map.Add(Ridge(6, V(0, 66), V(38, 68))).Add(Ridge(6, V(58, 68), V(96, 66)));
                map.Add(Feat(FeatureKind.OreField, 4.5f, V(30, 24))).Add(Feat(FeatureKind.OreField, 4.5f, V(66, 30)));
                map.Add(Road(V(48, 20), V(48, 104)));
                map.Starts.Add(new Int2(48, 20)); map.Starts.Add(new Int2(48, 104));
                list.Add(new MissionDef
                {
                    Id = "id05", Faction = ID, Index = 5, Title = "Kettle Pass", Location = "Kettle Pass", Date = "28 April 1994",
                    Map = map, StartingCredits = 9000, EnemyFaction = CA, Music = "battle",
                    Briefing = "The Alliance counter-offensive has begun, and Kettle Pass is the cork in the bottle. If it falls, three of our armies are cut off.\n\n" +
                               "Hold the pass for fifteen minutes. The Alliance will come in waves — armour, infantry and aircraft. Your Command Bunker must survive.",
                    VictoryText = "The pass held. Alliance dead choke the valley. Glory to the defenders of Kettle Pass.",
                    Tags = { "survive waves", "defend" },
                    Players = m => { m.AddHuman(); m.AddAI("enemy", "Alliance Northern Army", CA, 1, builds: false, attacks: false); m.Human.MaxTechTier = 3; },
                    Populate = m =>
                    {
                        m.BuildBase(m.Human, 48, 20, BasePreset.Medium, "base");
                        m.Build(m.Human, "id_avturret", 44, 60); m.Build(m.Human, "id_avturret", 52, 60); m.Build(m.Human, "id_mgbunker", 48, 58);
                        m.SpawnGroup(m.Human, 48, 50, null, "id_ravager", "id_ravager", "id_ravager", "id_ironclad", "id_ironclad", "id_levy", "id_levy", "id_levy", "id_breacher", "id_breacher", "id_anvil");
                    },
                    Script = m =>
                    {
                        var e = m.P("enemy");
                        m.AddObjective("hold", "Survive for 15 minutes.");
                        m.AddObjective("hq", "Keep the Command Bunker intact.");
                        m.At(1, "cd", () => { m.Countdown("Relief column", 900); m.Say("Marshal Strand", "Hold, Commander. Not one step back."); });
                        m.When("hqlost", () => m.AllDead("base_constructionyard"), () => m.Fail("hq"));
                        m.When("held", () => m.CountdownExpired("Relief column"), () => { m.Complete("hold"); m.Complete("hq"); });
                        var spawn = new[] { V(48, 108), V(20, 108), V(76, 108) };
                        Waves(m, "w", e, 40, 60, 14, spawn, V(48, 24),
                            i => i < 4 ? Mix(Inf(CA, 5), new[] { "ca_wasp" }) : i < 9 ? Mix(Inf(CA, 4), new[] { "ca_sabre", "ca_sabre", "ca_thunderhead" }) : new[] { "ca_warden", "ca_sabre", "ca_sabre", "ca_lancet", "ca_rifleman", "ca_rifleman", "ca_lancer", "ca_arbiter" });
                        m.At(300, "air1", () => m.Attack(e, 48, 110, 48, 24, "air", Scale(m, "ca_hornet", "ca_hornet")));
                        m.At(600, "air2", () => m.Attack(e, 48, 110, 48, 24, "air", Scale(m, "ca_hornet", "ca_hornet", "ca_condor")));
                    }
                });
            }

            // ============================================================ 6. Severed Wings — destroy airbase
            {
                var map = Map("id06", "Lysa Airfields", 112, 104, MapTheme.Desert, 3606, 0.0f, 0.05f);
                map.Add(Feat(FeatureKind.OreField, 4.5f, V(22, 22))).Add(Feat(FeatureKind.OreField, 4, V(56, 40))).Add(Feat(FeatureKind.CrystalField, 4, V(90, 40)));
                map.Add(Road(V(16, 14), V(56, 50), V(84, 86))).Add(Feat(FeatureKind.Mountain, 4, V(40, 70), V(80, 56)));
                map.Starts.Add(new Int2(16, 14)); map.Starts.Add(new Int2(86, 86));
                list.Add(new MissionDef
                {
                    Id = "id06", Faction = ID, Index = 6, Title = "Severed Wings", Location = "Lysa airfields", Date = "14 May 1994",
                    Map = map, StartingCredits = 9000, EnemyFaction = CA, Music = "battle",
                    Briefing = "Alliance aircraft from the Lysa airfields have been savaging our armoured columns. Their pilots are good. Their hangars are not bombproof.\n\n" +
                               "Destroy every Air Command Hangar at Lysa and the Skyshield batteries that guard them. Flak tracks and Skyburst troopers will keep their gunships off you.",
                    VictoryText = "Lysa burns. The sky over the front belongs to the Dominion again.",
                    Tags = { "destroy airbase" },
                    Players = m => { m.AddHuman(); m.AddAI("enemy", "Alliance Air Wing", CA, 1, builds: true, attacks: true, aggression: 0.8f); m.Human.MaxTechTier = 3; },
                    Populate = m =>
                    {
                        var e = m.P("enemy");
                        m.BuildBase(m.Human, 16, 14, BasePreset.Small, "base");
                        m.SpawnGroup(m.Human, 22, 22, null, "id_ravager", "id_ravager", "id_thresher", "id_thresher", "id_levy", "id_levy", "id_skyburst", "id_skyburst");
                        m.BuildBase(e, 86, 86, BasePreset.Medium, "ebase");
                        m.BuildBase(e, 70, 70, BasePreset.Airbase, "air");
                        m.Build(e, "ca_hangar", 92, 72, "air_hangar");
                        foreach (var p in new[] { V(64, 64), V(78, 62), V(66, 78), V(82, 80), V(58, 72) }) m.Build(e, "ca_aadefense", (int)p.X, (int)p.Y, "skyshield");
                        foreach (var p in new[] { V(70, 66), V(73, 66), V(92, 70) }) { var u = m.Spawn(e, "ca_hornet", p.X, p.Y); }
                    },
                    Script = m =>
                    {
                        var e = m.P("enemy");
                        m.AddObjective("hangars", "Destroy all Alliance aircraft hangars.");
                        m.AddObjective("aa", "Destroy the Skyshield batteries.", primary: false);
                        m.When("hangars", () => m.W.CountRole(e.Id, BuildingRole.Hangar) == 0 && m.W.CountRole(e.Id, BuildingRole.AdvancedHangar) == 0 && m.W.Time > 5, () => m.Complete("hangars"));
                        m.When("aa", () => m.AllDead("skyshield") && m.W.Time > 5, () => m.Complete("aa"));
                        m.Every(150, "sorties", () => m.W.CountRole(e.Id, BuildingRole.Hangar) > 0, () => m.Attack(e, 80, 80, 20, 18, "sortie", Scale(m, "ca_hornet", "ca_hornet")));
                    }
                });
            }

            // ============================================================ 7. Iron Tide — destroy multiple bases
            {
                var map = Map("id07", "Meridian Approaches", 136, 112, MapTheme.Temperate, 3707, 0.08f);
                map.Add(River(4, new[] { 1, 3 }, V(0, 56), V(34, 54), V(68, 60), V(102, 54), V(136, 58)));
                map.Add(Feat(FeatureKind.OreField, 4.5f, V(68, 22))).Add(Feat(FeatureKind.OreField, 4.5f, V(30, 30))).Add(Feat(FeatureKind.OreField, 4, V(30, 86))).Add(Feat(FeatureKind.OreField, 4, V(106, 86))).Add(Feat(FeatureKind.CrystalField, 4, V(68, 76)));
                map.Add(Feat(FeatureKind.Forest, 7, V(10, 60), V(126, 64)));
                map.Starts.Add(new Int2(68, 14)); map.Starts.Add(new Int2(24, 96)); map.Starts.Add(new Int2(112, 96));
                map.Neutrals.Add(new NeutralPlacement("tech_derrick", 50, 40)); map.Neutrals.Add(new NeutralPlacement("tech_derrick", 86, 40));
                list.Add(new MissionDef
                {
                    Id = "id07", Faction = ID, Index = 7, Title = "Iron Tide", Location = "Approaches to Meridian", Date = "3 June 1994",
                    Map = map, StartingCredits = 10000, EnemyFaction = CA, Music = "dominion",
                    Briefing = "Two Alliance corps bar the road to Meridian: the Coastal Corps to the west and the Republican Guard to the east.\n\n" +
                               "Destroy both. Let the tide of iron roll over them.",
                    VictoryText = "Both corps are shattered. Meridian's towers are visible on the horizon.",
                    Tags = { "destroy multiple bases" },
                    Players = m =>
                    {
                        m.AddHuman();
                        m.AddAI("west", "Alliance Coastal Corps", CA, 1, builds: true, attacks: true);
                        m.AddAI("east", "Republican Guard", CA, 1, builds: true, attacks: true);
                        m.AddNeutral();
                        m.Human.MaxTechTier = 4;
                    },
                    Populate = m =>
                    {
                        m.Spawn(m.Human, "id_foundry", 68, 14);
                        m.SpawnGroup(m.Human, 72, 18, null, "id_ravager", "id_ravager", "id_ravager", "id_thresher", "id_levy", "id_levy", "id_levy", "id_levy", "id_breacher", "id_sapper");
                        m.BuildBase(m.P("west"), 24, 96, BasePreset.Large, "west");
                        m.BuildBase(m.P("east"), 112, 96, BasePreset.Large, "east");
                    },
                    Script = m =>
                    {
                        m.AddObjective("west", "Destroy the Alliance Coastal Corps.");
                        m.AddObjective("east", "Destroy the Republican Guard.");
                        m.When("w", () => m.IsDestroyed(m.P("west")), () => m.Complete("west"));
                        m.When("e", () => m.IsDestroyed(m.P("east")), () => m.Complete("east"));
                    }
                });
            }

            // ============================================================ 8. The Prisoners — rescue trapped forces
            {
                var map = Map("id08", "Saint Aurel Stockade", 104, 96, MapTheme.Temperate, 3808, 0.1f);
                map.Add(Feat(FeatureKind.OreField, 4, V(22, 20))).Add(Road(V(14, 12), V(52, 48), V(80, 76))).Add(Feat(FeatureKind.Lake, 6, V(30, 64))).Add(Feat(FeatureKind.Forest, 7, V(76, 30)));
                map.Starts.Add(new Int2(14, 12)); map.Starts.Add(new Int2(80, 78));
                list.Add(new MissionDef
                {
                    Id = "id08", Faction = ID, Index = 8, Title = "The Prisoners", Location = "Saint Aurel", Date = "17 June 1994",
                    Map = map, StartingCredits = 6000, EnemyFaction = CA, Music = "tension",
                    Briefing = "Chief Engineer Aldric Moor — the mind behind Hammerfall — was captured with his staff during the retreat from Lysa. They are held in a stockade at Saint Aurel.\n\n" +
                               "Break into the stockade and lead Moor and his engineers back to the evacuation point. Moor himself must survive.\n\n" +
                               "The Alliance plans to move the prisoners by truck. If the transfer convoy reaches the southern road, they are lost to us.",
                    VictoryText = "Moor is home. Hammerfall will be finished.",
                    Tags = { "rescue", "prevent enemy objective" },
                    Players = m => { m.AddHuman(); m.AddAI("enemy", "Saint Aurel Garrison", CA, 1, builds: true, attacks: false); var pr = m.AddAI("prisoners", "Prisoners", ID, 0, builds: false, attacks: false); pr.IsNeutral = true; pr.IsAI = false; m.Human.MaxTechTier = 3; },
                    Populate = m =>
                    {
                        var e = m.P("enemy"); var pr = m.P("prisoners");
                        m.BuildBase(m.Human, 14, 12, BasePreset.Small, "base");
                        m.SpawnGroup(m.Human, 18, 20, null, "id_ravager", "id_ravager", "id_bulldog", "id_levy", "id_levy", "id_levy", "id_breacher", "id_surgeon");
                        m.BuildBase(e, 84, 82, BasePreset.Medium, "ebase");
                        for (int i = 0; i < 6; i++) m.Build(e, "ca_wall", 66 + i, 68); for (int i = 0; i < 6; i++) m.Build(e, "ca_wall", 66 + i, 74);
                        for (int i = 0; i < 5; i++) m.Build(e, "ca_wall", 66, 69 + i);
                        m.Build(e, "ca_gate", 71, 70, "gate");
                        m.Build(e, "ca_aiturret", 74, 70); m.Build(e, "ca_mgbunker", 64, 66);
                        m.Spawn(pr, "id_sapper", 68, 71, "moor"); m.Spawn(pr, "id_sapper", 69, 72, "staff"); m.Spawn(pr, "id_sapper", 68, 72, "staff"); m.Spawn(pr, "id_surgeon", 69, 71, "staff");
                        m.SpawnGroup(e, 62, 62, "guards", "ca_rifleman", "ca_rifleman", "ca_bulwark", "ca_lancer", "ca_sabre");
                    },
                    Script = m =>
                    {
                        var e = m.P("enemy"); var pr = m.P("prisoners");
                        m.Zone("stockade", 69, 71, 4); m.Zone("evac", 10, 8, 5); m.Zone("southroad", 100, 10, 5);
                        m.AddObjective("free", "Break into the stockade and free Chief Engineer Moor.");
                        m.AddObjective("evac", "Bring Moor to the evacuation point.");
                        m.AddObjective("moor", "Moor must survive.");
                        m.AddObjective("convoy", "Stop the prisoner transfer convoy.", primary: false, hidden: true);
                        m.When("free", () => m.CountInZone("stockade", m.Human) > 0 || m.AllDead("gate"), () =>
                        {
                            m.Complete("free");
                            m.GiveTagged("moor", m.Human); m.GiveTagged("staff", m.Human);
                            m.Say("Chief Engineer Moor", "About time! Get us out of here, Commander — I have a missile to finish.");
                        });
                        m.When("moordead", () => m.AllDead("moor"), () => m.Fail("moor"));
                        m.When("evac", () => m.IsComplete("free") && m.CountInZone("evac", m.Human, u => u.Tag == "moor") > 0, () => { m.Complete("evac"); m.Complete("moor"); });
                        m.At(420, "transfer", () =>
                        {
                            if (m.IsComplete("free")) return;
                            m.Reveal("convoy");
                            m.Say("Radio Intercept", "(Alliance) ...prisoner transfer truck departing now. Southern road.");
                            var t = m.Spawn(e, "civ_truck", 80, 70, "transfer");
                            m.W.Commands.Move(new[] { t.Id }, e.Id, new Vec2(100, 10), false, false);
                        });
                        m.When("transfer_escape", () => m.Fired("transfer") && m.CountInZone("southroad", e, u => u.Tag == "transfer") > 0, () => { m.Fail("convoy"); });
                        m.When("transfer_dead", () => m.Fired("transfer") && m.AllDead("transfer") && m.IsActive("convoy"), () => m.Complete("convoy"));
                    }
                });
            }

            // ============================================================ 9. Colossus Road — escort siege crawlers
            {
                var map = Map("id09", "Colossus Road", 128, 80, MapTheme.Wasteland, 3909, 0.06f, 0.05f);
                map.Add(Road(V(8, 40), V(40, 36), V(70, 44), V(100, 40), V(118, 40)));
                map.Add(Ridge(4, V(50, 0), V(56, 26))).Add(Ridge(4, V(82, 80), V(78, 54)));
                map.Add(Feat(FeatureKind.OreField, 4, V(20, 16)));
                var plat = Feat(FeatureKind.Plateau, 10, V(118, 40)); plat.Value = 1; plat.Ramps.Add(V(106, 40)); map.Add(plat);
                map.Starts.Add(new Int2(10, 40)); map.Starts.Add(new Int2(118, 40));
                list.Add(new MissionDef
                {
                    Id = "id09", Faction = ID, Index = 9, Title = "Colossus Road", Location = "Fort Varenne road", Date = "4 July 1994", NoBase = true,
                    Map = map, StartingCredits = 0, EnemyFaction = CA, Music = "battle",
                    Briefing = "Fort Varenne's guns have stopped three of our assaults. Conventional artillery cannot reach it.\n\n" +
                               "Escort two Earthbreaker Siege Crawlers along the road to the firing position below the fort. Once at least one crawler is in position, destroy Fort Varenne.",
                    VictoryText = "Fort Varenne is rubble. The Earthbreakers have earned their name.",
                    Tags = { "escort", "siege" },
                    Players = m => { m.AddHuman(); m.AddAI("enemy", "Fort Varenne", CA, 1, builds: false, attacks: false); },
                    Populate = m =>
                    {
                        var e = m.P("enemy");
                        m.Spawn(m.Human, "id_earthbreaker", 8, 38, "crawler", 0); m.Spawn(m.Human, "id_earthbreaker", 8, 42, "crawler", 0);
                        m.SpawnGroup(m.Human, 12, 36, null, "id_ravager", "id_ravager", "id_ravager", "id_behemoth", "id_thresher", "id_wrench", "id_levy", "id_levy", "id_breacher", "id_breacher", "id_surgeon");
                        m.Build(e, "ca_hq", 116, 38, "fort"); m.Build(e, "ca_artillery", 112, 44, "fort"); m.Build(e, "ca_artillery", 112, 34, "fort");
                        m.Build(e, "ca_avturret", 108, 40, "fort"); m.Build(e, "ca_aiturret", 109, 44, "fort"); m.Build(e, "ca_advpower", 120, 44, "fort"); m.Build(e, "ca_power", 120, 34, "fort");
                        m.SpawnGroup(e, 44, 40, "block1", "ca_rifleman", "ca_rifleman", "ca_lancer", "ca_sabre");
                        m.SpawnGroup(e, 72, 46, "block2", "ca_sabre", "ca_sabre", "ca_lancer", "ca_lancer", "ca_rifleman");
                        m.SpawnGroup(e, 96, 38, "block3", "ca_warden", "ca_lancet", "ca_rifleman", "ca_rifleman");
                    },
                    Script = m =>
                    {
                        var e = m.P("enemy");
                        m.Zone("firing", 96, 40, 5);
                        m.AddObjective("escort", "Get at least one Siege Crawler to the firing position.");
                        m.AddObjective("fort", "Destroy Fort Varenne.");
                        m.When("lost", () => m.AllDead("crawler") && !m.IsComplete("fort"), () => m.Fail("escort"));
                        m.When("inpos", () => m.CountInZone("firing", m.Human, u => u.Tag == "crawler") > 0, () => { m.Complete("escort"); m.Say("Crawler Crew", "In position. Opening fire on the fort!"); });
                        m.When("fort", () => m.AllDead("fort"), () => { m.Complete("fort"); m.Complete("escort"); });
                        m.Every(110, "harass", () => !m.AllDead("fort"), () => m.Attack(e, 126, 70, 50, 40, "harass", Scale(m, "ca_hornet", "ca_wasp", "ca_wasp")));
                    }
                });
            }

            // ============================================================ 10. Black Harbor — naval assault
            {
                var map = Map("id10", "Black Harbor", 120, 104, MapTheme.Temperate, 4010, 0.06f);
                map.Add(Ocean(60, 56, 0.1f, 1f));
                map.Add(Feat(FeatureKind.OreField, 4.5f, V(24, 18))).Add(Feat(FeatureKind.OreField, 4, V(70, 22)));
                map.Starts.Add(new Int2(20, 14)); map.Starts.Add(new Int2(60, 94));
                list.Add(new MissionDef
                {
                    Id = "id10", Faction = ID, Index = 10, Title = "Black Harbor", Location = "Black Harbor", Date = "19 July 1994",
                    Map = map, StartingCredits = 10000, EnemyFaction = CA, Music = "naval",
                    Briefing = "The Alliance fleet at Black Harbor protects Meridian's sea flank. While it floats, we cannot encircle the capital.\n\n" +
                               "Build a fleet at the Dockyard and destroy both Alliance Naval Yards across the bay. Hydra submarines are invisible while submerged — except to destroyers.",
                    VictoryText = "The Alliance fleet lies on the bottom of Black Harbor. Meridian is surrounded.",
                    Tags = { "naval" },
                    Players = m => { m.AddHuman(); m.AddAI("enemy", "Alliance Home Fleet", CA, 1, builds: true, attacks: true, aggression: 0.8f); m.Human.MaxTechTier = 4; },
                    Populate = m =>
                    {
                        var e = m.P("enemy");
                        m.BuildBase(m.Human, 20, 14, BasePreset.Medium, "base");
                        m.Build(m.Human, "id_shipyard", 40, 50);
                        m.Spawn(m.Human, "id_bastion", 46, 60); m.Spawn(m.Human, "id_hydra", 50, 58); m.Spawn(m.Human, "id_pike", 44, 62);
                        m.BuildBase(e, 60, 94, BasePreset.Medium, "ebase");
                        m.Build(e, "ca_shipyard", 40, 68, "navalyard"); m.Build(e, "ca_shipyard", 80, 68, "navalyard");
                        m.Spawn(e, "ca_vigilant", 60, 66); m.Spawn(e, "ca_barracuda", 70, 62); m.Spawn(e, "ca_swift", 50, 64);
                    },
                    Script = m =>
                    {
                        var e = m.P("enemy");
                        m.AddObjective("yards", "Destroy both Alliance Naval Yards.");
                        m.AddObjective("ebase", "Destroy the harbor base.", primary: false);
                        m.When("yards", () => m.AllDead("navalyard"), () => m.Complete("yards"));
                        m.When("ebase", () => m.CountBuildings(e) == 0, () => m.Complete("ebase"));
                        m.Every(140, "fleet", () => !m.AllDead("navalyard"), () => m.Attack(e, 60, 70, 40, 52, "fleet", Scale(m, "ca_swift", "ca_vigilant")));
                    }
                });
            }

            // ============================================================ 11. Eye of Helios — destroy superweapon
            {
                var map = Map("id11", "Helios Uplink", 120, 120, MapTheme.Desert, 4111, 0.0f, 0.06f);
                var plat = Feat(FeatureKind.Plateau, 15, V(92, 94)); plat.Value = 1; plat.Ramps.Add(V(76, 88)); plat.Ramps.Add(V(92, 78)); map.Add(plat);
                map.Add(Feat(FeatureKind.OreField, 4.5f, V(24, 24))).Add(Feat(FeatureKind.OreField, 4.5f, V(56, 30))).Add(Feat(FeatureKind.CrystalField, 4, V(30, 64))).Add(Feat(FeatureKind.OreField, 4, V(66, 64)));
                map.Add(Feat(FeatureKind.Mountain, 4, V(50, 90), V(90, 50)));
                map.Starts.Add(new Int2(18, 16)); map.Starts.Add(new Int2(92, 94));
                list.Add(new MissionDef
                {
                    Id = "id11", Faction = ID, Index = 11, Title = "Eye of Helios", Location = "Helios uplink station", Date = "28 July 1994",
                    Map = map, StartingCredits = 12000, EnemyFaction = CA, Music = "tension",
                    Briefing = "The Alliance has finished Helios. Its control station in the eastern desert can call down fire from orbit anywhere on Veldmark.\n\n" +
                               "Destroy the Helios Orbital Lance Control before its third firing window. Each window, it will strike at your base. An Interceptor Battery will blunt our bombardments; hit it first if you can.",
                    VictoryText = "Helios is blind and mute. Nothing now stands between us and Meridian.",
                    Tags = { "destroy superweapon", "time limit" },
                    Players = m => { m.AddHuman(); m.AddAI("enemy", "Helios Defence Command", CA, 1, builds: true, attacks: true, aggression: 1.1f); m.Human.MaxTechTier = 4; },
                    Populate = m =>
                    {
                        var e = m.P("enemy");
                        m.BuildBase(m.Human, 18, 16, BasePreset.Medium, "base");
                        m.SpawnGroup(m.Human, 24, 26, null, "id_behemoth", "id_ravager", "id_ravager", "id_spike", "id_thresher", "id_anvil", "id_anvil", "id_breacher", "id_breacher", "id_sapper");
                        m.BuildBase(e, 92, 94, BasePreset.Fortress, "fort");
                        m.Build(e, "ca_superweapon", 100, 102, "helios");
                    },
                    Script = m =>
                    {
                        var e = m.P("enemy");
                        m.AddObjective("helios", "Destroy the Helios Orbital Lance Control before its third firing.");
                        m.AddObjective("fort", "Destroy the Helios defence base.", primary: false);
                        m.At(1, "cd", () => { m.Countdown("Helios firing window", 540); m.SetFlag("windows", 0); });
                        m.Every(1, "window", () => !m.AllDead("helios") && m.CountdownExpired("Helios firing window"), () =>
                        {
                            int n = m.Flag("windows") + 1; m.SetFlag("windows", n);
                            Vec2 target = new Vec2(18, 16);
                            foreach (var b in m.W.BuildingsOf(m.Human.Id)) { target = b.Center; if (m.W.Rng.Chance(0.3f)) break; }
                            m.Say("Radar Op", "Orbital strike! Brace!");
                            m.W.Emit(new SimEvent { Type = SimEventType.SupportPowerLaunched, Player = -1, OtherId = e.Id, Pos = target, Text = "OrbitalLance", Value = 0 });
                            m.W.Powers.AreaDamage(target, 4.5f, 2600, "Super", e.Id, 0, true);
                            m.W.Emit(new SimEvent { Type = SimEventType.SupportPowerImpact, Player = -1, Pos = target, Text = "OrbitalLance", Value = 4.5f });
                            if (n >= 3) m.Fail("helios");
                            else m.Countdown("Helios firing window", 540);
                        });
                        m.When("dead", () => m.AllDead("helios"), () => { m.Complete("helios"); m.ClearCountdown("Helios firing window"); });
                        m.When("fort", () => m.CountBuildings(e) == 0, () => m.Complete("fort"));
                    }
                });
            }

            // ============================================================ 12. Meridian Falls — final assault
            {
                var map = Map("id12", "Meridian", 144, 136, MapTheme.Temperate, 4212, 0.07f, 0.03f);
                map.Add(Ocean(144, 68, 1f, 0.05f));
                map.Add(Feat(FeatureKind.OreField, 4.5f, V(24, 22))).Add(Feat(FeatureKind.OreField, 4.5f, V(70, 22))).Add(Feat(FeatureKind.OreField, 4.5f, V(40, 70))).Add(Feat(FeatureKind.CrystalField, 4, V(70, 70))).Add(Feat(FeatureKind.OreField, 4, V(30, 110)));
                map.Add(River(4, new[] { 1, 2, 3 }, V(0, 60), V(30, 62), V(60, 58), V(90, 62), V(126, 60)));
                map.Add(Road(V(24, 14), V(60, 58), V(70, 116), V(100, 124)));
                map.Starts.Add(new Int2(22, 14)); map.Starts.Add(new Int2(72, 14)); map.Starts.Add(new Int2(70, 116)); map.Starts.Add(new Int2(26, 112));
                list.Add(new MissionDef
                {
                    Id = "id12", Faction = ID, Index = 12, Title = "Meridian Falls", Location = "Meridian, capital of the Continental Alliance", Date = "9 August 1994",
                    Map = map, StartingCredits = 15000, EnemyFaction = CA, Music = "finale",
                    Briefing = "Meridian. The heart of the Alliance. The Marshal-Chancellor has given the order: the city falls today.\n\n" +
                               "With the 2nd Shock Army under Commissar Volk at your side, destroy both Alliance Command Centers in Meridian. General Marsh will try to evacuate the government by convoy along the coast road — stop it if you can.\n\n" +
                               "Victory, Commander. Nothing less.",
                    VictoryText = "Meridian has fallen. The Alliance government has surrendered. The aurite fields — and Veldmark — belong to the Iron Dominion.\n\nSteel without end.",
                    Tags = { "eliminate enemy commanders", "allied operation", "prevent enemy objective" },
                    Players = m =>
                    {
                        m.AddHuman();
                        m.AddAI("ally", "2nd Shock Army (Volk)", ID, 0, builds: true, attacks: true);
                        m.AddAI("meridian", "Meridian Defence Force", CA, 1, builds: true, attacks: true, aggression: 1.1f);
                        m.AddAI("guard", "Republican Guard", CA, 1, builds: true, attacks: true);
                    },
                    Populate = m =>
                    {
                        m.BuildBase(m.Human, 22, 14, BasePreset.Medium, "base");
                        m.SpawnGroup(m.Human, 26, 24, null, "id_behemoth", "id_behemoth", "id_ravager", "id_ravager", "id_spike", "id_thresher", "id_hailstorm", "id_breacher", "id_breacher", "id_sapper");
                        m.BuildBase(m.P("ally"), 72, 14, BasePreset.Large, "ally");
                        m.BuildBase(m.P("meridian"), 70, 116, BasePreset.Fortress, "hq1");
                        m.BuildBase(m.P("guard"), 26, 112, BasePreset.Large, "hq2");
                        m.Build(m.P("meridian"), "ca_superweapon", 80, 124, "helios2");
                    },
                    Script = m =>
                    {
                        m.Zone("coast", 120, 126, 6);
                        m.AddObjective("hq", "Destroy both Alliance Command Centers.");
                        m.AddObjective("convoy", "Stop the government evacuation convoy.", primary: false, hidden: true);
                        m.AddObjective("helios", "Destroy the backup Orbital Lance Control.", primary: false);
                        m.At(3, "intro", () => m.Say("Commissar Volk", "2nd Shock Army stands ready. Meridian burns tonight, Commander."));
                        m.When("hq", () => m.AllDead("hq1_constructionyard") && m.AllDead("hq2_constructionyard"), () => m.Complete("hq"));
                        m.When("helios", () => m.AllDead("helios2"), () => m.Complete("helios"));
                        m.At(600, "evac", () =>
                        {
                            m.Reveal("convoy");
                            m.Say("Radio Intercept", "(Alliance) ...government convoy is moving. Coast road. Godspeed.");
                            for (int i = 0; i < 4; i++) { var t = m.Spawn(m.P("meridian"), "civ_truck", 76 + i, 112, "evac"); m.W.Commands.Move(new[] { t.Id }, t.Owner, new Vec2(120, 126), false, false); }
                        });
                        m.When("escaped", () => m.Fired("evac") && m.CountInZone("coast", m.P("meridian"), u => u.Tag == "evac") >= 2, () => m.Fail("convoy"));
                        m.When("stopped", () => m.Fired("evac") && m.CountTagged("evac") <= 1 && m.IsActive("convoy"), () => m.Complete("convoy"));
                    }
                });
            }
            return list;
        }

        static bool HasDef(MissionRunner m, Player p, string def)
        {
            foreach (var b in m.W.BuildingsOf(p.Id)) if (b.DefId == def) return true;
            return false;
        }
    }

    /// <summary>The in-engine introductory cinematic: a narrated battle at Halvorsen.</summary>
    public static class IntroCinematic
    {
        public static MissionDef Build()
        {
            var map = MissionKit.Map("intro", "Halvorsen, 9 March 1994", 72, 64, MapTheme.Temperate, 777, 0.1f);
            map.Add(MissionKit.River(3, new[] { 1 }, new Vec2(0, 34), new Vec2(36, 32), new Vec2(72, 35)));
            map.Add(MissionKit.Road(new Vec2(36, 4), new Vec2(36, 60)));
            map.Neutrals.Add(new NeutralPlacement("civ_chapel", 30, 22)); map.Neutrals.Add(new NeutralPlacement("civ_house", 42, 18)); map.Neutrals.Add(new NeutralPlacement("civ_watertower", 26, 14));
            return new MissionDef
            {
                Id = "intro", Faction = FactionId.Alliance, Index = 0, Title = "Prologue", IsCinematic = true, Map = map, StartingCredits = 0, Music = "menu",
                Briefing = CampaignDatabase.Prologue,
                Players = m =>
                {
                    m.AddHuman("Observer");
                    m.AddAI("dom", "Iron Dominion", FactionId.Dominion, 1, builds: false, attacks: false);
                    m.AddAI("all", "Continental Alliance", FactionId.Alliance, 0, builds: false, attacks: false);
                    m.AddNeutral();
                },
                Populate = m =>
                {
                    m.W.Fog.SetRevealAll(m.Human.Team, true);
                    m.SpawnGroup(m.P("all"), 36, 20, "defenders", "ca_sabre", "ca_sabre", "ca_rifleman", "ca_rifleman", "ca_rifleman", "ca_lancer", "ca_lancer", "ca_bulwark");
                    m.Build(m.P("all"), "ca_mgbunker", 33, 28); m.Build(m.P("all"), "ca_avturret", 39, 28);
                    m.SpawnGroup(m.P("dom"), 36, 52, "attackers", "id_ravager", "id_ravager", "id_ravager", "id_behemoth", "id_levy", "id_levy", "id_levy", "id_levy", "id_levy", "id_breacher", "id_breacher");
                    m.Spawn(m.Human, "ca_owl", 36, 4).Invulnerable = true;
                },
                Script = m =>
                {
                    m.AutoWinOnPrimaries = false;
                    m.At(0.2f, "c0", () => { m.Cinematic(true); m.CameraTo(36, 56, 0.1f); });
                    m.At(1, "n1", () => m.Say("Narrator", "Veldmark, 1994. For thirty years the Iron Dominion has fed its foundries with the iron of the north."));
                    m.At(7, "n2", () => { m.Say("Narrator", "Beneath the southern republics lies aurite — the crystal that will power the next century."); m.CameraTo(36, 40, 5); });
                    m.At(9, "go", () => m.SendTagged("attackers", 36, 18));
                    m.At(13, "n3", () => m.Say("Narrator", "Marshal-Chancellor Strand has decided that it belongs to the Dominion."));
                    m.At(16, "art", () => { for (int i = 0; i < 5; i++) { var p = new Vec2(30 + i * 3, 24); m.W.Emit(new SimEvent { Type = SimEventType.Explosion, Player = -1, Pos = p, Value = 1.5f }); } });
                    m.At(19, "n4", () => { m.Say("Narrator", "The republics stand together as the Continental Alliance. Outnumbered. Outgunned. Unbroken."); m.CameraTo(36, 26, 5); });
                    m.At(27, "n5", () => m.Say("Narrator", "In laboratories on both sides, two weapons are nearing completion: Helios... and Hammerfall."));
                    m.At(34, "n6", () => { m.Say("Narrator", "The war for Veldmark begins at Halvorsen. How it ends... is up to you."); m.CameraTo(36, 30, 6); });
                    m.At(42, "end", () => { m.Cinematic(false); m.Win("Prologue complete."); });
                }
            };
        }
    }
}
