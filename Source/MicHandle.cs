using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace MicBuddy
{
	public class MicHandle
	{
		#region Fields
		public static MicHandle GetInstance;
		private const int FREQUENCY = 48000;
		// Wavelength, I think.
		private const int SAMPLECOUNT = 1024;
		// Sample Count.
		private const float REFVALUE = 0.1f;
		// RMS value for 0 dB.
		private const float THRESHOLD = 0.02f;
		// Minimum amplitude to extract pitch (recieve anything)
		private const float ALPHA = 0.05f;
		// The alpha for the low pass filter (I don't really understand this).
		public GameObject resultDisplay;
		// GUIText for displaying results
		public GameObject blowDisplay;
		// GUIText for displaying blow or not blow.
		public int recordedLength = 30;
		// How many previous frames of sound are analyzed.
		public int requiredBlowTime = 4;
		// How long a blow must last to be classified as a blow (and not a sigh for instance).
		private float pitchValue = 0.0f;
		// Pitch - Hz (is this frequency?)
		private int blowingTime = 0;
		// How long each blow has lasted
		private float lowPassResults;
		// Low Pass Filter result
		private float peakPowerForChannel;
		//
		private float[] samples;
		// Samples
		private float[] spectrum;
		// Spectrum
		private List <float> dbValues;
		// Used to average recent volume.
		private List <float> pitchValues;
		// Used to average recent
		/// <summary>
		/// The sound volume of the current frame. (stored in dannobels).
		/// </summary>
		private float m_fCurrentVolume = 0.0f;
		/// <summary>
		/// Used to find the max volume from the last x number of samples.
		/// </summary>
		private float m_fMaxVolume = 0.0f;
		#endregion
		#region Properties
		public float Volume
		{
			get { return m_fCurrentVolume; }
		}

		public float AverageVolume
		{
			get { return m_fMaxVolume; }
		}
		#endregion
		public void Start()
		{
			StartMicListener();
		}

		public void Awake()
		{
			GetInstance = this;
       
			samples = new float [SAMPLECOUNT];
			spectrum = new float [SAMPLECOUNT];
			dbValues = new List <float>();
			pitchValues = new List <float>();
		}

		public void Update()
		{
			// If the audio has stopped playing, this will restart the mic play the clip.
			if (!audio.isPlaying)
			{
				StartMicListener();
			}

			// Gets volume and pitch values
			AnalyzeSound();

			// Runs a series of algorithms to decide whether a blow is occuring.
			//DeriveBlow ();

			//Run a series of algorithms to decide whether a player is talking.
			DeriveIsTalking();

			// Update the meter display.
			resultDisplay.guiText.text =
                      "    Volume (DB): " + Volume.ToString("F2") + "\n" +
				"Avg Volume (DB): " + AverageVolume.ToString("F2") + "\n" +
				"Pitch (Hz): " + pitchValue.ToString("F0");
		}

		/// Starts the Mic, and plays the audio back in (near) real-time.
		private void StartMicListener()
		{
			audio.clip = Microphone.Start("Built-in Microphone", true, 999, FREQUENCY);

			// HACK - Forces the function to wait until the microphone has started, before moving onto the play function.
			while (!(Microphone.GetPosition("Built-in Microphone" ) > 0))
			{
			}

			audio.Play();
		}

		/// Credits to aldonaletto for the function, http://goo.gl/VGwKt
		/// Analyzes the sound, to get volume and pitch values.
		private void AnalyzeSound()
		{
			// Get all of our samples from the mic.
			audio.GetOutputData(samples, 0);

			//Get the largest waveform from all the current samples.
			float fMaxDb = 0.0f;
			for (int i = 0; i < SAMPLECOUNT; i++)
			{
				float fAbs = Mathf.Abs(samples[i]);
				if (fAbs > fMaxDb)
				{
					fMaxDb = fAbs;
				}
			}

			//Set the current volume to the loudest sound
			m_fCurrentVolume = fMaxDb;

			//CALCULATE PITCH

			// Gets the sound spectrum.
			audio.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);
			float maxV = 0;
			int maxN = 0;

			// Find the highest sample.
			for (int i = 0; i < SAMPLECOUNT; i++)
			{
				if (spectrum[i] > maxV && spectrum[i] > THRESHOLD)
				{
					maxV = spectrum[i];
					maxN = i; // maxN is the index of max
				}
			}

			// Pass the index to a float variable
			float freqN = maxN;

			// Interpolate index using neighbours
			if (maxN > 0 && maxN < SAMPLECOUNT - 1)
			{
				float dL = spectrum[maxN - 1] / spectrum[maxN];
				float dR = spectrum[maxN + 1] / spectrum[maxN];
				freqN += 0.5f * (dR * dR - dL * dL);
			}

			// Convert index to frequency
			pitchValue = freqN * 24000 / SAMPLECOUNT;
		}

		private void DeriveBlow()
		{
			UpdateRecords(Volume, dbValues);
			UpdateRecords(pitchValue, pitchValues);

			// Find the average pitch in our records (used to decipher against whistles, clicks, etc).
			float sumPitch = 0;
			foreach (float num in pitchValues)
			{
				sumPitch += num;
			}
			sumPitch /= pitchValues.Count;

			// Run our low pass filter.
			lowPassResults = LowPassFilter(Volume);

			// Decides whether this instance of the result could be a blow or not.
			if (lowPassResults > -30 && sumPitch == 0)
			{
				blowingTime += 1;
			}
			else
			{
				blowingTime = 0;
			}
		}
		// Updates a record, by removing the oldest entry and adding the newest value (val).
		private void UpdateRecords(float val, List<float > record)
		{
			while (record.Count > recordedLength)
			{
				record.RemoveAt(0);
			}
			record.Add(val);
		}

		/// <summary>
		/// Figure out if the player is talking by averaging out the recent values of volume
		/// </summary>
		private void DeriveIsTalking()
		{
			UpdateRecords(Volume, dbValues);

			//Find the largest value in the current list
			m_fMaxVolume = 0.0f;
			for (int i = 0; i < dbValues.Count; i++)
			{
				if (dbValues[i] > m_fMaxVolume)
				{
					m_fMaxVolume = dbValues[i];
				}
			}
		}

		/// Gives a result (I don't really understand this yet) based on the peak volume of the record
		/// and the previous low pass results.
		private float LowPassFilter(float peakVolume)
		{
			return ALPHA * peakVolume + (1.0f - ALPHA) * lowPassResults;
		}
	}
}