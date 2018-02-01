
namespace MicBuddyLib
{
	public interface IMicrophoneHelper
	{
		#region Properties

		string MicrophoneName { get; }

		/// <summary>
		/// Gets a value indicating whether this instance is microphone valid.
		/// </summary>
		/// <value><c>true</c> if this instance is microphone valid; otherwise, <c>false</c>.</value>
		bool IsMicrophoneValid { get; }

		/// <summary>
		/// The sound volume of the current frame. (stored in dannobels).
		/// </summary>
		float CurrentVolume { get; }

		/// <summary>
		/// Used to find the max volume from the last x number of samples.
		/// </summary>
		float AverageVolume { get; }

		/// <summary>
		/// Gets or sets the mic sensitivity.
		/// </summary>
		/// <value>The mic sensitivity.</value>
		float MicSensitivity { get; set; }

		bool IsTalking { get; }

		#endregion Properties

		#region Methods

		/// <summary>
		/// Start recording from the Microphone
		/// </summary>
		void StartRecording();

		/// <summary>
		/// Stop recording from the Microphone
		/// </summary>
		void StopRecording();

		#endregion Methods
	}
}