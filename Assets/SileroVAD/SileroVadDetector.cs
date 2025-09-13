using System;
using System.Collections.Generic;
using UnityEngine;

namespace VadDotNet
{
    public class SileroVadDetector
    {
        private readonly SileroVadOnnxModel _model;
        private readonly float _threshold;
        private readonly float _negThreshold;
        private readonly int _samplingRate;
        private readonly int _windowSizeSample;
        private readonly float _minSpeechSamples;
        private readonly float _speechPadSamples;
        private readonly float _maxSpeechSamples;
        private readonly float _minSilenceSamples;
        private readonly float _minSilenceSamplesAtMaxSpeech;
        private int _audioLengthSamples;
        private const float THRESHOLD_GAP = 0.15f;
        private const int SAMPLING_RATE_8K = 8000;
        private const int SAMPLING_RATE_16K = 16000;

        public SileroVadDetector(string onnxModelPath, float threshold, int samplingRate,
            int minSpeechDurationMs, float maxSpeechDurationSeconds,
            int minSilenceDurationMs, int speechPadMs)
        {
            if (samplingRate != SAMPLING_RATE_8K && samplingRate != SAMPLING_RATE_16K)
            {
                throw new ArgumentException("Sampling rate not supported. Only 8000 or 16000 Hz are allowed.");
            }

            _model = new SileroVadOnnxModel(onnxModelPath);
            _samplingRate = samplingRate;
            _threshold = threshold;
            _negThreshold = threshold - THRESHOLD_GAP;
            _windowSizeSample = samplingRate == SAMPLING_RATE_16K ? 512 : 256;
            _minSpeechSamples = samplingRate * minSpeechDurationMs / 1000f;
            _speechPadSamples = samplingRate * speechPadMs / 1000f;
            _maxSpeechSamples = samplingRate * maxSpeechDurationSeconds - _windowSizeSample - 2 * _speechPadSamples;
            _minSilenceSamples = samplingRate * minSilenceDurationMs / 1000f;
            _minSilenceSamplesAtMaxSpeech = samplingRate * 98 / 1000f;

            Reset();
        }

        public void Reset()
        {
            _model.ResetStates();
        }

        // ✅ AudioClip 기반 기존 메서드
        public List<SileroSpeechSegment> GetSpeechSegmentListFromAudioClip(AudioClip audioClip)
        {
            Reset();

            int totalSamples = audioClip.samples * audioClip.channels;
            float[] samples = new float[totalSamples];
            audioClip.GetData(samples, 0);

            return GetSpeechSegmentListFromPcm(samples, audioClip.channels);
        }

        // ✅ 추가된 메서드: float[] 기반 분석
        public List<SileroSpeechSegment> GetSpeechSegmentListFromPcm(float[] pcmData, int channels = 1)
        {
            Reset();

            float[] monoSamples;

            if (channels > 1)
            {
                int monoSamplesCount = pcmData.Length / channels;
                monoSamples = new float[monoSamplesCount];

                for (int i = 0; i < monoSamplesCount; i++)
                {
                    float sum = 0f;
                    for (int ch = 0; ch < channels; ch++)
                    {
                        sum += pcmData[i * channels + ch];
                    }
                    monoSamples[i] = sum / channels;
                }
            }
            else
            {
                monoSamples = pcmData;
            }

            _audioLengthSamples = monoSamples.Length;
            List<float> speechProbList = new List<float>();

            for (int offset = 0; offset <= monoSamples.Length - _windowSizeSample; offset += _windowSizeSample)
            {
                float[] buffer = new float[_windowSizeSample];
                Array.Copy(monoSamples, offset, buffer, 0, _windowSizeSample);
                float speechProb = _model.Call(new[] { buffer }, _samplingRate)[0];
                speechProbList.Add(speechProb);
            }

            return CalculateProb(speechProbList);
        }

        // ✅ 추가된 메서드: float[] 기반 단순 음성 감지
        public bool IsSpeechDetected(float[] pcmData, int channels = 1)
        {
            var segments = GetSpeechSegmentListFromPcm(pcmData, channels);
            return segments != null && segments.Count > 0;
        }

