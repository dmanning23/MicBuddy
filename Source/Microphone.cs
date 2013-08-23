﻿using System;
using System.Collections.Generic;
using System.Threading;
using OpenTK;
using OpenTK.Audio;
using OpenTK.Audio.OpenAL;

namespace MicBuddy
{
	public class Microphone
	{
		#region Fields

		/// <summary>
		/// The OpenAL audio capture device
		/// This will be NULL if shit is fucked up
		/// </summary>
		AudioCapture audio_capture;

		/// <summary>
		/// The buffer to hold audio data.  
		/// </summary>
		private byte[] buffer;

		/// <summary>
		/// Flag used by the threading thing to check if it should keep spinning and checking mic data
		/// </summary>
		private bool continuePolling = false;

		/// <summary>
		/// A thread for doing all the microhpone audio processing
		/// </summary>
		Thread workerThread = null;

		/// <summary>
		/// The number of bytes in one sample.
		/// </summary>
		private int SampleToByte;

		/// <summary>
		/// An openal source???
		/// </summary>
		private int src;

		/// <summary>
		/// Sample rate used to capture data per second
		/// </summary>
		private const int audioSamplingRate = 22050;

		/// <summary>
		/// Number of samples to collect (8000 samples per second / number of Samples = 64 ms of audio capture)
		/// </summary>
		private const int numberOfSamples = 1024;

		/// <summary>
		/// Gain of microphone
		/// </summary>
		private const float microphoneGain = 4.0f;

		/// <summary>
		/// How many previous frames of sound are analyzed.
		/// </summary>
		public const int recordedLength = 10;

		/// <summary>
		/// Used to average recent volume.
		/// </summary>
		private List<float> dbValues = new List<float>();

		/// <summary>
		/// Gets the name of the microphone.
		/// </summary>
		/// <value>The name of the microphone.</value>
		public string MicrophoneName { get; private set; }

		#endregion Fields

		#region Properties

		/// <summary>
		/// A list of all the available microphones
		/// </summary>
		public static IList<string> AvailableMicrophones { get; private set; }

		/// <summary>
		/// Gets a value indicating whether this instance is microphone valid.
		/// </summary>
		/// <value><c>true</c> if this instance is microphone valid; otherwise, <c>false</c>.</value>
		public bool IsMicrophoneValid
		{
			get
			{
				return (null != audio_capture);
			}
		}

		/// <summary>
		/// The sound volume of the current frame. (stored in dannobels).
		/// </summary>
		public float CurrentVolume { get; private set; }

		/// <summary>
		/// Used to find the max volume from the last x number of samples.
		/// </summary>
		public float AverageVolume { get; private set; }

		/// <summary>
		/// Gets or sets the mic sensitivity.
		/// </summary>
		/// <value>The mic sensitivity.</value>
		public float MicSensitivity { get; set; }

		public bool IsTalking
		{
			get
			{
				return AverageVolume >= MicSensitivity;
			}
		}

		#endregion Properties

		#region Constructors

		/// <summary>
		/// Initialize the microphone
		/// </summary>
		/// <param name="deviceCaptureName">Name of the Device used for capturing audio</param>
		public Microphone(string deviceCaptureName)
		{
			MicSensitivity = 0.05f;
			CurrentVolume = 0.0f;
			AverageVolume = 0.0f;
			MicrophoneName = deviceCaptureName;
			InitializeMicrophone(audioSamplingRate, microphoneGain, deviceCaptureName, ALFormat.Mono16, numberOfSamples);
		}

		/// <summary>
		/// Initialize the default microphone
		/// </summary>
		public Microphone() : this(AudioCapture.DefaultDevice)
		{
		}

		#endregion Constructors

		#region Methods

		public static void EnumerateMicrophones()
		{
			// Add available capture devices to the combo box.
			AvailableMicrophones = AudioCapture.AvailableDevices;
			int i = 0;
			while (i < AvailableMicrophones.Count)
			{
				if (String.IsNullOrEmpty(AvailableMicrophones[i]))
				{
					AvailableMicrophones.RemoveAt(i);
				}
				else
				{
					i++;
				}
			}
		}

		/// <summary>
		/// Start recording from the Microphone
		/// </summary>
		public void StartRecording()
		{
			if (IsMicrophoneValid)
			{
				//Start capturing data
				audio_capture.Start();
				continuePolling = true;

				//Spin up the thread to process all the mic data
				if (workerThread == null || workerThread.IsAlive == false)
				{
					workerThread = new Thread(PollMicrophoneForData);
					workerThread.Start();
				}
			}
		}

		/// <summary>
		/// Stop recording from the Microphone
		/// </summary>
		public void StopRecording()
		{
			if (IsMicrophoneValid)
			{
				continuePolling = false;
				audio_capture.Stop();
				ClearBuffers(0);
			}
		}

		/// <summary>
		/// Clears the Microphone buffers
		/// </summary>
		/// <param name="input">Which buffer</param>
		private void ClearBuffers(int input)
		{
			int[] freedbuffers;
			if (input == 0)
			{
				int BuffersProcessed;
				AL.GetSource(src, ALGetSourcei.BuffersProcessed, out BuffersProcessed);
				if (BuffersProcessed == 0)
					return;
				freedbuffers = AL.SourceUnqueueBuffers(src, BuffersProcessed);
			}
			else
			{
				freedbuffers = AL.SourceUnqueueBuffers(src, input);
			}
			AL.DeleteBuffers(freedbuffers);
		}

