using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Audio;

namespace MicBuddyLib
{
	public class MicBuddy : IMicrophone
	{
		#region static default fields 

		/// <summary>
		/// The default microhpohe to use
		/// </summary>
		public static string DefaultMicName { get; set; }

		/// <summary>
		/// The default mic sensitivity to use
		/// </summary>
		public static float DefaultSensitvity { get; set; }

		/// <summary>
		/// A list of all the available microphones
		/// </summary>
		public static List<string> AvailableMicrophones { get; private set; }

		/// <summary>
		/// Map all the mic names to microphone instances
		/// </summary>
		private static Dictionary<string, Microphone> MicDict { get; set; }

		private const float StartMicSensitvity = 0.05f;

		#endregion //static default fields

		#region Fields

		/// <summary>
		/// The buffer to hold audio data.  
		/// </summary>
		private byte[] buffer;

		/// <summary>
		/// The micrphone we gonna use
		/// </summary>
		private Microphone _Mic;

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
		/// How many previous frames of sound are analyzed.
		/// </summary>
		public const int recordedLength = 10;

		#endregion Fields

		#region Properties

		/// <summary>
		/// Gets a value indicating whether this instance is microphone valid.
		/// </summary>
		/// <value><c>true</c> if this instance is microphone valid; otherwise, <c>false</c>.</value>
		public bool IsMicrophoneValid
		{
			get
			{
				return (null != _Mic);
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
		/// Gets or sets the mic sensitivity for this particular microhphone.
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
		/// initialize the static fields
		/// </summary>
		static MicBuddy()
		{
			//Set the default microhpone to the built-in mic
			DefaultMicName = Microphone.Default.Name;

			//set the default mic sensitivity
			DefaultSensitvity = StartMicSensitvity;

			// Add available capture devices to the combo box.
			AvailableMicrophones = new List<string>();
			MicDict = new Dictionary<string, Microphone>();
			foreach (var dude in Microphone.All)
			{
				if (!String.IsNullOrEmpty(dude.Name))
				{
					AvailableMicrophones.Add(dude.Name);
					MicDict.Add(dude.Name, dude);
				}
			}
		}

		/// <summary>
		/// Initialize the microphone
		/// </summary>
		/// <param name="deviceCaptureName">Name of the Device used for capturing audio</param>
		public MicBuddy(string deviceCaptureName)
		{
			_Mic = MicBuddy.MicDict[deviceCaptureName];
			MicSensitivity = DefaultSensitvity;
			CurrentVolume = 0.0f;
			AverageVolume = 0.0f;
			MicrophoneName = deviceCaptureName;
		}

		/// <summary>
		/// Initialize the default microphone
		/// </summary>
		public MicBuddy()
			: this(DefaultMicName)
		{
		}

		#endregion Constructors

		#region Methods

		/// <summary>
		/// Start recording from the Microphone
		/// </summary>
		public void StartRecording()
		{
			if (IsMicrophoneValid)
			{
				_Mic.BufferDuration = TimeSpan.FromSeconds(0.1);
				buffer = new byte[_Mic.GetSampleSizeInBytes(_Mic.BufferDuration)];
				_Mic.BufferReady += handleBufferReady;
				_Mic.Start();
			}
		}

		/// <summary>
		/// Stop recording from the Microphone
		/// </summary>
		public void StopRecording()
		{
			if (IsMicrophoneValid)
			{
				_Mic.Stop();
			}
		}

		/// <summary>
		/// Used to poll the Microphone for data
		/// </summary>
		private void handleBufferReady(object sender, EventArgs e) 
		{
			_Mic.GetData(buffer);
			UpdateSamples();
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