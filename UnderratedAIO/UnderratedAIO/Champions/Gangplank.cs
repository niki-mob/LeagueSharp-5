﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics.Eventing.Reader;
using System.Drawing.Text;
using System.Linq;
using System.Reflection;
using System.Text;
using Color = System.Drawing.Color;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;
using UnderratedAIO.Helpers;
using Environment = UnderratedAIO.Helpers.Environment;
using Orbwalking = UnderratedAIO.Helpers.Orbwalking;

namespace UnderratedAIO.Champions
{
    internal class Gangplank
    {
        public static Menu config;
        public static Orbwalking.Orbwalker orbwalker;
        public static AutoLeveler autoLeveler;
        public static Spell Q, W, E, R;
        public static readonly Obj_AI_Hero player = ObjectManager.Player;
        public static bool justQ, justE;
        public Vector3 ePos;
        public const int BarrelExplosionRange = 375;
        public const int BarrelConnectionRange = 660;

        public Gangplank()
        {
            InitGangPlank();
            InitMenu();
            Game.PrintChat("<font color='#9933FF'>Soresu </font><font color='#FFFFFF'>- Gangplank</font>");
            Drawing.OnDraw += Game_OnDraw;
            Game.OnUpdate += Game_OnGameUpdate;
            Helpers.Jungle.setSmiteSlot();
            Utility.HpBarDamageIndicator.DamageToUnit = ComboDamage;
            Obj_AI_Base.OnProcessSpellCast += Game_ProcessSpell;
        }

        private void InitGangPlank()
        {
            Q = new Spell(SpellSlot.Q, 625f);
            W = new Spell(SpellSlot.W);
            E = new Spell(SpellSlot.E, 1000);
            E.SetSkillshot(0.8f, 50, float.MaxValue, false, SkillshotType.SkillshotCircle);
            R = new Spell(SpellSlot.R);
            R.SetSkillshot(0.7f, 250, float.MaxValue, false, SkillshotType.SkillshotCircle);
        }

        private void Game_OnGameUpdate(EventArgs args)
        {
            Jungle.CastSmite(config.Item("useSmite").GetValue<KeyBind>().Active);
            switch (orbwalker.ActiveMode)
            {
                case Orbwalking.OrbwalkingMode.Combo:
                    Combo();
                    break;
                case Orbwalking.OrbwalkingMode.Mixed:
                    Harass();
                    break;
                case Orbwalking.OrbwalkingMode.LaneClear:
                    Clear();
                    break;
                case Orbwalking.OrbwalkingMode.LastHit:
                    Lasthit();
                    break;
                default:
                    break;
            }
            if (config.Item("AutoR", true).GetValue<bool>() && R.IsReady())
            {
                foreach (var enemy in
                    HeroManager.Enemies.Where(
                        e => e.HealthPercent < 25 && e.IsValidTarget() && e.Distance(player) > 1500))
                {
                    var allies =
                        HeroManager.Allies.Where(
                            a => enemy.Distance(a) < 700 && CombatHelper.IsFacing(a, enemy.Position));
                    if (allies != null)
                    {
                        R.Cast(R.GetPrediction(enemy).CastPosition);
                    }
                }
            }
        }

        private void Lasthit()
        {
            if (Q.IsReady())
            {
                var mini =
                    MinionManager.GetMinions(Q.Range, MinionTypes.All, MinionTeam.NotAlly)
                        .Where(m => m.Health < Q.GetDamage(m) && m.SkinName != "GangplankBarrel")
                        .OrderByDescending(m => m.MaxHealth)
                        .ThenByDescending(m => m.Distance(player))
                        .FirstOrDefault();

                if (mini != null && !justE)
                {
                    Q.CastOnUnit(mini, config.Item("packets").GetValue<bool>());
                }
            }
        }


        private void Harass()
        {
            float perc = config.Item("minmanaH", true).GetValue<Slider>().Value / 100f;
            if (player.Mana < player.MaxMana * perc)
            {
                return;
            }
            if (config.Item("useqLHH", true).GetValue<bool>())
            {
                var mini =
                    MinionManager.GetMinions(Q.Range, MinionTypes.All, MinionTeam.NotAlly)
                        .Where(m => m.Health < Q.GetDamage(m) && m.SkinName != "GangplankBarrel")
                        .OrderByDescending(m => m.Distance(player))
                        .FirstOrDefault();
                if (mini != null)
                {
                    Q.CastOnUnit(mini, config.Item("packets").GetValue<bool>());
                    return;
                }
            }
            Obj_AI_Hero target = TargetSelector.GetTarget(Q.Range, TargetSelector.DamageType.Physical);
            if (target == null)
            {
                return;
            }
            if (config.Item("useqH", true).GetValue<bool>() && Q.CanCast(target) && !justE)
            {
                Q.CastOnUnit(target, config.Item("packets").GetValue<bool>());
            }
            if (config.Item("useeH", true).GetValue<bool>() && Q.CanCast(target) &&
                config.Item("eStacksH", true).GetValue<Slider>().Value < E.Instance.Ammo)
            {
                CastEtarget(target);
            }
        }