        private List<SileroSpeechSegment> CalculateProb(List<float> speechProbList)
        {
            List<SileroSpeechSegment> result = new List<SileroSpeechSegment>();
            bool triggered = false;
            int tempEnd = 0, prevEnd = 0, nextStart = 0;
            SileroSpeechSegment segment = new SileroSpeechSegment();

            for (int i = 0; i < speechProbList.Count; i++)
            {
                float speechProb = speechProbList[i];
                if (speechProb >= _threshold && (tempEnd != 0))
                {
                    tempEnd = 0;
                    if (nextStart < prevEnd)
                        nextStart = _windowSizeSample * i;
                }

                if (speechProb >= _threshold && !triggered)
                {
                    triggered = true;
                    segment.StartOffset = _windowSizeSample * i;
                    continue;
                }

                if (triggered && (_windowSizeSample * i) - segment.StartOffset > _maxSpeechSamples)
                {
                    if (prevEnd != 0)
                    {
                        segment.EndOffset = prevEnd;
                        result.Add(segment);
                        segment = new SileroSpeechSegment();
                        triggered = (nextStart >= prevEnd) ? true : false;
                        segment.StartOffset = (nextStart >= prevEnd) ? nextStart : 0;
                        prevEnd = nextStart = tempEnd = 0;
                    }
                    else
                    {
                        segment.EndOffset = _windowSizeSample * i;
                        result.Add(segment);
                        segment = new SileroSpeechSegment();
                        prevEnd = nextStart = tempEnd = 0;
                        triggered = false;
                        continue;
                    }
                }

                if (speechProb < _negThreshold && triggered)
                {
                    if (tempEnd == 0)
                        tempEnd = _windowSizeSample * i;

                    if ((_windowSizeSample * i) - tempEnd > _minSilenceSamplesAtMaxSpeech)
                        prevEnd = tempEnd;

                    if ((_windowSizeSample * i) - tempEnd < _minSilenceSamples)
                        continue;
                    else
                    {
                        segment.EndOffset = tempEnd;
                        if ((segment.EndOffset - segment.StartOffset) > _minSpeechSamples)
                            result.Add(segment);

                        segment = new SileroSpeechSegment();
                        prevEnd = nextStart = tempEnd = 0;
                        triggered = false;
                        continue;
                    }
                }
            }

            if (segment.StartOffset != null && (_audioLengthSamples - segment.StartOffset) > _minSpeechSamples)
            {
                segment.EndOffset = _audioLengthSamples;
                result.Add(segment);
            }

            for (int i = 0; i < result.Count; i++)
            {
                var item = result[i];
                if (i == 0)
                {
                    item.StartOffset = (int)Math.Max(0, item.StartOffset.Value - _speechPadSamples);
                }

                if (i != result.Count - 1)
                {
                    var nextItem = result[i + 1];
                    int silenceDuration = nextItem.StartOffset.Value - item.EndOffset.Value;

                    if (silenceDuration < 2 * _speechPadSamples)
                    {
                        item.EndOffset += silenceDuration / 2;
                        nextItem.StartOffset = Math.Max(0, nextItem.StartOffset.Value - silenceDuration / 2);
                    }
                    else
                    {
                        item.EndOffset = (int)Math.Min(_audioLengthSamples, item.EndOffset.Value + _speechPadSamples);
                        nextItem.StartOffset = (int)Math.Max(0, nextItem.StartOffset.Value - _speechPadSamples);
                    }
                }
                else
                {
                    item.EndOffset = (int)Math.Min(_audioLengthSamples, item.EndOffset.Value + _speechPadSamples);
                }
            }

            return MergeListAndCalculateSecond(result, _samplingRate);
        }

        private List<SileroSpeechSegment> MergeListAndCalculateSecond(List<SileroSpeechSegment> original, int samplingRate)
        {
            List<SileroSpeechSegment> result = new List<SileroSpeechSegment>();
            if (original == null || original.Count == 0)
                return result;

            int left = original[0].StartOffset.Value;
            int right = original[0].EndOffset.Value;

            if (original.Count > 1)
            {
                original.Sort((a, b) => a.StartOffset.Value.CompareTo(b.StartOffset.Value));

                for (int i = 1; i < original.Count; i++)
                {
                    var segment = original[i];
                    if (segment.StartOffset > right)
                    {
                        result.Add(new SileroSpeechSegment(left, right,
                            CalculateSecondByOffset(left, samplingRate),
                            CalculateSecondByOffset(right, samplingRate)));
                        left = segment.StartOffset.Value;
                        right = segment.EndOffset.Value;
                    }
                    else
                    {
                        right = Math.Max(right, segment.EndOffset.Value);
                    }
                }

                result.Add(new SileroSpeechSegment(left, right,
                    CalculateSecondByOffset(left, samplingRate),
                    CalculateSecondByOffset(right, samplingRate)));
            }
            else
            {
                result.Add(new SileroSpeechSegment(left, right,
                    CalculateSecondByOffset(left, samplingRate),
                    CalculateSecondByOffset(right, samplingRate)));
            }

            return result;
        }

        private float CalculateSecondByOffset(int offset, int samplingRate)
        {
            float secondValue = offset / (float)samplingRate;
            return (float)Math.Floor(secondValue * 1000.0f) / 1000.0f;
        }
    }
}
