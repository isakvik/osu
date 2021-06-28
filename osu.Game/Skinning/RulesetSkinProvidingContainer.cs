// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using JetBrains.Annotations;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.IO.Stores;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.UI;

namespace osu.Game.Skinning
{
    /// <summary>
    /// A type of <see cref="SkinProvidingContainer"/> specialized for <see cref="DrawableRuleset"/> and other gameplay-related components.
    /// Providing access to parent skin sources and the beatmap skin each surrounded with the ruleset legacy skin transformer.
    /// </summary>
    public class RulesetSkinProvidingContainer : SkinProvidingContainer
    {
        protected readonly Ruleset Ruleset;
        protected readonly IBeatmap Beatmap;

        /// <remarks>
        /// This container already re-exposes all parent <see cref="ISkinSource"/> sources in a ruleset-usable form.
        /// Therefore disallow falling back to any parent <see cref="ISkinSource"/> any further.
        /// </remarks>
        protected override bool AllowFallingBackToParent => false;

        protected override Container<Drawable> Content { get; }

        public RulesetSkinProvidingContainer(Ruleset ruleset, IBeatmap beatmap, [CanBeNull] ISkin beatmapSkin)
        {
            Ruleset = ruleset;
            Beatmap = beatmap;

            InternalChild = new BeatmapSkinProvidingContainer(beatmapSkin is LegacySkin ? GetLegacyRulesetTransformedSkin(beatmapSkin) : beatmapSkin)
            {
                Child = Content = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                }
            };
        }

        [Resolved]
        private GameHost host { get; set; }

        [Resolved]
        private AudioManager audio { get; set; }

        [Resolved]
        private SkinManager skinManager { get; set; }

        [Resolved]
        private ISkinSource skinSource { get; set; }

        [BackgroundDependencyLoader]
        private void load()
        {
            UpdateSkins();
            skinSource.SourceChanged += OnSourceChanged;
        }

        protected override void OnSourceChanged()
        {
            UpdateSkins();
            base.OnSourceChanged();
        }

        protected virtual void UpdateSkins()
        {
            foreach (var resourcesSkin in SkinSources.OfType<ResourcesSkin>())
                resourcesSkin.Dispose();

            SkinSources.Clear();

            foreach (var skin in skinSource.AllSources)
            {
                switch (skin)
                {
                    case LegacySkin legacySkin:
                        SkinSources.Add(GetLegacyRulesetTransformedSkin(legacySkin));
                        break;

                    default:
                        SkinSources.Add(skin);
                        break;
                }
            }

            if (Ruleset.CreateResourceStore() is IResourceStore<byte[]> resources)
            {
                int defaultSkinIndex = SkinSources.IndexOf(skinManager.DefaultSkin);

                var rulesetResources = new ResourcesSkin(resources, host, audio);

                if (defaultSkinIndex >= 0)
                    SkinSources.Insert(defaultSkinIndex, rulesetResources);
                else
                {
                    // Tests may potentially override the SkinManager with another source that doesn't include it in AllSources.
                    SkinSources.Add(rulesetResources);
                }
            }
        }

        protected ISkin GetLegacyRulesetTransformedSkin(ISkin legacySkin)
        {
            if (legacySkin == null)
                return null;

            var rulesetTransformed = Ruleset.CreateLegacySkinProvider(legacySkin, Beatmap);
            if (rulesetTransformed != null)
                return rulesetTransformed;

            return legacySkin;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (skinSource != null)
                skinSource.SourceChanged -= OnSourceChanged;
        }
    }
}
