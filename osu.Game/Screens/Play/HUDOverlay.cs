﻿// Copyright (c) 2007-2018 ppy Pty Ltd <contact@ppy.sh>.
// Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu/master/LICENCE

using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Play.HUD;
using osuTK;
using osuTK.Input;

namespace osu.Game.Screens.Play
{
    public class HUDOverlay : Container
    {
        private const int duration = 100;

        public readonly KeyCounterCollection KeyCounter;
        public readonly RollingCounter<int> ComboCounter;
        public readonly ScoreCounter ScoreCounter;
        public readonly RollingCounter<double> AccuracyCounter;
        public readonly HealthDisplay HealthDisplay;
        public readonly SongProgress Progress;
        public readonly ModDisplay ModDisplay;
        public readonly HoldForMenuButton HoldToQuit;
        public readonly PlayerSettingsOverlay PlayerSettingsOverlay;

        private Bindable<bool> showHud;
        private readonly Container visibilityContainer;
        private readonly BindableBool replayLoaded = new BindableBool();

        private static bool hasShownNotificationOnce;

        public HUDOverlay(ScoreProcessor scoreProcessor, RulesetContainer rulesetContainer, WorkingBeatmap working, IClock offsetClock, IAdjustableClock adjustableClock)
        {
            RelativeSizeAxes = Axes.Both;

            Children = new Drawable[]
            {
                visibilityContainer = new Container {
                    RelativeSizeAxes = Axes.Both,
                    AlwaysPresent = true,    // The hud may be hidden but certain elements may need to still be updated
                    Children = new  Drawable[] {
                        ComboCounter = CreateComboCounter(),
                        ScoreCounter = CreateScoreCounter(),
                        AccuracyCounter = CreateAccuracyCounter(),
                        HealthDisplay = CreateHealthDisplay(),
                        Progress = CreateProgress(),
                        ModDisplay = CreateModsContainer(),
                        PlayerSettingsOverlay = CreatePlayerSettingsOverlay(),
                    }
                },
                new FillFlowContainer
                {
                    Anchor = Anchor.BottomRight,
                    Origin = Anchor.BottomRight,
                    Position = -new Vector2(5, TwoLayerButton.SIZE_RETRACTED.Y),
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Children = new Drawable[]
                    {
                        KeyCounter = CreateKeyCounter(adjustableClock as IFrameBasedClock),
                        HoldToQuit = CreateHoldForMenuButton(),
                    }
                }
            };

            BindProcessor(scoreProcessor);
            BindRulesetContainer(rulesetContainer);

            Progress.Objects = rulesetContainer.Objects;
            Progress.AudioClock = offsetClock;
            Progress.AllowSeeking = rulesetContainer.HasReplayLoaded;
            Progress.OnSeek = pos => adjustableClock.Seek(pos);

            ModDisplay.Current.BindTo(working.Mods);

            PlayerSettingsOverlay.PlaybackSettings.AdjustableClock = adjustableClock;
        }

        [BackgroundDependencyLoader(true)]
        private void load(OsuConfigManager config, NotificationOverlay notificationOverlay)
        {
            showHud = config.GetBindable<bool>(OsuSetting.ShowInterface);
            showHud.ValueChanged += hudVisibility => visibilityContainer.FadeTo(hudVisibility ? 1 : 0, duration);
            showHud.TriggerChange();

            if (!showHud && !hasShownNotificationOnce)
            {
                hasShownNotificationOnce = true;

                notificationOverlay?.Post(new SimpleNotification
                {
                    Text = @"The score overlay is currently disabled. You can toggle this by pressing Shift+Tab."
                });
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            replayLoaded.ValueChanged += replayLoadedValueChanged;
            replayLoaded.TriggerChange();
        }

        private void replayLoadedValueChanged(bool loaded)
        {
            PlayerSettingsOverlay.ReplayLoaded = loaded;

            if (loaded)
            {
                PlayerSettingsOverlay.Show();
                ModDisplay.FadeIn(200);
                KeyCounter.Margin = new MarginPadding(10) { Bottom = 30 };
            }
            else
            {
                PlayerSettingsOverlay.Hide();
                ModDisplay.Delay(2000).FadeOut(200);
                KeyCounter.Margin = new MarginPadding(10);
            }
        }

        protected virtual void BindRulesetContainer(RulesetContainer rulesetContainer)
        {
            (rulesetContainer.KeyBindingInputManager as ICanAttachKeyCounter)?.Attach(KeyCounter);

            replayLoaded.BindTo(rulesetContainer.HasReplayLoaded);

            Progress.BindRulestContainer(rulesetContainer);
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (e.Repeat) return false;

            if (e.ShiftPressed)
            {
                switch (e.Key)
                {
                    case Key.Tab:
                        showHud.Value = !showHud.Value;
                        return true;
                }
            }

            return base.OnKeyDown(e);
        }

        protected virtual RollingCounter<double> CreateAccuracyCounter() => new PercentageCounter
        {
            Anchor = Anchor.TopCentre,
            Origin = Anchor.TopRight,
            Position = new Vector2(0, 35),
            TextSize = 20,
            Margin = new MarginPadding { Right = 140 },
        };

        protected virtual RollingCounter<int> CreateComboCounter() => new SimpleComboCounter
        {
            Anchor = Anchor.TopCentre,
            Origin = Anchor.TopLeft,
            Position = new Vector2(0, 35),
            Margin = new MarginPadding { Left = 140 },
            TextSize = 20,
        };

        protected virtual HealthDisplay CreateHealthDisplay() => new StandardHealthDisplay
        {
            Size = new Vector2(1, 5),
            RelativeSizeAxes = Axes.X,
            Margin = new MarginPadding { Top = 20 }
        };

        protected virtual KeyCounterCollection CreateKeyCounter(IFrameBasedClock offsetClock) => new KeyCounterCollection
        {
            FadeTime = 50,
            Anchor = Anchor.BottomRight,
            Origin = Anchor.BottomRight,
            Margin = new MarginPadding(10),
            AudioClock = offsetClock
        };

        protected virtual ScoreCounter CreateScoreCounter() => new ScoreCounter(6)
        {
            Anchor = Anchor.TopCentre,
            Origin = Anchor.TopCentre,
            TextSize = 40,
            Position = new Vector2(0, 30),
        };

        protected virtual SongProgress CreateProgress() => new SongProgress
        {
            Anchor = Anchor.BottomLeft,
            Origin = Anchor.BottomLeft,
            RelativeSizeAxes = Axes.X,
        };

        protected virtual HoldForMenuButton CreateHoldForMenuButton() => new HoldForMenuButton
        {
            Anchor = Anchor.BottomRight,
            Origin = Anchor.BottomRight,
        };

        protected virtual ModDisplay CreateModsContainer() => new ModDisplay
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            AutoSizeAxes = Axes.Both,
            Margin = new MarginPadding { Top = 20, Right = 10 },
        };

        protected virtual PlayerSettingsOverlay CreatePlayerSettingsOverlay() => new PlayerSettingsOverlay();

        protected virtual void BindProcessor(ScoreProcessor processor)
        {
            ScoreCounter?.Current.BindTo(processor.TotalScore);
            AccuracyCounter?.Current.BindTo(processor.Accuracy);
            ComboCounter?.Current.BindTo(processor.Combo);
            HealthDisplay?.Current.BindTo(processor.Health);

            var shd = HealthDisplay as StandardHealthDisplay;
            if (shd != null)
                processor.NewJudgement += shd.Flash;
        }
    }
}
