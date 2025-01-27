﻿using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using VRT.Core;
using Cwipc;
#if VRT_WITH_STATS
using Statistics = Cwipc.Statistics;
#endif


namespace VRT.UserRepresentation.Voice
{
    using Timestamp = System.Int64;
    using Timedelta = System.Int64;
    using QueueThreadSafe = Cwipc.QueueThreadSafe;
    using BaseMemoryChunk = Cwipc.BaseMemoryChunk;

    public class AsyncVoiceReader : AsyncWorker
    {
        const float extraWaitTime = 0.005f; // Schedule the next enumerator call 5ms after we expect the next audio frame to be available.
        Coroutine coroutine;
        QueueThreadSafe outQueue;

        public AsyncVoiceReader(string deviceName, int sampleRate, int fps, int minBufferSize, MonoBehaviour monoBehaviour, QueueThreadSafe _outQueue) : base()
        {
            NoUpdateCallsNeeded();
#if VRT_WITH_STATS
            stats = new Stats(Name());
#endif
            outQueue = _outQueue;
            device = deviceName;
            coroutine = monoBehaviour.StartCoroutine(MicroRecorder(deviceName, sampleRate, fps, minBufferSize));
            Debug.Log($"{Name()}: Started with bufferLength {nSamplesPerPacket} samples");
            Start();
        }

        public int getBufferSize()
        {
            return nSamplesPerPacket;
        }

        Timestamp sampleTimestamp(int nSamplesInInputBuffer)
        {
            System.TimeSpan sinceEpoch = System.DateTime.UtcNow - new System.DateTime(1970, 1, 1);
            double timestamp = sinceEpoch.TotalMilliseconds;
            timestamp -= (1000 * nSamplesInInputBuffer / wantedSampleRate);
            return (Timestamp)timestamp;
        }

        protected override void AsyncUpdate()
        {
        }

        public override void AsyncOnStop()
        {
            base.AsyncOnStop();
            outQueue.Close();
        }

        string device;
        int nSamplesInCircularBuffer;
        int nSamplesPerPacket;
        AudioClip recorder;

        int wantedSampleRate;
        

