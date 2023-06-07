﻿//
// Aurio: Audio Processing, Analysis and Retrieval Library
// Copyright (C) 2010-2017  Mario Guggenberger <mg@protyposis.net>
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
//
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Aurio.Project;
using Aurio.Streams;
using System.Diagnostics;
using Aurio.TaskMonitor;
using System.Threading.Tasks;

namespace Aurio.Matching
{
    public class Analysis
    {
        private AnalysisMode type;
        private TrackList<AudioTrack> audioTracks;
        private TimeSpan windowLength;
        private TimeSpan intervalLength;
        private int sampleRate;
        private ProgressMonitor progressMonitor;

        private unsafe Func<byte[], byte[], double> analyzeSection;

        public event EventHandler Started;
        public event EventHandler<AnalysisEventArgs> WindowAnalysed;
        public event EventHandler<AnalysisEventArgs> Finished;

        public Analysis(
            AnalysisMode type,
            TrackList<AudioTrack> audioTracks,
            TimeSpan windowLength,
            TimeSpan intervalLength,
            int sampleRate
        )
        {
            if (audioTracks.Count < 2)
            {
                // there must be at least 2 tracks, otherwise there's nothing to compare
                throw new Exception("there must be at least 2 audio tracks");
            }
            if (intervalLength < windowLength)
            {
                throw new ArgumentException(
                    "intervalLength must be at least as long as the windowLength"
                );
            }

            this.type = type;
            this.audioTracks = audioTracks;
            this.windowLength = windowLength;
            this.intervalLength = intervalLength;
            this.sampleRate = sampleRate;
            this.progressMonitor = ProgressMonitor.GlobalInstance;

            switch (type)
            {
                case AnalysisMode.CrossCorrelationOffset:
                    analyzeSection = AnalysisFunctions.CrossCorrelationOffset;
                    break;
                case AnalysisMode.Correlation:
                    analyzeSection = CrossCorrelation.Correlate;
                    break;
                case AnalysisMode.Interference:
                    analyzeSection = AnalysisFunctions.DestructiveInterference;
                    break;
                case AnalysisMode.FrequencyDistribution:
                    analyzeSection = AnalysisFunctions.FrequencyDistribution;
                    break;
                default:
                    throw new NotImplementedException(type + " not implemented");
            }
        }

        public Analysis(
            AnalysisMode type,
            TrackList<AudioTrack> audioTracks,
            TimeSpan windowLength,
            TimeSpan intervalLength,
            int sampleRate,
            ProgressMonitor progressMonitor
        )
            : this(type, audioTracks, windowLength, intervalLength, sampleRate)
        {
            this.progressMonitor = progressMonitor;
        }

