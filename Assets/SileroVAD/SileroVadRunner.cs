using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace VadDotNet
{
    public class SileroVadRunner : MonoBehaviour
    {
        [Header("ONNX Model Settings")]
        public string onnxModelFileName = "silero_vad.onnx";
        public int sampleRate = 16000;
        public float threshold = 0.5f;
        public int minSpeechDurationMs = 250;
        public float minSilenceDurationMs = 100;
        public float maxSpeechDurationSeconds = float.PositiveInfinity;
        public int speechPadMs = 30;
        
        [Header("Filtering")]
        [Tooltip("Discard segments shorter than this (ms)—no save, no Groq.")]
        public int minSegmentDurationMs = 800;   // try 600–1200 ms


        [Header("Audio Settings")]
        public string audioFileName = "path_to_audio_file.wav";
        // Window length in seconds for microphone real-time analysis (for VAD)
        public float micAnalysisWindowSeconds = 1.0f;
        public bool isEcho = false;

        [Header("Visual Settings")]
        public Renderer targetRenderer; // Green when speech, red when silence

        private SileroVadDetector vadDetector;
        private List<SileroSpeechSegment> speechSegments;
        private AudioSource audioSource;
        private string modelPath;

        // Microphone mode related variables
        private bool isMicModeActive = false;
        private string micDevice;

        // -------- Recording config --------
        [Header("Recording")]
        [Tooltip("Extra audio BEFORE detection begins (ms)")]
        public int prerollMs = 200;

        [Tooltip("Extra audio AFTER detection ends (ms)")]
        public int postrollMs = 200;

        [Tooltip("Folder (relative to persistentDataPath) where WAVs are saved")]
        public string saveFolder = "vad_captures";

        // -------- Internal buffers/state --------
        private float[] micBuffer;          // reused analysis window for VAD
        private Queue<float> preRoll;       // circular queue for preroll (mono samples)
        private int preRollMaxSamples;

        private bool isRecordingSegment = false;
        private List<float> segmentSamples; // collects current segment (mono)
        private float postrollTimer = 0f;   // seconds remaining to keep recording after speech=false

        // convenience
        private int analysisSamples;        // samples per analysis window (mono)
        private int lastReadPos = 0;        // last position read from mic ring buffer (in samples)
        private float[] deltaBuffer;        // reusable buffer for "only-new" samples
        
        // Sticky VAD state (for live mode)
        private bool speechState = false;      // smoothed/“sticky” speech state
        private float silenceMsAccum = 0f;     // how long we've been continuously silent
        private float speechMsAccum = 0f;      // how long we've been continuously in speech


        private void Start()
        {
            modelPath = Path.Combine(Application.streamingAssetsPath, onnxModelFileName);
            vadDetector = new SileroVadDetector(
                modelPath,
                threshold,
                sampleRate,
                minSpeechDurationMs,
                maxSpeechDurationSeconds,
                (int)minSilenceDurationMs,
                speechPadMs
            );
        }

        // Press button to call: file-based demo (unchanged)
        public void StartMusic()
        {
            string audioPath = Path.Combine(Application.streamingAssetsPath, audioFileName);
            audioSource = gameObject.AddComponent<AudioSource>();
            StartCoroutine(ProcessAudioClip(audioPath));
        }

        IEnumerator ProcessAudioClip(string audioPath)
        {
            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + audioPath, AudioType.WAV))
            {
                yield return www.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
                if (www.result != UnityWebRequest.Result.Success)
#else
                if (www.isNetworkError || www.isHttpError)
#endif
                {
                    Debug.LogError("Error loading audio clip: " + www.error);
                }
                else
                {
                    AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                    Debug.Log("Audio clip loaded. Processing...");

                    speechSegments = vadDetector.GetSpeechSegmentListFromAudioClip(clip);

                    audioSource.clip = clip;
                    audioSource.Play();
                }
            }
        }

        // Press button to call: enable live voice detection via microphone
        public void StartMicrophone()
        {
            if (Microphone.devices.Length == 0)
            {
                Debug.LogError("No microphone devices found.");
                return;
            }

            // Select the first available microphone
            micDevice = Microphone.devices[0];

            audioSource = gameObject.AddComponent<AudioSource>();
            // Start recording the microphone by creating a 10-second audio clip with loop:true
            // NOTE: Unity chooses channel count; many devices are mono, some are stereo.
            audioSource.clip = Microphone.Start(micDevice, true, 10, sampleRate);

            // Wait for recording to begin (avoid tight spin: yield for a frame if desired)
            while (!(Microphone.GetPosition(micDevice) > 0)) { }

            if (isEcho)
            {
                audioSource.Play();
            }

            isMicModeActive = true;

            // ----- Initialize buffers/state -----
            analysisSamples = Mathf.FloorToInt(sampleRate * micAnalysisWindowSeconds);
            micBuffer = new float[analysisSamples];

            preRollMaxSamples = Mathf.RoundToInt((prerollMs / 1000f) * sampleRate);
            preRoll = new Queue<float>(preRollMaxSamples);

            segmentSamples = new List<float>(sampleRate * 10); // start with ~10s capacity
            Directory.CreateDirectory(Path.Combine(Application.persistentDataPath, saveFolder));

            // Track the starting position in the ring buffer and size a delta buffer
            lastReadPos = Microphone.GetPosition(micDevice);
            int maxDelta = Mathf.CeilToInt(sampleRate * 0.2f); // generous headroom for 0.1s tick + jitter
            deltaBuffer = new float[maxDelta];

            StartCoroutine(ProcessMicrophoneAudio());
        }

        // Microphone audio is analyzed at regular intervals to detect audio in real time
        IEnumerator ProcessMicrophoneAudio()
        {
            while (isMicModeActive)
            {
                int currentPos = Microphone.GetPosition(micDevice);
                int totalSamples = audioSource.clip.samples;
                int channels = audioSource.clip.channels; // ASSUMED 1 for this implementation

                // ------- Read analysis window for VAD -------
                int startPos = currentPos - analysisSamples;
                if (startPos < 0) startPos += totalSamples;

                float[] samples = micBuffer; // reuse

                if (startPos + analysisSamples <= totalSamples)
                {
                    audioSource.clip.GetData(samples, startPos);
                }
                else
                {
                    // wrap-around: read tail then head
                    int firstPart = totalSamples - startPos;

                    // first tail
                    var tailTemp = new float[firstPart];
                    audioSource.clip.GetData(tailTemp, startPos);
                    Array.Copy(tailTemp, 0, samples, 0, firstPart);

                    // then head
                    int remaining = analysisSamples - firstPart;
                    var headTemp = new float[remaining];
                    audioSource.clip.GetData(headTemp, 0);
                    Array.Copy(headTemp, 0, samples, firstPart, remaining);
                }

                bool rawSpeech = vadDetector.IsSpeechDetected(samples, audioSource.clip.channels);

// Tick length in ms (matches your yield WaitForSeconds below)
                const float tickMs = 100f;

// ---- Sticky/Hangover logic ----
                if (rawSpeech)
                {
                    speechMsAccum += tickMs;
                    silenceMsAccum = 0f;

                    // Only flip on after we've seen at least minSpeechDurationMs of continuous speech
                    if (!speechState && speechMsAccum >= minSpeechDurationMs)
                        speechState = true;
                }
                else
                {
                    silenceMsAccum += tickMs;
                    speechMsAccum = 0f;

                    // Only flip off after we've seen at least minSilenceDurationMs of continuous silence
                    if (speechState && silenceMsAccum >= minSilenceDurationMs)
                        speechState = false;
                }

// From here on, USE 'speechState' (not rawSpeech) for color, logging, and recording:
                bool isSpeech = speechState;


                // ------- Visual feedback -------
                if (targetRenderer != null)
                    targetRenderer.material.color = isSpeech ? Color.green : Color.red;

                // ------- Maintain preroll (from analysis window is fine) -------
                for (int i = 0; i < samples.Length; i++)
                {
                    if (preRoll.Count >= preRollMaxSamples) preRoll.Dequeue();
                    preRoll.Enqueue(samples[i]); // mono assumed
                }

                // ------- Compute and fetch ONLY-NEW samples since lastReadPos -------
                int newEnd = currentPos; // exclusive
                int newCount = newEnd - lastReadPos;
                if (newCount < 0) newCount += totalSamples; // wrap-around

                // Clamp to deltaBuffer capacity (safety against long stalls)
                if (newCount > deltaBuffer.Length) newCount = deltaBuffer.Length;

                if (newCount > 0)
                {
                    CopyFromRing(audioSource.clip, lastReadPos, newCount, deltaBuffer);
                    lastReadPos = newEnd;
                }

                // ------- Recording state machine (append only deltaBuffer[0..newCount)) -------
                if (isSpeech)
                {
                    if (!isRecordingSegment)
                    {
                        isRecordingSegment = true;
                        segmentSamples.Clear();

                        // prepend preroll
                        if (preRoll.Count > 0)
                            segmentSamples.AddRange(preRoll);

                        postrollTimer = postrollMs / 1000f;
                        Debug.Log("[VAD] Segment START");
                    }

                    if (newCount > 0)
                    {
                        // TODO: if channels > 1, downmix deltaBuffer into mono before adding
                        segmentSamples.AddRange(new ArraySegment<float>(deltaBuffer, 0, newCount));
                    }

                    postrollTimer = postrollMs / 1000f; // keep armed while speech continues
                }
                else
                {
                    if (isRecordingSegment)
                    {
                        postrollTimer -= 0.1f; // matches yield WaitForSeconds below

                        if (newCount > 0)
                        {
                            // keep appending new audio during postroll
                            segmentSamples.AddRange(new ArraySegment<float>(deltaBuffer, 0, newCount));
                        }

                        if (postrollTimer <= 0f)
                        {
                            // --- NEW: duration check before saving/sending ---
                            float durMs = (segmentSamples.Count * 1000f) / Mathf.Max(1, sampleRate);
                            if (durMs < minSegmentDurationMs)
                            {
                                Debug.Log($"[VAD] Discarded short segment: {durMs:0} ms (< {minSegmentDurationMs} ms)");
                                isRecordingSegment = false;
                                segmentSamples.Clear();
                            }
                            else
                            {
                                // finalize and save
                                string wavPath = SaveSegmentToWav(segmentSamples.ToArray(), sampleRate);
                                isRecordingSegment = false;
                                segmentSamples.Clear();
                                Debug.Log("[VAD] Segment END (saved): " + wavPath);

                                // 🔁 Send to Groq for transcription
                                StartCoroutine(SendToGroq(wavPath));
                            }
                        }

                    }
                }

                if (isSpeech) Debug.Log("Voice detected by microphone.");
                yield return new WaitForSeconds(0.1f);
            }
        }

        /// <summary>
        /// Copy exactly 'length' mono samples from circular AudioClip starting at 'startPos' into dst[0..length).
        /// Assumes clip.GetData returns interleaved data; for mono channels==1 it's just samples.
        /// </summary>
        private void CopyFromRing(AudioClip clip, int startPos, int length, float[] dst)
        {
            if (length <= 0) return;

            int total = clip.samples; // per channel
            if (startPos < 0) startPos += total;

            int firstPart = Mathf.Min(length, total - startPos);
            if (firstPart > 0)
            {
                var temp = new float[firstPart];
                clip.GetData(temp, startPos);
                Array.Copy(temp, 0, dst, 0, firstPart);
            }

            int remaining = length - firstPart;
            if (remaining > 0)
            {
                var temp2 = new float[remaining];
                clip.GetData(temp2, 0);
                Array.Copy(temp2, 0, dst, firstPart, remaining);
            }
        }

        // -------- WAV writer (mono, 16-bit PCM) --------
        static string SaveSegmentToWav(float[] samples, int sampleRate)
        {
            try
            {
                // clamp to [-1,1] and convert to 16-bit PCM
                short[] intData = new short[samples.Length];
                for (int i = 0; i < samples.Length; i++)
                {
                    float v = Mathf.Clamp(samples[i], -1f, 1f);
                    intData[i] = (short)Mathf.RoundToInt(v * short.MaxValue);
                }

                byte[] bytes = new byte[intData.Length * 2];
                Buffer.BlockCopy(intData, 0, bytes, 0, bytes.Length);

                string dir = Path.Combine(Application.persistentDataPath, "vad_captures");
                string fileName = $"segment_{DateTime.Now:yyyyMMdd_HHmmss_fff}.wav";
                string path = Path.Combine(dir, fileName);

                using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
                using (var bw = new BinaryWriter(fs))
                {
                    int channels = 1;
                    int bitsPerSample = 16;
                    int byteRate = sampleRate * channels * bitsPerSample / 8;
                    int subchunk2Size = bytes.Length;
                    int chunkSize = 36 + subchunk2Size;

                    // RIFF header
                    bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                    bw.Write(chunkSize);
                    bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

                    // fmt  subchunk
                    bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                    bw.Write(16);                 // PCM header size
                    bw.Write((short)1);           // AudioFormat = PCM
                    bw.Write((short)channels);    // NumChannels = 1 (mono)
                    bw.Write(sampleRate);
                    bw.Write(byteRate);
                    bw.Write((short)(channels * bitsPerSample / 8)); // BlockAlign
                    bw.Write((short)bitsPerSample);

                    // data subchunk
                    bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                    bw.Write(subchunk2Size);
                    bw.Write(bytes);
                }

                return path;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VAD] Save WAV failed: {ex.Message}");
                return null;
            }
        }
        
        // -------- Groq STT --------
        [Header("Groq STT")]
        [Tooltip("Your Groq API key (keep secret in production!).")]
        [SerializeField] private string groqApiKey = "GROQ_API_KEY_HERE";
        [Tooltip("Model for transcription (fast): whisper-large-v3-turbo")]
        [SerializeField] private string groqTranscribeModel = "whisper-large-v3-turbo";
        [Tooltip("Translate to English instead of transcribing (uses whisper-large-v3).")]
        [SerializeField] private bool translateToEnglish = false;
        [Tooltip("Optional language hint (ISO-639-1). e.g., 'yo' for Yoruba, 'en' for English.")]
        [SerializeField] private string languageHint = "";
        [Tooltip("Optional prompt/context for STT (proper names, spellings).")]
        [TextArea] [SerializeField] private string transcriptionPrompt = "";
        [Tooltip("Request timestamps; if true, returns verbose_json (larger payload).")]
        [SerializeField] private bool requestTimestamps = false;

        private const string GROQ_TRANSCRIBE_URL = "https://api.groq.com/openai/v1/audio/transcriptions";
        private const string GROQ_TRANSLATE_URL  = "https://api.groq.com/openai/v1/audio/translations";

        [Serializable] public class TranscriptionEvent : UnityEngine.Events.UnityEvent<string> {}
        public TranscriptionEvent onTranscription;
        // -------- Send WAV to Groq STT --------
        IEnumerator SendToGroq(string wavPath)
        {
            if (string.IsNullOrEmpty(wavPath) || !File.Exists(wavPath))
            {
                Debug.LogError("[Groq STT] Missing wav path.");
                yield break;
            }
            if (string.IsNullOrEmpty(groqApiKey) || groqApiKey.Contains("GROQ_API_KEY_HERE"))
            {
                Debug.LogError("[Groq STT] Set your Groq API key in the inspector.");
                yield break;
            }

            // Build multipart form
            var form = new WWWForm();

            // Endpoint + model selection
            string url = translateToEnglish ? GROQ_TRANSLATE_URL : GROQ_TRANSCRIBE_URL;
            string model = translateToEnglish ? "whisper-large-v3" : groqTranscribeModel;

            form.AddField("model", model);

            if (!string.IsNullOrWhiteSpace(languageHint))
                form.AddField("language", languageHint.Trim()); // improves accuracy/latency

            if (!string.IsNullOrWhiteSpace(transcriptionPrompt))
                form.AddField("prompt", transcriptionPrompt);

            if (requestTimestamps)
            {
                form.AddField("response_format", "verbose_json");
                form.AddField("timestamp_granularities[]", "word"); // or "segment", or both
            }
            else
            {
                form.AddField("response_format", "json");
            }

            byte[] data = File.ReadAllBytes(wavPath);
            form.AddBinaryData("file", data, Path.GetFileName(wavPath), "audio/wav");

            using (var req = UnityWebRequest.Post(url, form))
            {
                req.SetRequestHeader("Authorization", "Bearer " + groqApiKey);
                yield return req.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
                if (req.result != UnityWebRequest.Result.Success)
#else
                if (req.isNetworkError || req.isHttpError)
#endif
                {
                    Debug.LogError($"[Groq STT] Error: {req.error}\n{req.downloadHandler.text}");
                    yield break;
                }

                string json = req.downloadHandler.text;
                Debug.Log("[Groq STT] Raw response: " + json);

                // Parse minimal { "text": "..." } when response_format = json
                var text = ExtractTextFromGroqJson(json);
                if (!string.IsNullOrEmpty(text))
                {
                    Debug.Log("[Groq STT] Text: " + text);
                    onTranscription?.Invoke(text);
                }
            }
        }

        // Minimal JSON extractor for { "text": "..." }
        [Serializable] private class GroqTextOnly { public string text; }
        private string ExtractTextFromGroqJson(string json)
        {
            try
            {
                var t = JsonUtility.FromJson<GroqTextOnly>(json);
                return t != null ? t.text : null;
            }
            catch { return null; }
        }

        // File-mode visual feedback (unchanged)
        void Update()
        {
            if (speechSegments != null && audioSource != null && audioSource.clip != null && audioSource.isPlaying)
            {
                float currentTime = audioSource.time;
                bool isSpeech = false;

                foreach (var segment in speechSegments)
                {
                    if (currentTime >= segment.StartSecond && currentTime <= segment.EndSecond)
                    {
                        isSpeech = true;
                        break;
                    }
                }

                if (targetRenderer != null)
                {
                    targetRenderer.material.color = isSpeech ? Color.green : Color.red;
                }

                if (isSpeech)
                {
                    Debug.Log("Voice detected.");
                }
            }
        }
    }
}
