using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ThereminQuestVR {
    public class Oscillator : MonoBehaviour {

        public OVRSkeleton volumeHand;
        public OVRSkeleton pitchHand;
        public GameObject volumeAntenna;
        public GameObject pitchAntenna;
        public GameObject marker;
        public Text pitchText;

        [Range(0.1f, 50)]
        public float volumeSensitivity;
        [Range(0.1f, 50)]
        public float pitchSensitivity;
        [Range(0, 10)]
        public float pitchMax;
        [Range(0, 10)]
        public float pitchMin;
        [Range(3, 22)]
        public int numBonesToIncludeInAverage;
        public bool markerIsVisible;

		
        private AudioSource audioSource;
        private float volume;
        private float volumeHandDistance;
        private bool volumeHandDetected = false;
        private float pitch;
        private float pitchHandDistance;
        private bool pitchHandDetected = false;

        private readonly int markerRange = 24;
        private readonly int[] blackKeys = { 1, 4, 6, 9, 11 };
        private GameObject[] markers;
        private readonly Vector3 blackKeyScale = new Vector3(1, 0.5f, 1);
        private readonly float frequencyRatio = Mathf.Pow(2, 1.0f / 12.0f);

        private static readonly float A4 = 440.0f;
        private static readonly float C0 = A4 * Mathf.Pow(2, -4.75f);
        private static readonly string[] notes = {"C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"};
        private static readonly int NUM_NOTES = 12;

        void Start() {
			
            // Launch coroutines to mark both hands as detected.
            StartCoroutine(DetectPitchHand());
            StartCoroutine(DetectVolumeHand());
			
            audioSource = gameObject.GetComponent<AudioSource>();

            markers = new GameObject[markerRange * 2 + 1];
            for (int i = 0; i < markers.Length; i++) {
                markers[i] = Instantiate(marker, transform);
                foreach (int j in blackKeys) {

                    if (i % 12 == 3) {
                        markers[i].GetComponent<Renderer>().material.color = Color.red;
                        break;
                    }

                    if (i % 12 == j) {
                        markers[i].GetComponent<Renderer>().material.color = Color.black;
                        markers[i].transform.localScale = Vector3.Scale(markers[i].transform.localScale, blackKeyScale);
                        break;
                    }
                }
            }
        }

        void Update() {
            volumeHandDistance = Mathf.Max(volumeHand.transform.position.y - volumeAntenna.transform.position.y, 0);
            volume = 1 - Mathf.Exp(-volumeHandDistance * volumeSensitivity);
            audioSource.volume = volume;

            pitch = GetPitch();
            audioSource.pitch = pitch;
            UpdatePitchUI(pitch);

            for (int i = 0; i < markers.Length; i++) {
                float a = (Mathf.Pow(frequencyRatio, i - markerRange) - pitchMin) / (pitchMax - pitchMin);
                float distance;

                if (a > 0) {
                    distance = -(1 / pitchSensitivity) * Mathf.Log(a);
                } else {
                    distance = 0;
                }

                if (distance > 0) {
                    markers[i].transform.localPosition = pitchAntenna.transform.localPosition + Vector3.right * distance;
                } else {
                    markers[i].transform.localPosition = pitchAntenna.transform.localPosition;
                }

                markers[i].SetActive(markerIsVisible);
            }
        }
		
        float GetPitch() {
            if (!pitchHandDetected) {
                pitchHandDistance = HorizontalDistance(pitchHand.transform.position, pitchAntenna.transform.position);
            } else {
                pitchHandDistance = ClosestKBonesAverageHorizontalDistance();
            }
            return Mathf.Exp(-pitchHandDistance * pitchSensitivity) * (pitchMax - pitchMin) + pitchMin;
        }
        
        void UpdatePitchUI(float pitch) {
            // Calculate the frequency from the pitch.
            float freq = A4 * pitch;
            // get number of half steps from C0
            int numHalfSteps = Mathf.RoundToInt(NUM_NOTES * Mathf.Log(freq / C0, 2));
            int octave = numHalfSteps / NUM_NOTES;
            int note = numHalfSteps % NUM_NOTES;
            pitchText.text = string.Format("Pitch: {0:0.00}, Frequency: {1:0}Hz, Nearest Note: {2}{3}", pitch, freq, notes[note], octave);
        }
		
        float HorizontalDistance(Vector3 v1, Vector3 v2) {
            return Vector2.Distance(new Vector2(v1.x, v1.z), new Vector2(v2.x, v2.z));
        }
		
        float ClosestKBonesAverageHorizontalDistance() {
            float[] closestK = new float[numBonesToIncludeInAverage];
            
            for (int i = 0; i < closestK.Length; ++i) {
                closestK[i] = Mathf.Infinity;
            }
            
            foreach (var bone in pitchHand.Bones) {
                float distance = HorizontalDistance(bone.Transform.position, pitchAntenna.transform.position);
                // If distance belongs in closest K, find where it should go.
                int index = -1;
                for (int i = 0; i < closestK.Length; ++i) {
                    if (distance < closestK[i]) {
                        index = i;
                        break;
                    }
                }
                if (index >= 0) {
                    // Shift everything including and after index to the right by one.
                    int i = closestK.Length - 1;
                    while (index < i) {
                        // Shift right.
                        closestK[i] = closestK[i-1];
                        --i;
                    }
                    // Place the new distance in the proper position.
                    closestK[index] = distance;
                }
            }
            
            float sumDistances = 0;
            foreach (var distance in closestK) {
                if (distance != Mathf.Infinity) {
                    sumDistances += distance;
                }
            }
            
            return sumDistances / numBonesToIncludeInAverage;
        }
		
        IEnumerator DetectPitchHand() {
            yield return new WaitUntil(() => pitchHand.Bones != null && pitchHand.Bones.Count > 0);
            pitchHandDetected = true;
        }

        IEnumerator DetectVolumeHand() {
            yield return new WaitUntil(() => volumeHand.Bones != null && volumeHand.Bones.Count > 0);
            volumeHandDetected = true;
        }
    }
}