        private void Clear()
        {
            float perc = config.Item("minmana", true).GetValue<Slider>().Value / 100f;
            if (player.Mana < player.MaxMana * perc)
            {
                return;
            }
            if (Q.IsReady())
            {
                var barrel =
                    ObjectManager.Get<Obj_AI_Base>()
                        .FirstOrDefault(
                            o =>
                                o.IsValid && !o.IsDead && o.Distance(player) < 1600 && o.SkinName == "GangplankBarrel" &&
                                o.GetBuff("gangplankebarrellife").Caster.IsMe && o.Health < 2 &&
                                Environment.Minion.countMinionsInrange(o.Position, BarrelExplosionRange) >=
                                config.Item("eMinHit", true).GetValue<Slider>().Value);
                if (barrel != null)
                {
                    Q.CastOnUnit(barrel, config.Item("packets").GetValue<bool>());
                    return;
                }
            }
            if (config.Item("useqLC", true).GetValue<bool>())
            {
                Lasthit();
            }
            if (config.Item("useeLC", true).GetValue<bool>() && E.IsReady() &&
                config.Item("eStacksLC", true).GetValue<Slider>().Value < E.Instance.Ammo)
            {
                MinionManager.FarmLocation bestPositionE =
                    E.GetCircularFarmLocation(
                        MinionManager.GetMinions(
                            ObjectManager.Player.ServerPosition, E.Range, MinionTypes.All, MinionTeam.NotAlly),
                        BarrelExplosionRange);

                if (bestPositionE.MinionsHit >= config.Item("eMinHit", true).GetValue<Slider>().Value &&
                    bestPositionE.Position.Distance(ePos) > 400)
                {
                    E.Cast(bestPositionE.Position, config.Item("packets").GetValue<bool>());
                }
            }
        }

