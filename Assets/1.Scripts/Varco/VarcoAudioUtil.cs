using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace Varco {
    // base64 WAV <-> AudioClip 변환 유틸 (VARCO 오디오 API 공용)
    public static class VarcoAudioUtil {
        // base64 WAV 문자열을 AudioClip으로 변환 (실패 시 null)
        public static AudioClip ClipFromWavBase64(string base64, string clipName = "varco_audio") {
            try {
                byte[] wav = Convert.FromBase64String(base64);
                return ClipFromWavBytes(wav, clipName);
            } catch (Exception e) {
                Debug.LogError($"base64 오디오 디코딩 실패: {e.Message}");
                return null;
            }
        }

        // WAV 바이트를 AudioClip으로 변환 - PCM 16/24/32bit, IEEE float 32bit 지원 (실패 시 null)
        public static AudioClip ClipFromWavBytes(byte[] wav, string clipName = "varco_audio") {
            try {
                if (wav.Length < 44 || Ascii(wav, 0, 4) != "RIFF" || Ascii(wav, 8, 4) != "WAVE") {
                    Debug.LogError("RIFF/WAVE 헤더가 아닙니다");
                    return null;
                }

                int format = 0, channels = 0, sampleRate = 0, bits = 0;
                int pos = 12;
                while (pos + 8 <= wav.Length) {
                    string chunkId = Ascii(wav, pos, 4);
                    int chunkSize = BitConverter.ToInt32(wav, pos + 4);
                    if (chunkId == "fmt ") {
                        format = BitConverter.ToInt16(wav, pos + 8);
                        channels = BitConverter.ToInt16(wav, pos + 10);
                        sampleRate = BitConverter.ToInt32(wav, pos + 12);
                        bits = BitConverter.ToInt16(wav, pos + 22);
                        // WAVE_FORMAT_EXTENSIBLE이면 SubFormat 앞 2바이트가 실제 포맷
                        if (format == -2 && chunkSize >= 40) {
                            format = BitConverter.ToInt16(wav, pos + 8 + 24);
                        }
                    } else if (chunkId == "data") {
                        if (channels <= 0 || sampleRate <= 0) {
                            Debug.LogError("data 청크가 fmt 청크보다 먼저 나왔습니다");
                            return null;
                        }
                        int dataLen = Math.Min(chunkSize, wav.Length - pos - 8);
                        float[] samples = DecodeSamples(wav, pos + 8, dataLen, format, bits);
                        if (samples == null) {
                            return null;
                        }
                        AudioClip clip = AudioClip.Create(clipName, samples.Length / channels, channels, sampleRate, false);
                        clip.SetData(samples, 0);
                        return clip;
                    }
                    // 청크 크기가 홀수면 1바이트 패딩
                    pos += 8 + chunkSize + (chunkSize & 1);
                }
                Debug.LogError("WAV data 청크를 찾지 못했습니다");
                return null;
            } catch (Exception e) {
                Debug.LogError($"WAV 파싱 실패: {e.Message}");
                return null;
            }
        }

        // 샘플 데이터를 포맷별로 float(-1~1) 배열로 변환
        private static float[] DecodeSamples(byte[] wav, int offset, int length, int format, int bits) {
            if (format == 3 && bits == 32) {
                int count = length / 4;
                float[] samples = new float[count];
                for (int i = 0; i < count; i++) {
                    samples[i] = BitConverter.ToSingle(wav, offset + i * 4);
                }
                return samples;
            }
            if (format == 1 && bits == 16) {
                int count = length / 2;
                float[] samples = new float[count];
                for (int i = 0; i < count; i++) {
                    samples[i] = BitConverter.ToInt16(wav, offset + i * 2) / 32768f;
                }
                return samples;
            }
            if (format == 1 && bits == 24) {
                int count = length / 3;
                float[] samples = new float[count];
                for (int i = 0; i < count; i++) {
                    int p = offset + i * 3;
                    int v = (wav[p] << 8 | wav[p + 1] << 16 | wav[p + 2] << 24) >> 8;
                    samples[i] = v / 8388608f;
                }
                return samples;
            }
            if (format == 1 && bits == 32) {
                int count = length / 4;
                float[] samples = new float[count];
                for (int i = 0; i < count; i++) {
                    samples[i] = BitConverter.ToInt32(wav, offset + i * 4) / 2147483648f;
                }
                return samples;
            }
            Debug.LogError($"지원하지 않는 WAV 포맷입니다 (format={format}, {bits}bit)");
            return null;
        }

        private static string Ascii(byte[] data, int offset, int count) {
            return System.Text.Encoding.ASCII.GetString(data, offset, count);
        }

        // AudioClip을 WAV(PCM 16bit) base64 문자열로 변환 (업로드용, 실패 시 null)
        public static string ClipToWavBase64(AudioClip clip) {
            byte[] wav = ClipToWavBytes(clip);
            return wav != null ? Convert.ToBase64String(wav) : null;
        }

        // AudioClip을 WAV base64로 비동기 변환 - 샘플 추출만 메인 스레드, 인코딩은 백그라운드 (실패 시 null)
        public static async Task<string> ClipToWavBase64Async(AudioClip clip) {
            try {
                // AudioClip.GetData는 메인 스레드에서만 호출 가능
                float[] samples = new float[clip.samples * clip.channels];
                clip.GetData(samples, 0);
                int channels = clip.channels;
                int frequency = clip.frequency;
                byte[] wav = await Task.Run(() => EncodeWav(samples, channels, frequency));
                return wav != null ? Convert.ToBase64String(wav) : null;
            } catch (Exception e) {
                Debug.LogError($"AudioClip 비동기 WAV 인코딩 실패: {e.Message}");
                return null;
            }
        }

        // AudioClip을 WAV(PCM 16bit) 바이트로 변환 (실패 시 null)
        public static byte[] ClipToWavBytes(AudioClip clip) {
            try {
                float[] samples = new float[clip.samples * clip.channels];
                clip.GetData(samples, 0);
                return EncodeWav(samples, clip.channels, clip.frequency);
            } catch (Exception e) {
                Debug.LogError($"AudioClip WAV 인코딩 실패: {e.Message}");
                return null;
            }
        }

        // float 샘플 배열을 WAV(PCM 16bit) 바이트로 인코딩 (스레드 안전, 실패 시 null)
        private static byte[] EncodeWav(float[] samples, int channels, int frequency) {
            try {
                using (MemoryStream ms = new MemoryStream())
                using (BinaryWriter w = new BinaryWriter(ms)) {
                    int dataSize = samples.Length * 2;
                    w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                    w.Write(36 + dataSize);
                    w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                    w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                    w.Write(16);
                    w.Write((short)1);
                    w.Write((short)channels);
                    w.Write(frequency);
                    w.Write(frequency * channels * 2);
                    w.Write((short)(channels * 2));
                    w.Write((short)16);
                    w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                    w.Write(dataSize);
                    foreach (float s in samples) {
                        w.Write((short)(Mathf.Clamp(s, -1f, 1f) * 32767f));
                    }
                    return ms.ToArray();
                }
            } catch (Exception e) {
                Debug.LogError($"WAV 인코딩 실패: {e.Message}");
                return null;
            }
        }
    }
}
