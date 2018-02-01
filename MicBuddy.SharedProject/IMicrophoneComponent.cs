using Microsoft.Xna.Framework.Audio;
using System.Collections.Generic;

namespace MicBuddyLib
{
	public interface IMicrophoneComponent
	{
		string DefaultMicName { get; }

		float DefaultSensitvity { get; set; }

		List<string> AvailableMicrophones { get; }

		Dictionary<string, Microphone> Microphones { get; }
	}
}