		private bool InitializeMicrophone(int samplingRate, float gain, string deviceCaptureName, ALFormat format, int bufferSize)
		{
			AL.Listener(ALListenerf.Gain, gain);

			src = AL.GenSource();

			SampleToByte = NumberOfBytesPerSample(format);

			buffer = new byte[bufferSize * SampleToByte];

			try
			{
				audio_capture = new AudioCapture(deviceCaptureName, samplingRate, format, bufferSize);
			}
			catch (AudioDeviceException)
			{
				audio_capture = null;
			}

			return (audio_capture != null);
		}

		private static int NumberOfBytesPerSample(ALFormat format)
		{
			switch (format)
			{
				case ALFormat.Mono16:
				return 2;
				case ALFormat.Mono8:
				return 1;
				case ALFormat.MonoALawExt:
				return 1;
				case ALFormat.MonoDoubleExt:
				return 8;
				case ALFormat.MonoFloat32Ext:
				return 4;
				case ALFormat.MonoIma4Ext:
				return 4;
				case ALFormat.MonoMuLawExt:
				return 1;
				case ALFormat.Mp3Ext:
				return 2; //Guessed might not be correct
				case ALFormat.Multi51Chn16Ext:
				return 6 * 2;
				case ALFormat.Multi51Chn32Ext:
				return 6 * 4;
				case ALFormat.Multi51Chn8Ext:
				return 6 * 1;
				case ALFormat.Multi61Chn16Ext:
				return 7 * 2;
				case ALFormat.Multi61Chn32Ext:
				return 7 * 4;
				case ALFormat.Multi61Chn8Ext:
				return 7 * 1;
				case ALFormat.Multi71Chn16Ext:
				return 7 * 2;
				case ALFormat.Multi71Chn32Ext:
				return 7 * 4;
				case ALFormat.Multi71Chn8Ext:
				return 7 * 1;
				case ALFormat.MultiQuad16Ext:
				return 4 * 2;
				case ALFormat.MultiQuad32Ext:
				return 4 * 4;
				case ALFormat.MultiQuad8Ext:
				return 4 * 1;
				case ALFormat.MultiRear16Ext:
				return 1 * 2;
				case ALFormat.MultiRear32Ext:
				return 1 * 4;
				case ALFormat.MultiRear8Ext:
				return 1 * 1;
				case ALFormat.Stereo16:
				return 2 * 2;
				case ALFormat.Stereo8:
				return 2 * 1;
				case ALFormat.StereoALawExt:
				return 2 * 1;
				case ALFormat.StereoDoubleExt:
				return  2 * 8;
				case ALFormat.StereoFloat32Ext:
				return  2 * 4;
				case ALFormat.StereoIma4Ext:
				return  1; //Guessed
				case ALFormat.StereoMuLawExt:
				return  2 * 1;
				case ALFormat.VorbisExt:
				return  2; //Guessed
				default:
				return 2;
			}
		}

		/// <summary>
		/// Used to poll the Microphone for data
		/// </summary>
		private void PollMicrophoneForData()
		{
			while (continuePolling)
			{
				Thread.Sleep(1); //Allow GUI some time

				if (audio_capture.AvailableSamples * SampleToByte >= buffer.Length)
				{
					UpdateSamples();
				}
			}
		}

		private void UpdateSamples()
		{
			buffer = new byte[buffer.Length];
			audio_capture.ReadSamples(buffer, buffer.Length / SampleToByte); //Need to divide as the readsamples expects the value to be in 2 bytes.

			//Queue raw data, let receiving application determine if it needs to compress
			// Gets volume and pitch values
			AnalyzeSound();

			//Run a series of algorithms to decide whether a player is talking.
			DeriveIsTalking();

			ClearBuffers(0);
		}

		private void AnalyzeSound()
		{
			//CurrentVolume = GetAverageWaveform();
			//CurrentVolume = GetDecibel();
			CurrentVolume = GetLargestWaveform();
		}

		/// <summary>
		/// Updates a record, by removing the oldest entry and adding the newest value (val).
		/// </summary>
		/// <param name="val">Value.</param>
		/// <param name="record">Record.</param>
		private void UpdateRecords(float val, List<float> record)
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
			UpdateRecords(CurrentVolume, dbValues);

			//Find the largest value in the current list
			AverageVolume = 0.0f;
			for (int i = 0; i < dbValues.Count; i++)
			{
				if (dbValues[i] > AverageVolume)
				{
					AverageVolume = dbValues[i];
				}
			}
		}

		private float GetDecibel()
		{
			double sum = 0;
			for (var i = 0; i < buffer.Length; i = i + 2)
			{
				double sample = System.BitConverter.ToInt16(buffer, i) / 32768.0;
				sum += (sample * sample);
			}
			double rms = Math.Sqrt(sum / (buffer.Length / 2));
			return (float)(20.0 * Math.Log10(rms));
		}

		private float GetLargestWaveform()
		{
			//Get the largest waveform from all the current samples.
			float fMaxDB = 0.0f;
			for (var i = 0; i < buffer.Length; i = i + 2)
			{
				float sample = System.BitConverter.ToInt16(buffer, i) / 32768.0f;
				if (sample > fMaxDB)
				{
					fMaxDB = sample;
				}
			}
			return fMaxDB;
		}

		private float GetAverageWaveform()
		{
			//Get the average waveform from all the current samples.
			float sum = 0.0f;
			for (var i = 0; i < buffer.Length; i = i + 2)
			{
				float sample = System.BitConverter.ToInt16(buffer, i) / 32768.0f;
				sum += (sample * sample);
			}
			return (sum / (buffer.Length / 2));
		}

		#endregion Methods
	}
}