﻿using System;

namespace K4AdotNet.Samples.Core.BodyTrackingSpeed
{
    internal abstract class Processor : IDisposable
    {
        public static Processor Create(ProcessingParameters processingParameters) => processingParameters.Implementation switch
        {
            ProcessingImplementation.SingleThread => new SingleThreadProcessor(processingParameters),
            ProcessingImplementation.PopInBackground => new PopInBackgroundProcessor(processingParameters),
            ProcessingImplementation.EnqueueInBackground => new EnqueueInBackgroundProcessor(processingParameters),
            _ => throw new NotSupportedException(),
        };

        protected readonly ProcessingParameters processingParameters;
        protected readonly Record.Playback playback;
        protected readonly Record.RecordConfiguration recordConfig;
        protected readonly Sensor.Calibration calibration;
        protected readonly BodyTracking.Tracker tracker;

        protected Processor(ProcessingParameters processingParameters)
        {
            this.processingParameters = processingParameters;
            playback = new Record.Playback(processingParameters.MkvPath!);
            playback.GetRecordConfiguration(out recordConfig);
            RecordLength = playback.RecordLength;
            playback.GetCalibration(out calibration);
            if (processingParameters.StartTime.HasValue)
                Seek(processingParameters.StartTime.Value);
            var config = BodyTracking.TrackerConfiguration.Default;
            config.ProcessingMode = processingParameters.CpuOnlyMode
                ? BodyTracking.TrackerProcessingMode.Cpu
                : BodyTracking.TrackerProcessingMode.Gpu;
            tracker = new BodyTracking.Tracker(ref calibration, config);
        }

        public virtual void Dispose()
        {
            tracker.Dispose();
            playback.Dispose();
        }

        public Record.RecordConfiguration RecordConfig => recordConfig;

        public TimeSpan RecordLength { get; }

        public abstract int TotalFrameCount { get; }

        public abstract int FrameWithBodyCount { get; }

        public int QueueSize => tracker.QueueSize;

        public abstract bool NextFrame();

        private void Seek(TimeSpan value)
        {
            if (!playback.TrySeekTimestamp(value, Record.PlaybackSeekOrigin.Begin))
                throw new ApplicationException("Cannot seek playback to " + value);
        }

        protected bool IsCaptureInInterval(Sensor.Capture? capture)
        {
            if (capture == null)
                return false;
            if (!processingParameters.EndTime.HasValue)
                return true;
            var deviceTimestamp = GetDeviceTimestamp(capture);
            if (deviceTimestamp.HasValue)
                deviceTimestamp = deviceTimestamp.Value - RecordConfig.StartTimeOffset;
            return deviceTimestamp.HasValue
                && processingParameters.IsTimeInStartEndInterval(deviceTimestamp.Value);
        }

        private static Microseconds64? GetDeviceTimestamp(Sensor.Capture capture)
        {
            using (var image = capture.DepthImage)
            {
                return image?.DeviceTimestamp;
            }
        }
    }
}