        public void Execute()
        {
            Debug.WriteLine(
                "window length: {0}s, interval length: {1}s, sample rate: {2}",
                windowLength.TotalSeconds,
                intervalLength.TotalSeconds,
                sampleRate
            );
            IProgressReporter reporter = progressMonitor.BeginTask("Analyzing alignment...", true);

            List<IAudioStream> streams = new List<IAudioStream>(audioTracks.Count);
            TimeSpan start = audioTracks.Start;
            TimeSpan end = audioTracks.End;
            foreach (AudioTrack audioTrack in audioTracks)
            {
                streams.Add(
                    CrossCorrelation.PrepareStream(audioTrack.CreateAudioStream(), sampleRate)
                );
            }

            long[] streamOffsets = new long[audioTracks.Count];
            for (int i = 0; i < audioTracks.Count; i++)
            {
                streamOffsets[i] = TimeUtil.TimeSpanToBytes(
                    audioTracks[i].Offset - start,
                    streams[0].Properties
                );
            }

            int windowLengthInBytes = (int)
                TimeUtil.TimeSpanToBytes(windowLength, streams[0].Properties);
            int windowLengthInSamples =
                windowLengthInBytes / streams[0].Properties.SampleBlockByteSize;
            long intervalLengthInBytes = TimeUtil.TimeSpanToBytes(
                intervalLength,
                streams[0].Properties
            );
            long analysisIntervalLength = TimeUtil.TimeSpanToBytes(
                end - start,
                streams[0].Properties
            );

            OnStarted();

            byte[] x = new byte[windowLengthInBytes];
            byte[] y = new byte[windowLengthInBytes];
            long positionX;
            long positionY;
            double sumNegative = 0;
            double sumPositive = 0;
            int countNegative = 0;
            int countPositive = 0;
            double min = 0;
            double max = 0;

            for (
                long position = 0;
                position < analysisIntervalLength;
                position += intervalLengthInBytes
            )
            {
                double windowSumNegative = 0;
                double windowSumPositive = 0;
                int windowCountNegative = 0;
                int windowCountPositive = 0;
                double windowMin = 0;
                double windowMax = 0;

                Debug.WriteLine(
                    "Analyzing {0} @ {1} / {2}",
                    intervalLengthInBytes,
                    position,
                    analysisIntervalLength
                );
                // at each position in the analysis interval, compare each stream with each other
                for (int i = 0; i < streams.Count; i++)
                {
                    positionX = position - streamOffsets[i];
                    if (positionX >= 0 && positionX < streams[i].Length)
                    {
                        streams[i].Position = positionX;
                        StreamUtil.ForceRead(streams[i], x, 0, windowLengthInBytes);
                        for (int j = i + 1; j < streams.Count; j++)
                        {
                            positionY = position - streamOffsets[j];
                            if (positionY >= 0 && positionY < streams[j].Length)
                            {
                                streams[j].Position = positionY;
                                StreamUtil.ForceRead(streams[j], y, 0, windowLengthInBytes);

                                double val = analyzeSection(x, y);
                                if (val > 0)
                                {
                                    windowSumPositive += val;
                                    windowCountPositive++;
                                }
                                else
                                {
                                    windowSumNegative += val;
                                    windowCountNegative++;
                                }
                                if (windowMin > val)
                                {
                                    windowMin = val;
                                }
                                if (windowMax < val)
                                {
                                    windowMax = val;
                                }
                                Debug.WriteLine("{0,2}->{1,2}: {2}", i, j, val);
                            }
                        }
                    }
                }
                sumPositive += windowSumPositive;
                countPositive += windowCountPositive;
                sumNegative += windowSumNegative;
                countNegative += windowCountNegative;
                if (min > windowMin)
                {
                    min = windowMin;
                }
                if (max < windowMax)
                {
                    max = windowMax;
                }
                reporter.ReportProgress((double)position / analysisIntervalLength * 100);
                OnWindowAnalyzed(
                    start + TimeUtil.BytesToTimeSpan(position, streams[0].Properties),
                    windowCountPositive,
                    windowCountNegative,
                    windowMin,
                    windowMax,
                    windowSumPositive,
                    windowSumNegative
                );
            }

            reporter.Finish();
            Debug.WriteLine(
                "Finished. sum: {0}, sum+: {1}, sum-: {2}, sumAbs: {3}, avg: {4}, avg+: {5}, avg-: {6}, avgAbs: {7}, min: {8}, max: {9}, points: {10}",
                sumPositive + sumNegative,
                sumPositive,
                sumNegative,
                sumPositive + (sumNegative * -1),
                (sumPositive + sumNegative) / (countPositive + countNegative),
                sumPositive / countPositive,
                sumNegative / countNegative,
                (sumPositive + (sumNegative * -1)) / (countPositive + countNegative),
                min,
                max,
                countPositive + countNegative
            );
            double score = (sumPositive + (sumNegative * -1)) / (countPositive + countNegative);
            Debug.WriteLine("Score: {0} => {1}%", score, Math.Round(score * 100));

            OnFinished(countPositive, countNegative, min, max, sumPositive, sumNegative);

            streams.ForEach(s => s.Close());
        }

        public void ExecuteAsync()
        {
            Task.Factory.StartNew(() =>
            {
                Execute();
            });
        }

        private void OnStarted()
        {
            if (Started != null)
            {
                Started(this, EventArgs.Empty);
            }
        }

        private void OnWindowAnalyzed(
            TimeSpan time,
            int measurementPointsPositive,
            int measurementPointsNegative,
            double min,
            double max,
            double sumPositive,
            double sumNegative
        )
        {
            if (WindowAnalysed != null)
            {
                WindowAnalysed(
                    this,
                    new AnalysisEventArgs()
                    {
                        Time = time,
                        MeasurementPointsPositive = measurementPointsPositive,
                        MeasurementPointsNegative = measurementPointsNegative,
                        Min = min,
                        Max = max,
                        SumPositive = sumPositive,
                        SumNegative = sumNegative
                    }
                );
            }
        }

        private void OnFinished(
            int measurementPointsPositive,
            int measurementPointsNegative,
            double min,
            double max,
            double sumPositive,
            double sumNegative
        )
        {
            if (Finished != null)
            {
                Finished(
                    this,
                    new AnalysisEventArgs()
                    {
                        MeasurementPointsPositive = measurementPointsPositive,
                        MeasurementPointsNegative = measurementPointsNegative,
                        Min = min,
                        Max = max,
                        SumPositive = sumPositive,
                        SumNegative = sumNegative
                    }
                );
            }
        }
    }

    public class AnalysisEventArgs : EventArgs
    {
        public TimeSpan Time { get; set; }
        public int MeasurementPointsPositive { get; set; }
        public int MeasurementPointsNegative { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
        public double SumPositive { get; set; }
        public double SumNegative { get; set; }

        public int MeasurementPoints
        {
            get { return MeasurementPointsPositive + MeasurementPointsNegative; }
        }
        public double SumAbsolute
        {
            get { return SumPositive - SumNegative; }
        }
        public double AveragePositive
        {
            get
            {
                return MeasurementPointsPositive != 0 ? SumPositive / MeasurementPointsPositive : 0;
            }
        }
        public double AverageNegative
        {
            get
            {
                return MeasurementPointsNegative != 0 ? SumNegative / MeasurementPointsNegative : 0;
            }
        }
        public double AverageAbsolute
        {
            get { return MeasurementPoints != 0 ? SumAbsolute / MeasurementPoints : 0; }
        }
        public double Score
        {
            get { return MeasurementPoints != 0 ? SumAbsolute / MeasurementPoints : 0; }
        }
    }
}