        private void Combo()
        {
            Obj_AI_Hero target = TargetSelector.GetTarget(
                E.Range, TargetSelector.DamageType.Physical, true, HeroManager.Enemies.Where(h => h.IsInvulnerable));
            if (target == null)
            {
                return;
            }
            var ignitedmg = (float) player.GetSummonerSpellDamage(target, Damage.SummonerSpell.Ignite);
            bool hasIgnite = player.Spellbook.CanUseSpell(player.GetSpellSlot("SummonerDot")) == SpellState.Ready;
            if (config.Item("useIgnite", true).GetValue<bool>() && ignitedmg > target.Health && hasIgnite &&
                !CombatHelper.CheckCriticalBuffs(target) && !Q.IsReady() && !justQ)
            {
                player.Spellbook.CastSpell(player.GetSpellSlot("SummonerDot"), target);
            }
            if (config.Item("usew", true).GetValue<Slider>().Value > player.HealthPercent &&
                player.CountEnemiesInRange(500) > 0)
            {
                W.Cast();
            }
            if (R.IsReady() && config.Item("user", true).GetValue<bool>())
            {
                var Rtarget =
                    HeroManager.Enemies.FirstOrDefault(e => e.HealthPercent < 50 && e.CountAlliesInRange(660) > 0);
                if (Rtarget != null)
                {
                    R.CastIfWillHit(Rtarget, config.Item("Rmin", true).GetValue<Slider>().Value);
                }
            }
            if (config.Item("useItems").GetValue<bool>())
            {
                ItemHandler.UseItems(target, config, ComboDamage(target), config.Item("AutoW", true).GetValue<bool>());
            }
            var barrels =
                ObjectManager.Get<Obj_AI_Base>()
                    .Where(
                        o =>
                            o.IsValid && !o.IsDead && o.Distance(player) < 1600 && o.SkinName == "GangplankBarrel" &&
                            o.GetBuff("gangplankebarrellife").Caster.IsMe)
                    .ToList();
            if (config.Item("usee", true).GetValue<bool>() && E.IsReady() && player.Distance(target) < E.Range &&
                Orbwalking.CanMove(100) && config.Item("eStacksC", true).GetValue<Slider>().Value < E.Instance.Ammo)
            {
                CastE(target, barrels);
            }
            var meleeRangeBarrel =
                barrels.FirstOrDefault(
                    b =>
                        b.Health < 2 && b.Distance(player) < Orbwalking.GetAutoAttackRange(player, b) &&
                        b.CountEnemiesInRange(BarrelExplosionRange) > 0);
            if (meleeRangeBarrel != null)
            {
                orbwalker.ForceTarget(meleeRangeBarrel);
            }
            if (Q.IsReady() && !justE)
            {
                if (barrels.Any())
                {
                    var detoneateTargetBarrels = barrels.Where(b => b.Distance(player) < Q.Range);
                    if (config.Item("detoneateTarget", true).GetValue<bool>())
                    {
                        if (detoneateTargetBarrels.Any())
                        {
                            foreach (var detoneateTargetBarrel in detoneateTargetBarrels)
                            {
                                if (detoneateTargetBarrel.Health > 1)
                                {
                                    continue;
                                }
                                if (
                                    detoneateTargetBarrel.Distance(Prediction.GetPrediction(target, 0.25f).UnitPosition) <
                                    BarrelExplosionRange &&
                                    target.Distance(detoneateTargetBarrel.Position) < BarrelExplosionRange)
                                {
                                    Q.CastOnUnit(detoneateTargetBarrel, config.Item("packets").GetValue<bool>());
                                    return;
                                }
                                var detoneateTargetBarrelSeconds =
                                    barrels.Where(b => b.Distance(detoneateTargetBarrel) < BarrelConnectionRange);
                                if (detoneateTargetBarrelSeconds.Any())
                                {
                                    foreach (var detoneateTargetBarrelSecond in detoneateTargetBarrelSeconds)
                                    {
                                        if (
                                            detoneateTargetBarrelSecond.Distance(
                                                Prediction.GetPrediction(target, 0.9f).UnitPosition) <
                                            BarrelExplosionRange &&
                                            target.Distance(detoneateTargetBarrelSecond.Position) < BarrelExplosionRange)
                                        {
                                            Q.CastOnUnit(detoneateTargetBarrel, config.Item("packets").GetValue<bool>());
                                            return;
                                        }
                                    }
                                }
                            }
                        }

                        if (config.Item("detoneateTargets", true).GetValue<Slider>().Value > 1)
                        {
                            var enemies =
                                HeroManager.Enemies.Where(e => e.IsValidTarget() && e.Distance(player) < 600)
                                    .Select(e => Prediction.GetPrediction(e, 0.35f));
                            var enemies2 =
                                HeroManager.Enemies.Where(e => e.IsValidTarget() && e.Distance(player) < 600)
                                    .Select(e => Prediction.GetPrediction(e, 0.95f));
                            if (detoneateTargetBarrels.Any())
                            {
                                foreach (var detoneateTargetBarrel in detoneateTargetBarrels)
                                {
                                    if (detoneateTargetBarrel.Health > 1)
                                    {
                                        continue;
                                    }
                                    var enemyCount =
                                        enemies.Count(
                                            e =>
                                                e.UnitPosition.Distance(detoneateTargetBarrel.Position) <
                                                BarrelExplosionRange);
                                    if (enemyCount >= config.Item("detoneateTargets", true).GetValue<Slider>().Value &&
                                        detoneateTargetBarrel.CountEnemiesInRange(BarrelExplosionRange) >=
                                        config.Item("detoneateTargets", true).GetValue<Slider>().Value)
                                    {
                                        Q.CastOnUnit(detoneateTargetBarrel, config.Item("packets").GetValue<bool>());
                                        return;
                                    }
                                    var detoneateTargetBarrelSeconds =
                                        barrels.Where(b => b.Distance(detoneateTargetBarrel) < BarrelConnectionRange);
                                    if (detoneateTargetBarrelSeconds.Any())
                                    {
                                        foreach (var detoneateTargetBarrelSecond in detoneateTargetBarrelSeconds)
                                        {
                                            if (enemyCount +
                                                enemies2.Count(
                                                    e =>
                                                        e.UnitPosition.Distance(detoneateTargetBarrelSecond.Position) <
                                                        BarrelExplosionRange) >=
                                                config.Item("detoneateTargets", true).GetValue<Slider>().Value &&
                                                detoneateTargetBarrelSecond.CountEnemiesInRange(BarrelExplosionRange) >=
                                                config.Item("detoneateTargets", true).GetValue<Slider>().Value)
                                            {
                                                Q.CastOnUnit(
                                                    detoneateTargetBarrel, config.Item("packets").GetValue<bool>());
                                                return;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                if (config.Item("useq", true).GetValue<bool>() && Q.CanCast(target) && Orbwalking.CanMove(100) && !justE)
                {
                    Q.CastOnUnit(target, config.Item("packets").GetValue<bool>());
                }
            }
        }

        private void CastE(Obj_AI_Hero target, IEnumerable<Obj_AI_Base> barrels)
        {
            if (!barrels.Any())
            {
                CastEtarget(target);

                return;
            }

            var enemies =
                HeroManager.Enemies.Where(e => e.IsValidTarget() && e.Distance(player) < 600)
                    .Select(e => Prediction.GetPrediction(e, 0.35f));
            List<Vector3> points = new List<Vector3>();
            foreach (var barrel in
                barrels.Where(b => b.Distance(player) < Q.Range && b.Health < 2))
            {
                points.AddRange(GetBarrelPoints(barrel.Position).Where(p => p.Distance(player.Position) < E.Range));
            }
            var bestPoint =
                points.Where(b => enemies.Count(e => e.UnitPosition.Distance(b) < BarrelExplosionRange) > 0)
                    .OrderByDescending(b => enemies.Count(e => e.UnitPosition.Distance(b) < BarrelExplosionRange))
                    .FirstOrDefault();
            if (bestPoint.IsValid() && bestPoint.Distance(ePos) > 400)
            {
                E.Cast(bestPoint, config.Item("packets").GetValue<bool>());
            }
            else
            {
                CastEtarget(target);
            }
        }

        private void CastEtarget(Obj_AI_Hero target)
        {
            var ePred = E.GetPrediction(target);
            if (ePred.CastPosition.Distance(ePos) > 400 && !justE)
            {
                E.Cast(ePred.CastPosition, config.Item("packets").GetValue<bool>());
            }
        }

        private void Game_OnDraw(EventArgs args)
        {
            DrawHelper.DrawCircle(config.Item("drawqq", true).GetValue<Circle>(), Q.Range);
            DrawHelper.DrawCircle(config.Item("drawee", true).GetValue<Circle>(), E.Range);
            Helpers.Jungle.ShowSmiteStatus(
                config.Item("useSmite").GetValue<KeyBind>().Active, config.Item("smiteStatus").GetValue<bool>());
            Utility.HpBarDamageIndicator.Enabled = config.Item("drawcombo", true).GetValue<bool>();
        }

        private static float ComboDamage(Obj_AI_Hero hero)
        {
            double damage = 0;
            if (Q.IsReady())
            {
                damage += Damage.GetSpellDamage(player, hero, SpellSlot.Q);
            }
            if (E.IsReady())
            {
                damage += Damage.GetSpellDamage(player, hero, SpellSlot.E);
            }
            if (R.IsReady())
            {
                damage += Damage.GetSpellDamage(player, hero, SpellSlot.R);
            }
            //damage += ItemHandler.GetItemsDamage(hero);
            var ignitedmg = player.GetSummonerSpellDamage(hero, Damage.SummonerSpell.Ignite);
            if (player.Spellbook.CanUseSpell(player.GetSpellSlot("summonerdot")) == SpellState.Ready &&
                hero.Health < damage + ignitedmg)
            {
                damage += ignitedmg;
            }
            return (float) damage;
        }

        private void Game_ProcessSpell(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (sender.IsMe)
            {
                if (args.SData.Name == "GangplankQWrapper")
                {
                    if (!justQ)
                    {
                        justQ = true;
                        Utility.DelayAction.Add(200, () => justQ = false);
                    }
                }
                if (args.SData.Name == "GangplankE")
                {
                    ePos = args.End;
                    if (!justE)
                    {
                        justE = true;
                        Utility.DelayAction.Add(350, () => justE = false);
                    }
                }
            }
        }


        private List<Vector3> GetBarrelPoints(Vector3 point)
        {
            return CombatHelper.PointsAroundTheTargetOuterRing(point, BarrelConnectionRange, 20f);
        }

        private float getEActivationDelay()
        {
            if (player.Level >= 13)
            {
                return 0.5f * 2;
            }
            if (player.Level >= 7)
            {
                return 1f * 2;
            }
            return 2f * 2;
        }


        private void InitMenu()
        {
            config = new Menu("Gangplank ", "Gangplank", true);
            // Target Selector
            Menu menuTS = new Menu("Selector", "tselect");
            TargetSelector.AddToMenu(menuTS);
            config.AddSubMenu(menuTS);
            // Orbwalker
            Menu menuOrb = new Menu("Orbwalker", "orbwalker");
            orbwalker = new Orbwalking.Orbwalker(menuOrb);
            config.AddSubMenu(menuOrb);
            // Draw settings
            Menu menuD = new Menu("Drawings ", "dsettings");
            menuD.AddItem(new MenuItem("drawqq", "Draw Q range", true))
                .SetValue(new Circle(false, Color.FromArgb(180, 100, 146, 166)));
            menuD.AddItem(new MenuItem("drawee", "Draw E range", true))
                .SetValue(new Circle(false, Color.FromArgb(180, 100, 146, 166)));
            menuD.AddItem(new MenuItem("drawcombo", "Draw combo damage", true)).SetValue(true);
            config.AddSubMenu(menuD);
            // Combo Settings
            Menu menuC = new Menu("Combo ", "csettings");
            menuC.AddItem(new MenuItem("useq", "Use Q", true)).SetValue(true);
            menuC.AddItem(new MenuItem("detoneateTarget", "   Blow up target with E", true)).SetValue(true);
            menuC.AddItem(new MenuItem("detoneateTargets", "   Blow up enemies with E", true))
                .SetValue(new Slider(2, 1, 5));
            menuC.AddItem(new MenuItem("usew", "Use W under health", true)).SetValue(new Slider(20, 0, 100));
            menuC.AddItem(new MenuItem("usee", "Use E", true)).SetValue(true);
            menuC.AddItem(new MenuItem("eStacksC", "   Keep stacks", true)).SetValue(new Slider(0, 0, 5));
            menuC.AddItem(new MenuItem("user", "Use R", true)).SetValue(true);
            menuC.AddItem(new MenuItem("Rmin", "   R min", true)).SetValue(new Slider(2, 1, 5));
            menuC.AddItem(new MenuItem("useIgnite", "Use Ignite", true)).SetValue(true);
            menuC = ItemHandler.addItemOptons(menuC);
            config.AddSubMenu(menuC);
            // Harass Settings
            Menu menuH = new Menu("Harass ", "Hsettings");
            menuH.AddItem(new MenuItem("useqH", "Use Q harass", true)).SetValue(true);
            menuH.AddItem(new MenuItem("useqLHH", "Use Q lasthit", true)).SetValue(true);
            menuH.AddItem(new MenuItem("useeH", "Use E", true)).SetValue(true);
            menuH.AddItem(new MenuItem("eStacksH", "   Keep stacks", true)).SetValue(new Slider(0, 0, 5));
            menuH.AddItem(new MenuItem("minmanaH", "Keep X% mana", true)).SetValue(new Slider(1, 1, 100));
            config.AddSubMenu(menuH);
            // LaneClear Settings
            Menu menuLC = new Menu("LaneClear ", "Lcsettings");
            menuLC.AddItem(new MenuItem("useqLC", "Use Q", true)).SetValue(true);
            menuLC.AddItem(new MenuItem("useeLC", "Use E", true)).SetValue(true);
            menuLC.AddItem(new MenuItem("eMinHit", "   Min hit", true)).SetValue(new Slider(3, 1, 6));
            menuLC.AddItem(new MenuItem("eStacksLC", "   Keep stacks", true)).SetValue(new Slider(0, 0, 5));
            menuLC.AddItem(new MenuItem("minmana", "Keep X% mana", true)).SetValue(new Slider(1, 1, 100));
            config.AddSubMenu(menuLC);
            Menu menuM = new Menu("Misc ", "Msettings");
            menuM.AddItem(new MenuItem("AutoR", "Cast R to get assists", true)).SetValue(false);
            menuM.AddItem(new MenuItem("AutoW", "W with QSS options", true)).SetValue(true);
            menuM = Jungle.addJungleOptions(menuM);

            Menu autolvlM = new Menu("AutoLevel", "AutoLevel");
            autoLeveler = new AutoLeveler(autolvlM);
            menuM.AddSubMenu(autolvlM);
            config.AddSubMenu(menuM);
            config.AddItem(new MenuItem("packets", "Use Packets")).SetValue(false);
            config.AddItem(new MenuItem("UnderratedAIO", "by Soresu v" + Program.version.ToString().Replace(",", ".")));
            config.AddToMainMenu();
        }
    }
}