        IEnumerator MicroRecorder(string deviceName, int _sampleRate, int _fps, int _minBufferSize)
        {
            wantedSampleRate = _sampleRate;
            nSamplesPerPacket = wantedSampleRate / _fps;
            if (_minBufferSize > 0 && nSamplesPerPacket % _minBufferSize != 0)
            {
                // Round up to a multiple of _minBufferSize
                nSamplesPerPacket = ((nSamplesPerPacket + _minBufferSize - 1) / _minBufferSize) * _minBufferSize;
                float actualFps = (float)wantedSampleRate / nSamplesPerPacket;
                Debug.LogWarning($"{Name()}: adapted bufferSize={nSamplesPerPacket}, fps={actualFps}");
            }
            if (wantedSampleRate % nSamplesPerPacket != 0)
            {
                Debug.LogWarning($"{Name()}: non-integral number of buffers per second. This may not work.");
            }
            VoiceDspController.PrepareDSP(wantedSampleRate, nSamplesPerPacket);
            if (Microphone.devices.Length > 0)
            {
                if (deviceName == null || deviceName == "") deviceName = Microphone.devices[0];
#if XXXJACK_UNNEEDED
                int currentMinFreq;
                int currentMaxFreq;
                Microphone.GetDeviceCaps(deviceName, out currentMinFreq, out currentMaxFreq);
                // xxxjack should check whether we think the frequency is supported
#endif

                // We record a looping clip of 1 second.
                const int recorderBufferDuration = 1; 
                recorder = Microphone.Start(deviceName, true, recorderBufferDuration, wantedSampleRate);
                nSamplesInCircularBuffer = recorder.samples * recorder.channels;
                // We expect the recorder clip to contain an integral number of
                // buffers, because we are going to use it as a circular buffer.
                if (nSamplesInCircularBuffer % nSamplesPerPacket != 0)
                {
                    Debug.LogError($"VoiceReader: Incorrect clip size {nSamplesInCircularBuffer} for buffer size {nSamplesPerPacket}");
                }
                if (recorder.channels != 1)
                {
                    Debug.LogWarning("{Name()}: Microphone has {recorder.channels} channels, not supported");
                }
                if (nSamplesInCircularBuffer != wantedSampleRate)
                {
                    Debug.LogWarning($"VoiceReader: Microphone produces {nSamplesInCircularBuffer} samples per second, expected {wantedSampleRate}");
                }

                int nInputSamplesNeededPerPacket = nSamplesPerPacket;
                float[] readBuffer = new float[nInputSamplesNeededPerPacket];
                Debug.Log($"{Name()}: Using {deviceName}  Channels {recorder.channels} Frequency {nSamplesInCircularBuffer} bufferLength {nSamplesPerPacket} IsRecording {Microphone.IsRecording(deviceName)}");

                int readPosition = 0;

                while (true)
                {
                    //
                    // Get the position in the circular buffer where the DSP has just deposited a sample.
                    //
                    int writePosition = Microphone.GetPosition(deviceName);
                    //
                    // See how many samples are available in the circular buffer
                    //
                    int available;
                    if (writePosition < readPosition)
                    {
                        available = nSamplesInCircularBuffer - readPosition + writePosition;
                    }
                    else
                    {
                        available = writePosition - readPosition;
                    }
                    float timeRemainingInBuffer = (float)available / wantedSampleRate;
                    //
                    // If the microphone is not recording for some reason we restart it
                    //
                    if (!Microphone.IsRecording(deviceName))
                    {
                        Debug.LogWarning($"{Name()}: microphone {deviceName} stopped recording, starting again.");
                        recorder = Microphone.Start(deviceName, true, recorderBufferDuration, wantedSampleRate);
                        readPosition = 0;
                        writePosition = 0;
                        available = 0;
                        timeRemainingInBuffer = 0;
                    }
                    else
                    {
                        //
                        // Move all available packets from the circular buffer to our output queue.
                        //
                        while (available >= nInputSamplesNeededPerPacket)
                        {
                            if (!recorder.GetData(readBuffer, readPosition))
                            {
                                Debug.LogError($"{Name()}: Error getting audio from microphone");
                            }
                            //
                            // Write one packet from microphone.
                            lock (outQueue)
                            {
                                FloatMemoryChunk mc = new FloatMemoryChunk(nSamplesPerPacket);
                                _copyTo(readBuffer, mc.buffer);
                                //
                                // Update read position and number of available samples in the circular buffer
                                //
                                readPosition = (readPosition + nSamplesPerPacket) % nSamplesInCircularBuffer;
                                available -= nSamplesPerPacket;
                                //
                                // We need to compute timestamp of this audio frame
                                // by using system clock and adjusting with "available".
                                //
                                mc.metadata.timestamp = sampleTimestamp(available);
                                timeRemainingInBuffer = (float)available / wantedSampleRate;
                                bool ok = outQueue.Enqueue(mc);
#if VRT_WITH_STATS
                                stats.statsUpdate(timeRemainingInBuffer, nSamplesPerPacket, !ok, outQueue.QueuedDuration(), _calculateAveragePower(mc.buffer));
#endif
                            }
                        }
                    }
                    // Check when we expect the next audio frame to be available in the circular buffer.
                    float frameDuration = (float)nInputSamplesNeededPerPacket / wantedSampleRate;
                    float untilNextCall = frameDuration - timeRemainingInBuffer + extraWaitTime; // Add 5ms to forestall getting the callback just a little too early
                    //Debug.Log($"{Name()}: xxxjack frameDuration={frameDuration} inBuffer={timeRemainingInBuffer} untilNextCall={untilNextCall}");

                    yield return new WaitForSecondsRealtime(untilNextCall);
                }

                void _copyTo(float[] inBuffer, float[] outBuffer)
                {
                    for (int i = 0; i < nSamplesPerPacket; i++)
                    {
                        outBuffer[i] = inBuffer[i];
                    }
                }

                double _calculateAveragePower(float[] buffer)
                {
                    double powerSum = 0;
                    foreach(float x in buffer)
                    {
                        powerSum += x*x;
                    }
                    return powerSum / buffer.Length;
                }
            }
            else
                Debug.LogError("{Name()}: No Microphones detected.");
        }

#if VRT_WITH_STATS
        protected class Stats : Statistics
        {
            public Stats(string name) : base(name) { }

            double statsTotalUpdates;
            double statsTotalSamples;
            double statsTotalTimeInInputBuffer;
            double statsTotalQueuedDuration;
            double statsDrops;
            double statsTotalAveragePower;

            public void statsUpdate(double timeInInputBuffer, int sampleCount, bool dropped, Timedelta queuedDuration, double averagePower)
            {

                statsTotalUpdates += 1;
                statsTotalSamples += sampleCount;
                statsTotalTimeInInputBuffer += timeInInputBuffer;
                statsTotalQueuedDuration += queuedDuration;
                if (dropped) statsDrops++;
                statsTotalAveragePower += averagePower;

                if (ShouldOutput())
                {
                    Output($"fps={statsTotalUpdates / Interval():F3}, record_latency_ms={(int)(statsTotalTimeInInputBuffer * 1000 / statsTotalUpdates)}, output_queue_ms={(int)(statsTotalQueuedDuration / statsTotalUpdates)}, fps_dropped={statsDrops / Interval()}, samples_per_frame={(int)(statsTotalSamples/statsTotalUpdates)}, signal_power={statsTotalAveragePower/statsTotalUpdates}");
                    Clear();
                    statsTotalUpdates = 0;
                    statsTotalSamples = 0;
                    statsTotalTimeInInputBuffer = 0;
                    statsTotalQueuedDuration = 0;
                    statsDrops = 0;
                    statsTotalAveragePower = 0;
                }
            }
        }

        protected Stats stats;
#endif
    }

}