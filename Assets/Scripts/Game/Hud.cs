using System;
using System.Collections.Generic;
using Alba.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Alba.Game
{
    /// <summary>One line of a menu: a label, an optional value, and what choosing or nudging it does.</summary>
    public sealed class MenuItem
    {
        public string Label;
        public Func<string> Value;
        public Action Activate;
        /// <summary>Left/right on the item (-1 or +1); null when it has no value to change.</summary>
        public Action<int> Adjust;
        public bool Enabled = true;
    }

    /// <summary>A menu or story card: a title, optional text, and items chosen with keys, pad or mouse.</summary>
    public sealed class MenuPage
    {
        public string Id;
        public string Kicker;
        public string Title;
        public string Subtitle;
        public string Body;
        public string Footer;
        public bool Logo;
        public bool Wide;
        public Action Back;
        public readonly List<MenuItem> Items = new List<MenuItem>();

        public MenuItem Add(string label, Action activate, Func<string> value = null, Action<int> adjust = null, bool enabled = true)
        {
            var item = new MenuItem { Label = label, Activate = activate, Value = value, Adjust = adjust, Enabled = enabled };
            Items.Add(item);
            return item;
        }
    }

    /// <summary>
    /// The UI Toolkit overlay, built in code and styled by Assets/UI/Hud.uss: health, flask, torch,
    /// weapons, score and sealstones while playing; banners and toasts; and every menu and story card.
    /// Items never take keyboard focus; the director reads the keys and moves the highlight itself.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class Hud : MonoBehaviour
    {
        VisualElement root;
        VisualElement play;
        VisualElement healthFill, staminaFill, batteryFill, bossBox, bossFill, dawnBox, dawnFill;
        Label potions, score, orbs, gems, objective, batteryLabel;
        VisualElement lives;
        readonly Label[] weaponRows = new Label[4];
        Label banner, bannerSub, toast;
        VisualElement bannerBox;
        VisualElement hurt, fade, crosshair;
        VisualElement menuLayer, menuPanel, logo, menuItems;
        Label menuKicker, menuTitle, menuSubtitle, menuBody, menuFooter;
        readonly List<Label> itemLabels = new List<Label>();
        MenuPage page;
        float bannerTime, toastTime, hurtLevel;
        float fadeLevel, fadeTarget;
        int shownLives = -1;

        public int Selected { get; private set; }
        public MenuPage Page => page;
        public bool MenuOpen => page != null;
        public string BannerText => banner?.text;
        public string ToastText => toast?.text;
        public string ObjectiveText => objective?.text;

        public event Action<int> ItemClicked;

        public void Build()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();
            root.AddToClassList("hud");
            root.pickingMode = PickingMode.Ignore;

            hurt = Layer("hurt");
            play = Layer("play");
            crosshair = Box(play, "crosshair");
            Box(crosshair, "crosshair__dot");

            VisualElement status = Box(play, "panel panel--status");
            VisualElement healthRow = Box(status, "row");
            Text(healthRow, "caption", "HEALTH");
            lives = Box(healthRow, "lives");
            VisualElement healthTrack = Box(status, "bar bar--health");
            healthFill = Box(healthTrack, "bar__fill bar__fill--health");
            VisualElement staminaTrack = Box(status, "bar bar--stamina");
            staminaFill = Box(staminaTrack, "bar__fill bar__fill--stamina");
            VisualElement flaskRow = Box(status, "row row--gap");
            Box(flaskRow, "icon icon--potion");
            potions = Text(flaskRow, "value", "1");
            batteryLabel = Text(flaskRow, "caption caption--torch", "TORCH");
            VisualElement batteryTrack = Box(flaskRow, "bar bar--battery");
            batteryFill = Box(batteryTrack, "bar__fill bar__fill--battery");

            VisualElement tally = Box(play, "panel panel--tally");
            score = Text(tally, "display score", "0");
            VisualElement orbRow = Box(tally, "row row--end");
            Text(orbRow, "caption", "ORBS");
            orbs = Text(orbRow, "value value--orb", "0");
            Box(orbRow, "icon icon--orb");
            VisualElement gemRow = Box(tally, "row row--end");
            Text(gemRow, "caption", "SEALSTONES");
            gems = Text(gemRow, "value value--gem", "0/0");
            Box(gemRow, "icon icon--gem");

            VisualElement arms = Box(play, "panel panel--weapons");
            for (int i = 0; i < weaponRows.Length; i++) weaponRows[i] = Text(arms, "weapon", "");

            VisualElement top = Box(play, "top");
            objective = Text(top, "objective", "");
            bossBox = Box(top, "boss hidden");
            Text(bossBox, "boss__name", "AM FOMHAIR");
            bossFill = Box(Box(bossBox, "boss__track"), "boss__fill");
            dawnBox = Box(top, "dawn hidden");
            dawnFill = Box(Box(dawnBox, "dawn__track"), "dawn__fill");

            toast = Text(play, "toast hidden", "");

            bannerBox = Box(root, "banner hidden");
            banner = Text(bannerBox, "display banner__title", "");
            bannerSub = Text(bannerBox, "banner__sub", "");

            fade = Layer("fade");

            menuLayer = Layer("menu-layer hidden");
            menuLayer.pickingMode = PickingMode.Position;
            menuPanel = Box(menuLayer, "menu");
            logo = Box(menuPanel, "logo");
            Text(logo, "display logo__word", "ALBA");
            Text(logo, "logo__sub", "SECRET HISTORY");
            menuKicker = Text(menuPanel, "menu__kicker", "");
            menuTitle = Text(menuPanel, "display menu__title", "");
            menuSubtitle = Text(menuPanel, "menu__subtitle", "");
            menuBody = Text(menuPanel, "menu__body", "");
            menuItems = Box(menuPanel, "menu__items");
            menuFooter = Text(menuPanel, "menu__footer", "");
        }

        // ---- Play HUD ----

        public void ShowPlay(bool visible) => play.EnableInClassList("hidden", !visible);

        public void UpdatePlay(Run run, string objectiveText, bool gamepad)
        {
            Stage stage = run.Stage;
            Player player = stage.Player;
            healthFill.style.width = Length.Percent(100f * player.Health / Tuning.MaxHealth);
            healthFill.EnableInClassList("bar__fill--low", player.Health <= 30);
            staminaFill.style.width = Length.Percent(100f * player.Stamina);
            staminaFill.EnableInClassList("bar__fill--winded", player.Winded);
            batteryFill.style.width = Length.Percent(100f * player.Battery);
            batteryFill.EnableInClassList("bar__fill--off", !player.LightOn);
            batteryLabel.text = player.LightOn ? "TORCH" : "TORCH OFF";
            potions.text = player.Potions.ToString();
            score.text = run.Score.ToString("N0");
            orbs.text = stage.OrbsFound.ToString();
            gems.text = $"{stage.GemsFound}/{stage.GemsTotal}";
            gems.EnableInClassList("value--done", stage.GemsTotal > 0 && stage.GemsFound >= stage.GemsTotal);
            gems.parent.EnableInClassList("hidden", stage.GemsTotal == 0);
            if (shownLives != run.Lives)
            {
                shownLives = run.Lives;
                lives.Clear();
                for (int i = 0; i < Tuning.StartingLives; i++) Box(lives, i < run.Lives ? "life" : "life life--lost");
            }

            Arsenal arsenal = player.Arsenal;
            for (int i = 0; i < weaponRows.Length; i++)
            {
                WeaponSpec spec = WeaponSpec.All[i];
                string ammo = spec.UsesAmmo ? arsenal.Ammo(spec.Kind).ToString() : "-";
                weaponRows[i].text = $"{(gamepad ? "" : (i + 1) + "  ")}{spec.Name.ToUpperInvariant()}   {ammo}";
                weaponRows[i].EnableInClassList("weapon--current", arsenal.Current == spec.Kind);
                weaponRows[i].EnableInClassList("weapon--empty", spec.UsesAmmo && arsenal.Ammo(spec.Kind) == 0);
            }

            objective.text = objectiveText;
            Enemy boss = stage.Boss;
            bool fight = boss != null && boss.Alive && boss.Hostile;
            bossBox.EnableInClassList("hidden", !fight);
            if (fight) bossFill.style.width = Length.Percent(100f * boss.Health / boss.Spec.Health);
            bool survival = stage.SurvivalTotal > 0f;
            dawnBox.EnableInClassList("hidden", !survival || stage.DawnBroke);
            if (survival) dawnFill.style.width = Length.Percent(100f * stage.DawnProgress);
        }

        public void Hurt(float amount) => hurtLevel = Mathf.Max(hurtLevel, amount);

        public void Banner(string title, string sub, float seconds = 2.4f)
        {
            banner.text = title;
            bannerSub.text = sub ?? "";
            bannerSub.EnableInClassList("hidden", string.IsNullOrEmpty(sub));
            bannerBox.RemoveFromClassList("hidden");
            bannerTime = seconds;
        }

        public void HideBanner()
        {
            bannerTime = 0f;
            bannerBox.AddToClassList("hidden");
        }

        public void Toast(string text, float seconds = 2.6f)
        {
            toast.text = text;
            toast.RemoveFromClassList("hidden");
            toastTime = seconds;
        }

        /// <summary>Fades the screen to black (1) or back (0).</summary>
        public void FadeTo(float level, bool instant = false)
        {
            fadeTarget = level;
            if (instant) fadeLevel = level;
        }

        public void Tick(float dt)
        {
            if (root == null) return;
            if (bannerTime > 0f)
            {
                bannerTime -= dt;
                bannerBox.style.opacity = Mathf.Clamp01(bannerTime / 0.5f);
                if (bannerTime <= 0f) bannerBox.AddToClassList("hidden");
            }
            if (toastTime > 0f)
            {
                toastTime -= dt;
                toast.style.opacity = Mathf.Clamp01(toastTime / 0.4f);
                if (toastTime <= 0f) toast.AddToClassList("hidden");
            }
            hurtLevel = Mathf.Max(0f, hurtLevel - dt * 1.6f);
            hurt.style.opacity = hurtLevel;
            fadeLevel = Mathf.MoveTowards(fadeLevel, fadeTarget, dt * 1.5f);
            fade.style.opacity = fadeLevel;
        }

        // ---- Menus ----

        public void ShowMenu(MenuPage newPage, int selected = 0)
        {
            page = newPage;
            menuLayer.RemoveFromClassList("hidden");
            SetText(menuKicker, page.Kicker);
            SetText(menuTitle, page.Title);
            SetText(menuSubtitle, page.Subtitle);
            SetText(menuBody, page.Body);
            SetText(menuFooter, page.Footer);
            logo.EnableInClassList("hidden", !page.Logo);
            menuTitle.EnableInClassList("hidden", page.Logo || string.IsNullOrEmpty(page.Title));
            menuPanel.EnableInClassList("menu--title", page.Logo);
            menuPanel.EnableInClassList("menu--wide", page.Wide);
            menuItems.Clear();
            itemLabels.Clear();
            for (int i = 0; i < page.Items.Count; i++)
            {
                int index = i;
                var label = new Label();
                label.AddToClassList("menu__item");
                label.EnableInClassList("menu__item--disabled", !page.Items[i].Enabled);
                label.RegisterCallback<ClickEvent>(_ => Click(index));
                label.RegisterCallback<PointerEnterEvent>(_ =>
                {
                    if (page != null && index < page.Items.Count && page.Items[index].Enabled) Select(index);
                });
                menuItems.Add(label);
                itemLabels.Add(label);
            }
            Selected = Mathf.Clamp(selected, 0, Mathf.Max(0, page.Items.Count - 1));
            if (page.Items.Count > 0 && !page.Items[Selected].Enabled) Move(1);
            Refresh();
        }

        static void SetText(Label label, string text)
        {
            label.text = text ?? "";
            label.EnableInClassList("hidden", string.IsNullOrEmpty(text));
        }

        public void HideMenu()
        {
            page = null;
            menuLayer.AddToClassList("hidden");
        }

        /// <summary>Moves the highlight up (-1) or down (+1), skipping disabled items.</summary>
        public void Move(int step)
        {
            if (page == null || page.Items.Count == 0) return;
            int next = Selected;
            for (int i = 0; i < page.Items.Count; i++)
            {
                next = (next + step + page.Items.Count) % page.Items.Count;
                if (page.Items[next].Enabled) break;
            }
            Select(next);
        }

        public void Select(int index)
        {
            Selected = index;
            Refresh();
        }

        /// <summary>Refreshes item text (values change as settings are nudged).</summary>
        public void Refresh()
        {
            if (page == null) return;
            for (int i = 0; i < itemLabels.Count; i++)
            {
                MenuItem item = page.Items[i];
                string value = item.Value?.Invoke();
                bool chosen = i == Selected;
                string shown = string.IsNullOrEmpty(value) ? "" : "   " + (item.Adjust != null && chosen ? "< " + value + " >" : value);
                itemLabels[i].text = item.Label + shown;
                itemLabels[i].EnableInClassList("menu__item--selected", chosen);
            }
        }

        void Click(int index)
        {
            if (page == null || index >= page.Items.Count || !page.Items[index].Enabled) return;
            Select(index);
            ItemClicked?.Invoke(index);
        }

        // ---- Helpers ----

        VisualElement Layer(string classes)
        {
            VisualElement layer = Box(root, "layer " + classes);
            layer.pickingMode = PickingMode.Ignore;
            return layer;
        }

        static VisualElement Box(VisualElement parent, string classes)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            foreach (string c in classes.Split(' ')) element.AddToClassList(c);
            parent.Add(element);
            return element;
        }

        static Label Text(VisualElement parent, string classes, string text)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            foreach (string c in classes.Split(' ')) label.AddToClassList(c);
            parent.Add(label);
            return label;
        }
    }
}
