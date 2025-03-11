// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Mods
{
    public class ManiaModInvert : Mod, IApplicableAfterBeatmapConversion
    {
        public override string Name => "Invert";

        public override string Acronym => "IN";
        public override double ScoreMultiplier => 1;

        public override LocalisableString Description => "Hold the keys. To the beat.";

        public override IconUsage? Icon => FontAwesome.Solid.YinYang;

        public override ModType Type => ModType.Conversion;

        public override Type[] IncompatibleMods => new[] { typeof(ManiaModHoldOff) };

        [SettingSource("Hold Gap Size", "The distance of the gap between holds denoted by a 1/n snap.")]
        public BindableNumber<int> HoldGapSize { get; } = new BindableInt(4)
        {
            Precision = 2,
            MinValue = 2,
            MaxValue = 16,
            Default = 4
        };

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            var maniaBeatmap = (ManiaBeatmap)beatmap;

            var newObjects = new List<ManiaHitObject>();

            foreach (var column in maniaBeatmap.HitObjects.GroupBy(h => h.Column))
            {
                var newColumnObjects = new List<ManiaHitObject>();

                var locations = column.OfType<Note>().Select(n => (startTime: n.StartTime, samples: n.Samples))
                                      .Concat(column.OfType<HoldNote>().Select(h => (startTime: h.StartTime, samples: h.GetNodeSamples(0))))
                                      .OrderBy(h => h.startTime).ToList();

                for (int i = 0; i < locations.Count - 1; i++)
                {
                    // Full duration of the hold note.
                    double duration = locations[i + 1].startTime - locations[i].startTime;

                    // Beat length at the end of the hold note.
                    double beatLength = beatmap.ControlPointInfo.TimingPointAt(locations[i + 1].startTime).BeatLength;

                    // Avoid creating holds shorter than some selected gap size values, replacing them with a note (e.g 1/8 holds with 1/4 gap size set).
                    // The -1 to the duration is due to slight differences in distances between notes and/or holds, this to avoid unwanted holds with the chosen gap size.
                    if (duration - 1 > (beatLength / HoldGapSize.Value))
                    {
                        // Decrease the duration according to the desired gap size.
                        duration = Math.Max(duration / 2, duration - beatLength / HoldGapSize.Value);

                        newColumnObjects.Add(new HoldNote
                        {
                            Column = column.Key,
                            StartTime = locations[i].startTime,
                            Duration = duration,
                            NodeSamples = new List<IList<HitSampleInfo>> { locations[i].samples, Array.Empty<HitSampleInfo>() }
                        });
                        continue;
                    }

                    newColumnObjects.Add(new Note
                    {
                        Column = column.Key,
                        StartTime = locations[i].startTime,
                        Samples = locations[i].samples,
                    });
                }

                newObjects.AddRange(newColumnObjects);
            }

            maniaBeatmap.HitObjects = newObjects.OrderBy(h => h.StartTime).ToList();

            // No breaks
            maniaBeatmap.Breaks.Clear();
        }
    }
}
