using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Media;
using System.Threading.Tasks;

namespace MicBuddyLib
{
	public class MicBuddy : IMicrophone
	{
		#region Fields
		/// <summary>
		/// The android audio capture device
		/// </summary>
		AudioRecord audioRecord = null;

		/// <summary>
		/// The buffer to hold audio data.  
		/// </summary>
		private byte[] buffer;

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

		/// <summary>
		/// flag that gets set when we want to stop recording
		/// </summary>
		private bool endRecording = false;

		#endregion Fields

		#region Properties

		/// <summary>
		/// A list of all the available microphones
		/// </summary>
		public static List<string> AvailableMicrophones { get; private set; }

		/// <summary>
		/// Gets a value indicating whether this instance is microphone valid.
		/// </summary>
		/// <value><c>true</c> if this instance is microphone valid; otherwise, <c>false</c>.</value>
		public bool IsMicrophoneValid
		{
			get
			{
				return (null != audioRecord);
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
		public MicBuddy()
		{
			MicSensitivity = 0.05f;
			CurrentVolume = 0.0f;
			AverageVolume = 0.0f;
		}

		#endregion Constructors

		#region Methods

		async Task ReadAudioAsync()
		{
			while (true)
			{
				if (endRecording)
				{
					endRecording = false;
					break;
				}

				try
				{
					// Keep reading the buffer while there is audio input.
					int numBytes = await audioRecord.ReadAsync(buffer, 0, buffer.Length);
					// Do something with the audio input.
					UpdateSamples();
				}
				catch (Exception ex)
				{
					Console.Out.WriteLine(ex.Message);
					break;
				}
			}
			audioRecord.Stop();
			audioRecord.Release();
		}

		protected async Task StartRecorderAsync()
		{
			endRecording = false;

			buffer = new Byte[100000];
			audioRecord = new AudioRecord(
				// Hardware source of recording.
				AudioSource.Mic,
				// Frequency
				11025,
				// Mono or stereo
				ChannelIn.Mono,
				// Audio encoding
				Android.Media.Encoding.Pcm16bit,
				// Length of the audio clip.
				buffer.Length
			);

			audioRecord.StartRecording();

			// Off line this so that we do not block the UI thread.
			await ReadAudioAsync();
		}

		private async Task StartAsync()
		{
			await StartRecorderAsync();
		}

		/// <summary>
		/// Start recording from the Microphone
		/// </summary>
		async public void StartRecording()
		{
			await StartAsync();
		}

		/// <summary>
		/// Stop recording from the Microphone
		/// </summary>
		public void StopRecording()
		{
			endRecording = true;
			Thread.Sleep(500); // Give it time to drop out.
		}

		private void UpdateSamples()
		{
			//Queue raw data, let receiving application determine if it needs to compress
			// Gets volume and pitch values
			AnalyzeSound();

			//Run a series of algorithms to decide whether a player is talking.
			DeriveIsTalking();
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